using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Non-destructive diagnostic for the seam-aware UV raster pipeline.
    /// It captures a mesh hit, then separately measures UV raster coverage,
    /// projection-gate acceptance, and output from the production stamp engine.
    /// It never writes to the user's assigned mask asset or active painting session.
    /// </summary>
    internal sealed class SurfaceWeaverGpuProbeWindow : EditorWindow
    {
        private const string ProbeShaderName = "Hidden/SurfaceWeaver/MeshProjectionProbe";

        private SurfacePaintTarget target;
        private bool captureSceneHit;
        private bool hasHit;
        private Vector3 hitPoint;
        private Vector3 hitNormal = Vector3.up;
        private Vector2 hitUv;
        private int hitTriangle = -1;
        private float brushRadius = 0.2f;
        private float projectionDepth = 0.15f;
        private float normalCutoff = 0.2f;
        private int resolution = 1024;
        private int channel;
        private string result = "Assign a target, enable Capture Hit, then click the cylinder side in Scene View.";
        private Vector2 scroll;

        [MenuItem("Tools/SurfaceWeaver/Diagnostics/GPU Projection Probe")]
        private static void Open()
        {
            SurfaceWeaverGpuProbeWindow window = GetWindow<SurfaceWeaverGpuProbeWindow>("SW GPU Probe");
            window.minSize = new Vector2(360f, 430f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("SurfaceWeaver GPU Projection Probe", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Non-destructive test. It does not modify your profile, active mask page, or saved textures. " +
                "Close the regular SurfaceWeaver Painter while capturing the test hit to avoid Scene View input conflicts.",
                MessageType.Info);

            target = (SurfacePaintTarget)EditorGUILayout.ObjectField("Paint Target", target, typeof(SurfacePaintTarget), true);
            EditorGUILayout.Space(6);
            captureSceneHit = EditorGUILayout.ToggleLeft("Capture click in Scene View", captureSceneHit);
            if (captureSceneHit)
                EditorGUILayout.HelpBox("Click once on the visible side of the cylinder, away from the caps. The captured point is used for all three probe passes.", MessageType.Warning);

            EditorGUILayout.Space(6);
            brushRadius = EditorGUILayout.FloatField("Brush Radius (World Units)", Mathf.Max(0.001f, brushRadius));
            projectionDepth = EditorGUILayout.FloatField("Projection Depth (World Units)", Mathf.Max(0.001f, projectionDepth));
            normalCutoff = EditorGUILayout.Slider("Facing Threshold", normalCutoff, 0f, 0.95f);
            resolution = EditorGUILayout.IntPopup("Probe Resolution", resolution,
                new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
            channel = EditorGUILayout.IntPopup("Mask Channel", channel,
                new[] { "R", "G", "B", "A" }, new[] { 0, 1, 2, 3 });

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(!hasHit))
            {
                EditorGUILayout.LabelField("Captured Hit", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("UV0", hitUv.ToString("F4"));
                EditorGUILayout.LabelField("Point", hitPoint.ToString("F3"));
                EditorGUILayout.LabelField("Normal", hitNormal.ToString("F3"));
                EditorGUILayout.LabelField("Triangle", hitTriangle.ToString());
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!CanRun()))
            {
                if (GUILayout.Button("Run 3-Stage GPU Probe", GUILayout.Height(34)))
                    RunProbe();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(result, GUILayout.MinHeight(145f));
            EditorGUILayout.HelpBox(
                "Interpretation:\n" +
                "• Coverage = 0: UV raster mesh did not draw to the probe RT.\n" +
                "• Coverage > 0, Accepted = 0: world-space projection gates/attributes reject every fragment.\n" +
                "• Accepted > 0, Stamp = 0: production stamp shader/output path differs from the gate probe.\n" +
                "• All > 0: low-level GPU stamping works; investigate Painter event/session/channel binding.",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private bool CanRun()
        {
            return target != null && hasHit && target.gameObject != null;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!captureSceneHit || target == null || !target.gameObject.activeInHierarchy)
                return;

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null || collider.convex || collider.sharedMesh == null)
                return;

            Event e = Event.current;
            if (e == null || e.alt)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (collider.Raycast(ray, out RaycastHit hit, 100000f))
            {
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
                if (e.type == EventType.Layout)
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    hitPoint = hit.point;
                    hitNormal = hit.normal.normalized;
                    hitUv = hit.textureCoord;
                    hitTriangle = hit.triangleIndex;
                    hasHit = true;
                    captureSceneHit = false;
                    result = "Hit captured. Click Run 3-Stage GPU Probe.";
                    e.Use();
                    Repaint();
                }
            }
            sceneView.Repaint();
        }

        private void RunProbe()
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (filter == null || filter.sharedMesh == null || collider == null || collider.sharedMesh != filter.sharedMesh)
            {
                result = "SETUP ERROR: Target MeshFilter and non-convex MeshCollider must reference the same mesh.";
                return;
            }

            Mesh sourceMesh = filter.sharedMesh;
            if (!sourceMesh.isReadable)
            {
                result = "SETUP ERROR: Enable Read/Write on the mesh import settings.";
                return;
            }

            if (!SurfaceUvRasterMesh.TryBuild(sourceMesh, out SurfaceUvRasterMesh raster, out string buildError))
            {
                result = "RASTER BUILD FAILED: " + buildError;
                return;
            }

            try
            {
                Matrix4x4 localToWorld = target.transform.localToWorldMatrix;
                if (Mathf.Abs(localToWorld.determinant) < 0.000001f)
                {
                    result = "SETUP ERROR: Target transform has zero or near-zero scale.";
                    return;
                }

                if (!TryRenderProbe(raster.Mesh, localToWorld, hitPoint, hitNormal,
                    brushRadius, projectionDepth, normalCutoff, resolution,
                    out int coveragePixels, out int acceptedPixels, out string probeError))
                {
                    result = "PROBE SHADER FAILED: " + probeError;
                    return;
                }

                SurfaceGpuBrushEngine testEngine = new SurfaceGpuBrushEngine();
                int stampedPixels = -1;
                string stampError = string.Empty;
                try
                {
                    if (!testEngine.Initialize(Texture2D.blackTexture, resolution, resolution, out stampError))
                    {
                        result = "TEMP ENGINE INIT FAILED: " + stampError;
                        return;
                    }

                    bool stampOk = testEngine.StampSurfaceMesh(
                        raster.Mesh,
                        localToWorld,
                        hitPoint,
                        hitNormal,
                        brushRadius,
                        projectionDepth,
                        normalCutoff,
                        1f,
                        1f,
                        channel,
                        false,
                        null,
                        0f,
                        false,
                        out stampError);

                    if (!stampOk)
                    {
                        result = "PRODUCTION STAMP FAILED: " + stampError;
                        return;
                    }

                    Texture2D readback = testEngine.CreateReadbackTexture(out stampError);
                    if (readback == null)
                    {
                        result = "PRODUCTION STAMP READBACK FAILED: " + stampError;
                        return;
                    }

                    try
                    {
                        Color32[] pixels = readback.GetPixels32();
                        stampedPixels = CountChannelPixels(pixels, channel, 8);
                    }
                    finally
                    {
                        DestroyObject(readback);
                    }
                }
                finally
                {
                    testEngine.Dispose();
                }

                result =
                    "GPU PROBE COMPLETE\n\n" +
                    "UV raster coverage pixels: " + coveragePixels + "\n" +
                    "Projection-accepted pixels: " + acceptedPixels + "\n" +
                    "Production stamp changed pixels: " + stampedPixels + "\n\n" +
                    Interpret(coveragePixels, acceptedPixels, stampedPixels) +
                    "\n\nHit UV0: " + hitUv.ToString("F4") +
                    "\nHit point: " + hitPoint.ToString("F3") +
                    "\nHit normal: " + hitNormal.ToString("F3") +
                    "\nRadius/depth/cutoff: " + brushRadius.ToString("F3") + " / " +
                    projectionDepth.ToString("F3") + " / " + normalCutoff.ToString("F3");

                Debug.Log("[SurfaceWeaver GPU Probe]\n" + result, target);
            }
            finally
            {
                raster.Dispose();
            }

            Repaint();
        }

        private static string Interpret(int coverage, int accepted, int stamped)
        {
            if (coverage <= 0)
                return "DIAGNOSIS: The UV raster mesh is not producing fragments in the probe RenderTexture. Investigate raster draw setup, clip-space mapping and UV raster geometry.";
            if (accepted <= 0)
                return "DIAGNOSIS: UV triangles rasterize, but the world-space projection gates accept no fragments. Investigate interpolated localPosition/localNormal attributes, target transform uniforms, brush point, radius and depth.";
            if (stamped <= 0)
                return "DIAGNOSIS: The independent projection probe accepts fragments, but the production stamp changes no mask pixels. Investigate production shader binding, channel writes, source/destination RT setup and pass state.";
            return "DIAGNOSIS: The low-level UV raster, projection gates and production GPU stamp all work in isolation. Investigate Painter Scene View event flow, active page/layer selection, preview binding and mask persistence.";
        }

        private static bool TryRenderProbe(
            Mesh rasterMesh,
            Matrix4x4 localToWorld,
            Vector3 brushCenter,
            Vector3 brushNormal,
            float radius,
            float depth,
            float cutoff,
            int size,
            out int coveragePixels,
            out int acceptedPixels,
            out string error)
        {
            coveragePixels = 0;
            acceptedPixels = 0;
            error = string.Empty;

            Shader shader = Shader.Find(ProbeShaderName);
            if (shader == null || !shader.isSupported)
            {
                error = "Could not find or use " + ProbeShaderName + ". Check Console shader compilation errors.";
                return false;
            }

            Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            try
            {
                rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "SurfaceWeaver GPU Probe RT",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (!rt.Create())
                {
                    error = "Could not allocate diagnostic RenderTexture.";
                    return false;
                }

                material.SetMatrix("_TargetLocalToWorld", localToWorld);
                material.SetMatrix("_TargetWorldNormalMatrix", localToWorld.inverse.transpose);
                material.SetVector("_BrushCenterWorld", brushCenter);
                material.SetVector("_BrushNormalWorld", brushNormal.normalized);
                material.SetFloat("_RadiusWorld", Mathf.Max(0.001f, radius));
                material.SetFloat("_ProjectionDepth", Mathf.Max(0.001f, depth));
                material.SetFloat("_NormalCutoff", Mathf.Clamp(cutoff, 0f, 0.99f));
                material.SetFloat("_FlipRasterY", SystemInfo.graphicsUVStartsAtTop ? 1f : 0f);

                if (!RenderPass(rt, rasterMesh, material, 0, out coveragePixels, out error))
                    return false;
                if (!RenderPass(rt, rasterMesh, material, 1, out acceptedPixels, out error))
                    return false;

                return true;
            }
            finally
            {
                if (rt != null)
                {
                    if (rt.IsCreated()) rt.Release();
                    DestroyObject(rt);
                }
                DestroyObject(material);
            }
        }

        private static bool RenderPass(
            RenderTexture targetTexture,
            Mesh rasterMesh,
            Material material,
            int pass,
            out int pixelCount,
            out string error)
        {
            pixelCount = 0;
            error = string.Empty;
            RenderTexture previousActive = RenderTexture.active;
            CommandBuffer cmd = new CommandBuffer { name = "SurfaceWeaver GPU Probe Pass " + pass };
            try
            {
                cmd.SetRenderTarget(targetTexture);
                cmd.SetViewport(new Rect(0f, 0f, targetTexture.width, targetTexture.height));
                cmd.ClearRenderTarget(false, true, Color.black);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(rasterMesh, Matrix4x4.identity, material, 0, pass);
                Graphics.ExecuteCommandBuffer(cmd);

                Texture2D readback = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false, true);
                try
                {
                    RenderTexture.active = targetTexture;
                    readback.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0, false);
                    readback.Apply(false, false);
                    pixelCount = CountChannelPixels(readback.GetPixels32(), 0, 8);
                }
                finally
                {
                    DestroyObject(readback);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                cmd.Release();
            }
        }

        private static int CountChannelPixels(Color32[] pixels, int channel, byte threshold)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = channel == 0 ? pixels[i].r : channel == 1 ? pixels[i].g : channel == 2 ? pixels[i].b : pixels[i].a;
                if (value > threshold) count++;
            }
            return count;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
