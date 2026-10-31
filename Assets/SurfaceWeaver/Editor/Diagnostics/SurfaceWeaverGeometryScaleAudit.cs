using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor.Diagnostics
{
    /// <summary>
    /// Read-only diagnostic that reports raw local and world triangle cross-product magnitudes.
    /// It does not modify the mesh, target, profile, masks, or materials.
    /// </summary>
    public sealed class SurfaceWeaverGeometryScaleAudit : EditorWindow
    {
        [SerializeField] private GameObject targetObject;
        [SerializeField] private float builderCrossSqrThreshold = 1e-7f;
        [SerializeField] private int sampleCount = 12;
        private string report = "Assign the cylinder GameObject and click Audit Geometry Scale.";
        private Vector2 scroll;

        [MenuItem("Tools/SurfaceWeaver/Diagnostics/Geometry Scale Audit")]
        private static void Open()
        {
            SurfaceWeaverGeometryScaleAudit window = GetWindow<SurfaceWeaverGeometryScaleAudit>("SW Geometry Audit");
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SurfaceWeaver Geometry Scale Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Read-only diagnostic. It measures every triangle's raw local-space and world-space cross-product magnitude. " +
                "This specifically checks whether the absolute 1e-7 geometry threshold rejects small but valid imported triangles.",
                MessageType.Info);

            targetObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", targetObject, typeof(GameObject), true);
            builderCrossSqrThreshold = EditorGUILayout.FloatField("Builder cross.sqrMagnitude threshold", builderCrossSqrThreshold);
            sampleCount = EditorGUILayout.IntSlider("Sample rows", sampleCount, 4, 40);

            using (new EditorGUI.DisabledScope(targetObject == null))
            {
                if (GUILayout.Button("Audit Geometry Scale", GUILayout.Height(28f)))
                    RunAudit();
            }

            if (GUILayout.Button("Copy Report") && !string.IsNullOrEmpty(report))
                EditorGUIUtility.systemCopyBuffer = report;

            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunAudit()
        {
            StringBuilder sb = new StringBuilder(8192);
            MeshFilter filter = targetObject != null ? targetObject.GetComponent<MeshFilter>() : null;
            MeshCollider collider = targetObject != null ? targetObject.GetComponent<MeshCollider>() : null;

            if (filter == null || filter.sharedMesh == null)
            {
                report = "ERROR: Target GameObject has no MeshFilter/sharedMesh.";
                Debug.LogError(report, targetObject);
                return;
            }

            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                report = "ERROR: Mesh is not readable. Enable Read/Write in the model import settings.";
                Debug.LogError(report, targetObject);
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Matrix4x4 l2w = filter.transform.localToWorldMatrix;

            sb.AppendLine("[SurfaceWeaver Geometry Scale Audit]");
            sb.AppendLine("Target: " + targetObject.name);
            sb.AppendLine("Mesh: " + mesh.name);
            sb.AppendLine("Readable: " + mesh.isReadable);
            sb.AppendLine("Vertices: " + vertices.Length);
            sb.AppendLine("Submeshes: " + mesh.subMeshCount);
            sb.AppendLine("Mesh local bounds size: " + F(mesh.bounds.size));
            sb.AppendLine("Mesh local bounds extents: " + F(mesh.bounds.extents));
            sb.AppendLine("Transform lossyScale: " + F(filter.transform.lossyScale));
            sb.AppendLine("Collider shares mesh: " + (collider != null && collider.sharedMesh == mesh));
            sb.AppendLine("UV0 length: " + (uvs != null ? uvs.Length.ToString(CultureInfo.InvariantCulture) : "null"));
            sb.AppendLine("Builder geometry threshold (cross.sqrMagnitude): " + builderCrossSqrThreshold.ToString("E6", CultureInfo.InvariantCulture));
            sb.AppendLine();

            int triCount = 0, invalid = 0, uvDegenerate = 0;
            int localNonFinite = 0, localZero = 0, localBelowThreshold = 0, localAboveThreshold = 0;
            int worldNonFinite = 0, worldZero = 0, worldBelowThreshold = 0, worldAboveThreshold = 0;
            double minLocalSqr = double.PositiveInfinity, maxLocalSqr = 0.0;
            double minWorldSqr = double.PositiveInfinity, maxWorldSqr = 0.0;
            double minLocalNonZero = double.PositiveInfinity, maxLocalNonZero = 0.0;
            double minWorldNonZero = double.PositiveInfinity, maxWorldNonZero = 0.0;
            List<string> samples = new List<string>();

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                    continue;

                int[] indices;
                try { indices = mesh.GetIndices(sub, true); }
                catch (Exception ex)
                {
                    sb.AppendLine("Submesh " + sub + " GetIndices failed: " + ex.Message);
                    continue;
                }

                for (int k = 0; k + 2 < indices.Length; k += 3)
                {
                    triCount++;
                    int i0 = indices[k];
                    int i1 = indices[k + 1];
                    int i2 = indices[k + 2];
                    if (!Valid(i0, vertices.Length) || !Valid(i1, vertices.Length) || !Valid(i2, vertices.Length))
                    {
                        invalid++;
                        continue;
                    }

                    Vector3 p0 = vertices[i0];
                    Vector3 p1 = vertices[i1];
                    Vector3 p2 = vertices[i2];
                    Vector3 e1 = p1 - p0;
                    Vector3 e2 = p2 - p0;
                    // Compute cross products in double precision so the audit itself does not
                    // overflow/underflow before it can report the raw mesh scale.
                    double lx, ly, lz;
                    CrossComponents(e1, e2, out lx, out ly, out lz);
                    double localSqr = lx * lx + ly * ly + lz * lz;

                    Vector3 wp0 = l2w.MultiplyPoint3x4(p0);
                    Vector3 wp1 = l2w.MultiplyPoint3x4(p1);
                    Vector3 wp2 = l2w.MultiplyPoint3x4(p2);
                    Vector3 we1 = wp1 - wp0;
                    Vector3 we2 = wp2 - wp0;
                    double wx, wy, wz;
                    CrossComponents(we1, we2, out wx, out wy, out wz);
                    double worldSqr = wx * wx + wy * wy + wz * wz;

                    if (uvs != null && uvs.Length == vertices.Length)
                    {
                        Vector2 a = uvs[i0], b = uvs[i1], c = uvs[i2];
                        double det = ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)c.x - a.x) * ((double)b.y - a.y);
                        if (Math.Abs(det) < 1e-7) uvDegenerate++;
                    }

                    Classify(localSqr, builderCrossSqrThreshold, ref localNonFinite, ref localZero,
                        ref localBelowThreshold, ref localAboveThreshold, ref minLocalSqr, ref maxLocalSqr,
                        ref minLocalNonZero, ref maxLocalNonZero);
                    Classify(worldSqr, builderCrossSqrThreshold, ref worldNonFinite, ref worldZero,
                        ref worldBelowThreshold, ref worldAboveThreshold, ref minWorldSqr, ref maxWorldSqr,
                        ref minWorldNonZero, ref maxWorldNonZero);

                    if (samples.Count < sampleCount)
                    {
                        double localLength = Math.Sqrt(localSqr);
                        string localNormal = localLength > 0.0
                            ? "(" + (lx / localLength).ToString("G6", CultureInfo.InvariantCulture) + ", " +
                              (ly / localLength).ToString("G6", CultureInfo.InvariantCulture) + ", " +
                              (lz / localLength).ToString("G6", CultureInfo.InvariantCulture) + ")"
                            : "(0, 0, 0)";
                        string localCross = "(" + lx.ToString("G6", CultureInfo.InvariantCulture) + ", " +
                            ly.ToString("G6", CultureInfo.InvariantCulture) + ", " +
                            lz.ToString("G6", CultureInfo.InvariantCulture) + ")";
                        samples.Add(string.Format(CultureInfo.InvariantCulture,
                            "tri {0}: indices=({1},{2},{3}) localCross={4} localCrossSqr={5:E6} worldCrossSqr={6:E6} localNormal={7}",
                            triCount - 1, i0, i1, i2, localCross, localSqr, worldSqr, localNormal));
                    }
                }
            }

            sb.AppendLine("--- Summary ---");
            sb.AppendLine("Triangle records: " + triCount);
            sb.AppendLine("Invalid index triangles: " + invalid);
            sb.AppendLine("UV-degenerate triangles (|det| < 1e-7): " + uvDegenerate);
            sb.AppendLine("Local cross: non-finite=" + localNonFinite + ", zero=" + localZero +
                           ", below threshold=" + localBelowThreshold + ", at/above threshold=" + localAboveThreshold);
            sb.AppendLine("World cross: non-finite=" + worldNonFinite + ", zero=" + worldZero +
                           ", below threshold=" + worldBelowThreshold + ", at/above threshold=" + worldAboveThreshold);
            sb.AppendLine("Local cross.sqrMagnitude raw range: [" + Range(minLocalSqr, maxLocalSqr) + "]");
            sb.AppendLine("Local non-zero cross.sqrMagnitude range: [" + Range(minLocalNonZero, maxLocalNonZero) + "]");
            sb.AppendLine("World cross.sqrMagnitude raw range: [" + Range(minWorldSqr, maxWorldSqr) + "]");
            sb.AppendLine("World non-zero cross.sqrMagnitude range: [" + Range(minWorldNonZero, maxWorldNonZero) + "]");
            sb.AppendLine();
            sb.AppendLine("--- First " + samples.Count + " triangles ---");
            for (int i = 0; i < samples.Count; i++) sb.AppendLine(samples[i]);
            sb.AppendLine();
            sb.AppendLine("NOTE: SurfaceUvRasterMesh currently tests the LOCAL-space cross.sqrMagnitude against an absolute threshold.");
            sb.AppendLine("Compare local and world counts. If local triangles are below threshold but world triangles are valid, the absolute local threshold is scale-dependent.");

            report = sb.ToString();
            Debug.Log(report, targetObject);
        }

        private static void Classify(double value, float threshold,
            ref int nonFinite, ref int zero, ref int below, ref int above,
            ref double minAll, ref double maxAll, ref double minNonZero, ref double maxNonZero)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) { nonFinite++; return; }
            minAll = Math.Min(minAll, value);
            maxAll = Math.Max(maxAll, value);
            if (value <= 1e-30) { zero++; return; }
            minNonZero = Math.Min(minNonZero, value);
            maxNonZero = Math.Max(maxNonZero, value);
            if (value < threshold) below++;
            else above++;
        }

        private static void CrossComponents(Vector3 a, Vector3 b, out double x, out double y, out double z)
        {
            x = (double)a.y * b.z - (double)a.z * b.y;
            y = (double)a.z * b.x - (double)a.x * b.z;
            z = (double)a.x * b.y - (double)a.y * b.x;
        }

        private static bool Valid(int index, int count) { return index >= 0 && index < count; }
        private static string F(Vector3 v) { return "(" + v.x.ToString("G6", CultureInfo.InvariantCulture) + ", " + v.y.ToString("G6", CultureInfo.InvariantCulture) + ", " + v.z.ToString("G6", CultureInfo.InvariantCulture) + ")"; }
        private static string Range(double min, double max)
        {
            if (double.IsPositiveInfinity(min)) return "no finite samples";
            return min.ToString("E6", CultureInfo.InvariantCulture) + ", " + max.ToString("E6", CultureInfo.InvariantCulture);
        }
    }
}
