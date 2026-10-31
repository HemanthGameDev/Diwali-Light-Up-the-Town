using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Read-only diagnostic. Reports why SurfaceUvRasterMesh may reject all source triangles.
    /// Does not create or modify meshes, materials, masks, profiles, or scene objects.
    /// </summary>
    internal sealed class SurfaceWeaverUvTriangleAudit : EditorWindow
    {
        private GameObject targetObject;
        private Vector2 scroll;
        private string report = "Assign the cylinder GameObject and click Audit UV Triangles.";

        [MenuItem("Tools/SurfaceWeaver/Diagnostics/UV Triangle Audit")]
        private static void Open()
        {
            var window = GetWindow<SurfaceWeaverUvTriangleAudit>("SW UV Triangle Audit");
            window.minSize = new Vector2(460f, 430f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SurfaceWeaver UV Triangle Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Read-only diagnostic for RASTER BUILD FAILED. It inspects the exact MeshFilter.sharedMesh used by the target and counts every triangle rejection reason. It does not paint or modify assets.",
                MessageType.Info);

            targetObject = (GameObject)EditorGUILayout.ObjectField(
                "Target GameObject", targetObject, typeof(GameObject), true);

            using (new EditorGUI.DisabledScope(targetObject == null))
            {
                if (GUILayout.Button("Audit UV Triangles", GUILayout.Height(32f)))
                    RunAudit();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Audit Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true), GUILayout.MinHeight(300f));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy Report") && !string.IsNullOrEmpty(report))
                EditorGUIUtility.systemCopyBuffer = report;
        }

        private void RunAudit()
        {
            var sb = new StringBuilder(8192);
            MeshFilter filter = targetObject != null ? targetObject.GetComponent<MeshFilter>() : null;
            MeshCollider collider = targetObject != null ? targetObject.GetComponent<MeshCollider>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            sb.AppendLine("[SurfaceWeaver UV Triangle Audit]");
            sb.AppendLine("GameObject: " + (targetObject != null ? targetObject.name : "<none>"));
            sb.AppendLine("MeshFilter: " + (filter != null ? "present" : "MISSING"));
            sb.AppendLine("MeshCollider: " + (collider != null ? "present" : "MISSING"));
            if (collider != null)
            {
                sb.AppendLine("Collider mesh matches MeshFilter: " + (filter != null && collider.sharedMesh == filter.sharedMesh));
                sb.AppendLine("Collider convex: " + collider.convex);
            }

            if (mesh == null)
            {
                sb.AppendLine("RESULT: No MeshFilter.sharedMesh assigned.");
                report = sb.ToString();
                Debug.Log(report, targetObject);
                return;
            }

            sb.AppendLine("Mesh: " + mesh.name);
            sb.AppendLine("Readable: " + mesh.isReadable);
            sb.AppendLine("Vertices: " + mesh.vertexCount);
            sb.AppendLine("Submeshes: " + mesh.subMeshCount);
            sb.AppendLine("Index format: " + mesh.indexFormat);

            if (!mesh.isReadable)
            {
                sb.AppendLine("RESULT: Mesh is not readable. Enable Read/Write in Model Import Settings.");
                report = sb.ToString();
                Debug.Log(report, targetObject);
                return;
            }

            Vector3[] vertices;
            Vector2[] uv0;
            try
            {
                vertices = mesh.vertices;
                uv0 = mesh.uv;
            }
            catch (Exception ex)
            {
                sb.AppendLine("RESULT: Failed reading vertices/UV0: " + ex);
                report = sb.ToString();
                Debug.LogError(report, targetObject);
                return;
            }

            sb.AppendLine("mesh.vertices length: " + (vertices != null ? vertices.Length : -1));
            sb.AppendLine("mesh.uv (UV0) length: " + (uv0 != null ? uv0.Length : -1));
            sb.AppendLine("UV0 matches vertex count: " + (vertices != null && uv0 != null && vertices.Length == uv0.Length));

            if (vertices == null || uv0 == null || uv0.Length != vertices.Length)
            {
                sb.AppendLine("RESULT: UV0 must contain one coordinate per mesh vertex.");
                report = sb.ToString();
                Debug.LogError(report, targetObject);
                return;
            }

            float minU = float.PositiveInfinity, minV = float.PositiveInfinity;
            float maxU = float.NegativeInfinity, maxV = float.NegativeInfinity;
            int nonFiniteUvs = 0, outOfRangeUvs = 0;
            for (int i = 0; i < uv0.Length; i++)
            {
                Vector2 uv = uv0[i];
                if (!Finite(uv.x) || !Finite(uv.y)) { nonFiniteUvs++; continue; }
                minU = Mathf.Min(minU, uv.x); maxU = Mathf.Max(maxU, uv.x);
                minV = Mathf.Min(minV, uv.y); maxV = Mathf.Max(maxV, uv.y);
                if (uv.x < -0.0001f || uv.x > 1.0001f || uv.y < -0.0001f || uv.y > 1.0001f)
                    outOfRangeUvs++;
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "UV0 bounds: U[{0:F6}, {1:F6}] V[{2:F6}, {3:F6}]", minU, maxU, minV, maxV));
            sb.AppendLine("Non-finite UV vertices: " + nonFiniteUvs);
            sb.AppendLine("Out-of-range UV vertices: " + outOfRangeUvs);

            int totalTriangles = 0, invalidIndices = 0, nonFiniteTriangleData = 0;
            int outOfRangeTriangles = 0, uvDegenerate = 0, geometryDegenerate = 0, validTriangles = 0;
            float minNonZeroUvDet = float.PositiveInfinity, maxUvDet = 0f;
            float minGeomCrossSqr = float.PositiveInfinity, maxGeomCrossSqr = 0f;
            var sampleLines = new List<string>();

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                MeshTopology topology = mesh.GetTopology(sub);
                uint baseVertex = mesh.GetBaseVertex(sub);
                int[] indices;
                try { indices = mesh.GetIndices(sub, true); }
                catch (Exception ex)
                {
                    sb.AppendLine("Submesh " + sub + " GetIndices failed: " + ex.Message);
                    continue;
                }

                sb.AppendLine(string.Format("Submesh {0}: topology={1}, baseVertex={2}, indices={3}",
                    sub, topology, baseVertex, indices != null ? indices.Length : -1));

                if (topology != MeshTopology.Triangles || indices == null)
                    continue;

                if (indices.Length % 3 != 0)
                    sb.AppendLine("WARNING: triangle index count is not divisible by 3.");

                for (int k = 0; k + 2 < indices.Length; k += 3)
                {
                    totalTriangles++;
                    int i0 = indices[k], i1 = indices[k + 1], i2 = indices[k + 2];
                    if (!Valid(i0, vertices.Length) || !Valid(i1, vertices.Length) || !Valid(i2, vertices.Length))
                    {
                        invalidIndices++;
                        continue;
                    }

                    Vector3 p0 = vertices[i0], p1 = vertices[i1], p2 = vertices[i2];
                    Vector2 a = uv0[i0], b = uv0[i1], c = uv0[i2];
                    if (!Finite(p0) || !Finite(p1) || !Finite(p2) ||
                        !Finite(a.x) || !Finite(a.y) || !Finite(b.x) || !Finite(b.y) || !Finite(c.x) || !Finite(c.y))
                    {
                        nonFiniteTriangleData++;
                        continue;
                    }
                    if (a.x < -0.0001f || a.x > 1.0001f || a.y < -0.0001f || a.y > 1.0001f ||
                        b.x < -0.0001f || b.x > 1.0001f || b.y < -0.0001f || b.y > 1.0001f ||
                        c.x < -0.0001f || c.x > 1.0001f || c.y < -0.0001f || c.y > 1.0001f)
                    {
                        outOfRangeTriangles++;
                        continue;
                    }

                    float det = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                    float crossSqr = Vector3.Cross(p1 - p0, p2 - p0).sqrMagnitude;
                    if (Mathf.Abs(det) < 0.0000001f)
                    {
                        uvDegenerate++;
                        if (sampleLines.Count < 6)
                            sampleLines.Add(string.Format(CultureInfo.InvariantCulture,
                                "UV-degenerate tri {0}: idx=({1},{2},{3}) uv=({4:F6},{5:F6}) ({6:F6},{7:F6}) ({8:F6},{9:F6}) det={10:E4}",
                                totalTriangles - 1, i0, i1, i2, a.x, a.y, b.x, b.y, c.x, c.y, det));
                        continue;
                    }
                    minNonZeroUvDet = Mathf.Min(minNonZeroUvDet, Mathf.Abs(det));
                    maxUvDet = Mathf.Max(maxUvDet, Mathf.Abs(det));

                    if (!Finite(crossSqr) || crossSqr < 0.0000001f)
                    {
                        geometryDegenerate++;
                        continue;
                    }
                    minGeomCrossSqr = Mathf.Min(minGeomCrossSqr, crossSqr);
                    maxGeomCrossSqr = Mathf.Max(maxGeomCrossSqr, crossSqr);
                    validTriangles++;
                }
            }

            int skippedNonTriangle = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) skippedNonTriangle++;

            sb.AppendLine("--- Triangle counts ---");
            sb.AppendLine("Total triangle records: " + totalTriangles);
            sb.AppendLine("Invalid index triangles: " + invalidIndices);
            sb.AppendLine("Non-finite data triangles: " + nonFiniteTriangleData);
            sb.AppendLine("Out-of-range UV triangles: " + outOfRangeTriangles);
            sb.AppendLine("UV-degenerate triangles (|det| < 1e-7): " + uvDegenerate);
            sb.AppendLine("Geometry-degenerate triangles: " + geometryDegenerate);
            sb.AppendLine("Valid raster candidates: " + validTriangles);
            sb.AppendLine("Skipped non-triangle submeshes: " + skippedNonTriangle);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Abs UV determinant range (non-zero): [{0:E4}, {1:E4}]", minNonZeroUvDet, maxUvDet));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Geometry cross-product sqrMagnitude range: [{0:E4}, {1:E4}]", minGeomCrossSqr, maxGeomCrossSqr));

            if (sampleLines.Count > 0)
            {
                sb.AppendLine("--- First UV-degenerate samples ---");
                foreach (string line in sampleLines) sb.AppendLine(line);
            }

            // Compare the flattened Mesh.triangles accessor with the submesh loop used by SurfaceUvRasterMesh.
            try
            {
                int[] flattened = mesh.triangles;
                sb.AppendLine("mesh.triangles flattened index count: " + flattened.Length);
                sb.AppendLine("Submesh-read total index count: " + (totalTriangles * 3 + (mesh.subMeshCount > 0 ? 0 : 0)));
            }
            catch (Exception ex) { sb.AppendLine("mesh.triangles read failed: " + ex.Message); }

            if (validTriangles == 0)
            {
                sb.AppendLine("RESULT: The same builder threshold would reject every triangle. Use the rejection counts above to identify why.");
            }
            else
            {
                sb.AppendLine("RESULT: This mesh contains valid UV0 raster candidates. The previous failure may involve a different target/mesh instance or a stale imported mesh reference.");
            }

            report = sb.ToString();
            Debug.Log(report, targetObject);
        }

        private static bool Valid(int index, int count) => index >= 0 && index < count;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
