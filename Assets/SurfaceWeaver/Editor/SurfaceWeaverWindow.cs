using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>SurfaceWeaver authoring window with UV diagnostics and GPU mask painting.</summary>
    public sealed class SurfaceWeaverWindow : EditorWindow
    {
        private SurfacePaintTarget target;
        private SurfacePaintProfile profile;
        private MeshUvValidationReport lastReport;
        private Vector2 scroll;

        private bool scenePreviewEnabled = true;
        private bool paintingEnabled = true;
        private float brushRadius = 2f;
        private float brushOpacity = 0.25f;
        private float brushHardness = 0.2f;
        private float brushSpacing = 0.25f;
        private float brushRotation;
        private float brushScaleJitter;
        private float projectionDepthScale = 0.75f;
        // Off by default: the projection slab and radial falloff are the primary filters.
        // This avoids false rejection on imported meshes whose face normals differ from hit normals.
        private bool useSurfaceNormalFilter;
        private float surfaceNormalCutoff = 0f;
        private bool randomizeSplineStamps;
        private bool closeSplinePath;
        private int undoMemoryBudgetMb = 64;
        private Color previewColor = new Color(1f, 0.65f, 0.12f, 0.95f);
        private Texture2D directBrushAlpha;
        private SurfaceBrushPreset brushPreset;
        private int operationToolbar;
        private int selectedLayerIndex;
        private string maskPreviewProperty = "_PaintMask";
        private bool showSelectedChannelPreview = true;
        private Material maskChannelPreviewMaterial;
        private int cachedPreviewChannel = -1;

        // Live mask preview uses a temporary material instance instead of MaterialPropertyBlock.
        // This avoids renderer-wide overrides and keeps the original material asset untouched.
        private MeshRenderer previewRenderer;
        private Material previewOriginalMaterial;
        private Material previewMaterialInstance;
        private int previewMaterialIndex = -1;

        private bool hasSceneHit;
        private Vector2 lastHitUv;
        private Vector3 lastHitPoint;
        private Vector3 lastHitNormal;
        private int lastHitTriangle;
        private string sceneHitStatus = "Move the pointer over the selected target in Scene View.";

        private SurfaceGpuBrushEngine gpuEngine;
        private SurfacePaintProfile activeSessionProfile;
        private Texture2D activeMaskAsset;
        private int activeMaskPage = -1;
        private bool isPainting;
        private int paintingControlId;
        private bool hasPreviousPaintHit;
        private SurfaceSceneHit previousPaintHit;
        private bool hasUnsavedChanges;
        private bool hasUnsavedChangesBeforeStroke;
        private string paintStatus = "Create or load a mask page to begin painting.";

        private SurfaceUvRasterMesh uvRasterMesh;
        private Mesh uvRasterSourceMesh;
        private bool pathAuthoringEnabled;
        private bool seamAwareProjection = true;
        private SurfacePaintPathAsset pathAsset;
        private readonly List<SurfacePaintPathPoint> pathPoints = new List<SurfacePaintPathPoint>();
        private string pathStatus = "Click the target mesh in Scene View to add spline anchors.";
        private int pathRandomSeed = 1;

        [MenuItem("Window/SurfaceWeaver/Painter", false, 2100)]
        public static void OpenWindow()
        {
            SurfaceWeaverWindow window = GetWindow<SurfaceWeaverWindow>();
            window.titleContent = new GUIContent("SurfaceWeaver");
            window.minSize = new Vector2(390f, 600f);
            window.Show();
        }

        public static void OpenFor(SurfacePaintTarget selectedTarget)
        {
            OpenWindow();
            SurfaceWeaverWindow window = GetWindow<SurfaceWeaverWindow>();
            window.SetTarget(selectedTarget);
            window.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            if (Selection.activeGameObject != null)
            {
                SurfacePaintTarget selected =
                    Selection.activeGameObject.GetComponent<SurfacePaintTarget>();
                if (selected != null)
                    SetTarget(selected);
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            StopStroke();
            RestorePreviewMaterial();
            DisposePaintSession();
            DisposeUvRasterMesh();
            if (maskChannelPreviewMaterial != null)
            {
                DestroyImmediate(maskChannelPreviewMaterial);
                maskChannelPreviewMaterial = null;
                cachedPreviewChannel = -1;
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject == null)
                return;

            SurfacePaintTarget selected =
                Selection.activeGameObject.GetComponent<SurfacePaintTarget>();
            if (selected != null && selected != target)
            {
                SetTarget(selected);
                Repaint();
            }
        }

        private void SetTarget(SurfacePaintTarget value)
        {
            if (target == value)
                return;

            StopStroke();
            RestorePreviewMaterial();
            DisposePaintSession();
            DisposeUvRasterMesh();
            pathPoints.Clear();
            pathAsset = null;
            pathAuthoringEnabled = false;
            target = value;
            profile = target != null ? target.Profile : null;
            selectedLayerIndex = 0;
            lastReport = null;
            hasSceneHit = false;
            sceneHitStatus = "Move the pointer over the selected target in Scene View.";
            paintStatus = "Create or load a mask page to begin painting.";
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawTargetSection();
            EditorGUILayout.Space(8f);
            DrawScenePreviewSection();
            EditorGUILayout.Space(8f);
            DrawProfileSection();
            EditorGUILayout.Space(8f);
            DrawPaintingSection();
            EditorGUILayout.Space(8f);
            DrawSplinePathSection();
            EditorGUILayout.Space(8f);
            DrawDiagnosticsSection();
            EditorGUILayout.Space(8f);
            DrawRoadmapSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("SURFACEWEAVER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Custom-mesh surface authoring for URP",
                EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Custom-mesh surface authoring with seam-aware UV raster painting, packed RGBA masks, and reusable spline path assets. " +
                "Mask changes are saved as PNG assets referenced by the SurfacePaintProfile.",
                MessageType.Info);
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target Mesh", EditorStyles.boldLabel);
            SurfacePaintTarget newTarget = (SurfacePaintTarget)EditorGUILayout.ObjectField(
                "Paint Target", target, typeof(SurfacePaintTarget), true);
            if (newTarget != target)
                SetTarget(newTarget);

            using (new EditorGUI.DisabledScope(target != null || Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Add Target Component to Selected Mesh"))
                    AddTargetToSelection();
            }

            if (target == null)
                return;

            if (!target.TryGetMesh(out Mesh mesh))
            {
                EditorGUILayout.HelpBox(
                    "The selected target does not have a valid MeshFilter and shared mesh.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Mesh", mesh.name);
            EditorGUILayout.LabelField("Vertices", mesh.vertexCount.ToString());
            EditorGUILayout.LabelField("Submeshes", mesh.subMeshCount.ToString());
            EditorGUILayout.LabelField("UV Channel", "UV" + target.UvChannel);

            if (GUILayout.Button("Select Target GameObject"))
                Selection.activeGameObject = target.gameObject;
        }

        private void AddTargetToSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return;

            if (selected.GetComponent<MeshFilter>() == null ||
                selected.GetComponent<MeshRenderer>() == null)
            {
                EditorUtility.DisplayDialog(
                    "SurfaceWeaver",
                    "Select a GameObject with both MeshFilter and MeshRenderer components.",
                    "OK");
                return;
            }

            SurfacePaintTarget added = Undo.AddComponent<SurfacePaintTarget>(selected);
            SetTarget(added);
            EditorGUIUtility.PingObject(added);
        }

        private void DrawScenePreviewSection()
        {
            EditorGUILayout.LabelField("Scene View Brush Preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            scenePreviewEnabled = EditorGUILayout.ToggleLeft(
                "Enable Scene View Preview", scenePreviewEnabled);
            using (new EditorGUI.DisabledScope(!scenePreviewEnabled))
            {
                brushRadius = EditorGUILayout.Slider(
                    "Brush Radius (World Units)", brushRadius, 0.05f, 20f);
                previewColor = EditorGUILayout.ColorField("Preview Color", previewColor);
            }
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Add / Refresh MeshCollider"))
                    AddOrRefreshMeshCollider();
            }

            if (target == null)
            {
                EditorGUILayout.HelpBox("Assign a SurfacePaintTarget to enable Scene View picking.", MessageType.Info);
                return;
            }

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                EditorGUILayout.HelpBox(
                    "A non-convex MeshCollider is required for UV picking. Add or refresh it before painting.",
                    MessageType.Warning);
            }
            else if (target.TryGetMesh(out Mesh mesh) && meshCollider.sharedMesh != mesh)
            {
                EditorGUILayout.HelpBox("The MeshCollider references a different mesh. Refresh it before painting.", MessageType.Warning);
            }
            else if (meshCollider.convex)
            {
                EditorGUILayout.HelpBox("Disable Convex on the target MeshCollider.", MessageType.Warning);
            }

            if (target.UvChannel != 0)
            {
                EditorGUILayout.HelpBox(
                    "This release paints UV0. Set the target UV Channel to 0.",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField("Picking Status", sceneHitStatus);
            if (hasSceneHit)
            {
                EditorGUILayout.LabelField("UV0", $"({lastHitUv.x:0.0000}, {lastHitUv.y:0.0000})");
                EditorGUILayout.LabelField("World Position", lastHitPoint.ToString("F3"));
                EditorGUILayout.LabelField("World Normal", lastHitNormal.ToString("F3"));
                EditorGUILayout.LabelField("Triangle Index", lastHitTriangle.ToString());
            }
        }

        private void AddOrRefreshMeshCollider()
        {
            if (target == null || !target.TryGetMesh(out Mesh mesh))
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "The target does not have a valid MeshFilter mesh.", "OK");
                return;
            }

            if (!mesh.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "SurfaceWeaver",
                    "Enable Read/Write in the mesh Model Import Settings, then reimport the model.",
                    "OK");
                return;
            }

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = Undo.AddComponent<MeshCollider>(target.gameObject);

            Undo.RecordObject(meshCollider, "Configure SurfaceWeaver MeshCollider");
            meshCollider.convex = false;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            EditorUtility.SetDirty(meshCollider);
            Physics.SyncTransforms();
            SurfaceUvMetric.ClearCache();
            hasSceneHit = false;
            sceneHitStatus = "MeshCollider refreshed. Move the pointer over the target in Scene View.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawProfileSection()
        {
            EditorGUILayout.LabelField("Surface Profile", EditorStyles.boldLabel);
            SurfacePaintProfile newProfile = (SurfacePaintProfile)EditorGUILayout.ObjectField(
                "Profile", profile, typeof(SurfacePaintProfile), false);
            if (newProfile != profile)
            {
                RestorePreviewMaterial();
                DisposePaintSession();
                profile = newProfile;
                selectedLayerIndex = 0;
                if (profile != null && target != null && target.Profile != profile)
                    AssignProfile();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Profile"))
                    CreateProfile();

                using (new EditorGUI.DisabledScope(target == null || profile == null))
                {
                    if (GUILayout.Button("Assign to Target"))
                        AssignProfile();
                }
            }

            if (profile == null)
                return;

            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("baseAlbedo"), new GUIContent("Base Albedo"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("baseTint"), new GUIContent("Base Tint"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("baseTiling"), new GUIContent("Base Tiling"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("maskStorageMode"), new GUIContent("Mask Storage"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("maskResolution"), new GUIContent("Mask Resolution"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("maskPages"), new GUIContent("Mask Pages"), true);
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("paintLayers"), new GUIContent("Paint Layers"), true);

            if (serializedProfile.ApplyModifiedProperties())
                EditorUtility.SetDirty(profile);

            if (GUILayout.Button("Add Paint Layer"))
            {
                Undo.RecordObject(profile, "Add Surface Layer");
                profile.AddLayer("Surface " + (profile.PaintLayers.Count + 1));
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.HelpBox(
                "Packed RGBA pages store four independent grayscale layer masks (R, G, B and A). " +
                "New layers are assigned to the next channel automatically.",
                MessageType.None);
        }

        private void DrawPaintingSection()
        {
            EditorGUILayout.LabelField("GPU Painting", EditorStyles.boldLabel);

            if (target == null || profile == null)
            {
                EditorGUILayout.HelpBox("Assign a target mesh and a SurfacePaintProfile to enable painting.", MessageType.Info);
                return;
            }

            if (target.Profile != profile)
            {
                EditorGUILayout.HelpBox("The selected profile is not assigned to the target. Click Assign to Target above.", MessageType.Warning);
                return;
            }

            if (profile.MaskStorageMode != SurfaceMaskStorageMode.PackedRgbaPages)
            {
                EditorGUILayout.HelpBox(
                    "This GPU painting milestone supports Packed RGBA Pages. Change Mask Storage to Packed Rgba Pages.",
                    MessageType.Warning);
                return;
            }

            if (profile.PaintLayers == null || profile.PaintLayers.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one painted surface layer in the profile section above.", MessageType.Info);
                return;
            }

            string[] layerNames = new string[profile.PaintLayers.Count];
            for (int i = 0; i < layerNames.Length; i++)
            {
                SurfaceLayerDefinition item = profile.PaintLayers[i];
                layerNames[i] = item != null ? item.DisplayName : "Missing Layer " + i;
            }
            selectedLayerIndex = Mathf.Clamp(selectedLayerIndex, 0, layerNames.Length - 1);
            selectedLayerIndex = EditorGUILayout.Popup("Paint Layer", selectedLayerIndex, layerNames);

            SurfaceLayerDefinition layer = profile.PaintLayers[selectedLayerIndex];
            if (layer == null)
            {
                EditorGUILayout.HelpBox("The selected layer definition is missing.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Mask Page", layer.MaskPageIndex.ToString());
            EditorGUILayout.LabelField("Mask Channel", layer.MaskChannel.ToString());
            if (!layer.Enabled)
                EditorGUILayout.HelpBox("This surface layer is disabled in the profile.", MessageType.Warning);
            if (layer.MaskChannel != SurfaceMaskChannel.Red)
            {
                EditorGUILayout.HelpBox(
                    "The current one-channel preview shader must sample the selected channel to display this layer. " +
                    "The GPU painter preserves and writes the selected RGBA channel correctly; profile-driven multi-layer rendering is a later milestone.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(5f);
            EditorGUI.BeginChangeCheck();
            SurfaceBrushPreset newBrushPreset = (SurfaceBrushPreset)EditorGUILayout.ObjectField(
                "Brush Preset", brushPreset, typeof(SurfaceBrushPreset), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Starter Brush Library"))
                {
                    SurfaceBrushPreset createdPreset = SurfaceBrushPresetFactory.CreateStarterBrushLibrary();
                    if (createdPreset != null)
                        newBrushPreset = createdPreset;
                }
                if (GUILayout.Button("Select Brush Asset"))
                {
                    string selectedPath = EditorUtility.OpenFilePanel(
                        "Select SurfaceWeaver Brush Preset", Application.dataPath, "asset");
                    if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                    {
                        string assetPath = "Assets" + selectedPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                        SurfaceBrushPreset selectedBrush = AssetDatabase.LoadAssetAtPath<SurfaceBrushPreset>(assetPath);
                        if (selectedBrush != null)
                            newBrushPreset = selectedBrush;
                    }
                }
            }
            directBrushAlpha = (Texture2D)EditorGUILayout.ObjectField(
                "Brush Alpha Override", directBrushAlpha, typeof(Texture2D), false);
            seamAwareProjection = EditorGUILayout.ToggleLeft(
                "Seam-Aware Mesh Projection (recommended)", seamAwareProjection);
            operationToolbar = GUILayout.Toolbar(operationToolbar, new[] { "Paint", "Erase", "Smooth" });
            paintingEnabled = EditorGUILayout.ToggleLeft("Enable Scene View Painting", paintingEnabled);
            brushRadius = EditorGUILayout.Slider("Radius (World Units)", brushRadius, 0.05f, 20f);
            brushOpacity = EditorGUILayout.Slider("Opacity", brushOpacity, 0.01f, 1f);
            brushHardness = EditorGUILayout.Slider("Hardness", brushHardness, 0f, 1f);
            brushSpacing = EditorGUILayout.Slider("Spacing (Diameter)", brushSpacing, 0.05f, 1f);
            brushRotation = EditorGUILayout.Slider("Brush Rotation", brushRotation, 0f, 360f);
            brushScaleJitter = EditorGUILayout.Slider("Scale Randomness", brushScaleJitter, 0f, 0.75f);
            if (seamAwareProjection)
            {
                projectionDepthScale = EditorGUILayout.Slider(
                    "Projection Depth (x Radius)", projectionDepthScale, 0.05f, 2f);
                useSurfaceNormalFilter = EditorGUILayout.ToggleLeft(
                    "Filter by Surface Normal (optional)", useSurfaceNormalFilter);
                if (useSurfaceNormalFilter)
                {
                    surfaceNormalCutoff = EditorGUILayout.Slider(
                        "Normal Dot Cutoff", surfaceNormalCutoff, -1f, 1f);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Normal filtering is disabled. Projection depth and brush radius still constrain stamps. " +
                        "Enable this only when you need to reject nearby surfaces with different facing directions.",
                        MessageType.None);
                }
            }
            ResolveMaskPreviewPropertyForCurrentTarget();
            maskPreviewProperty = EditorGUILayout.TextField("Preview Shader Property", maskPreviewProperty);
            bool brushFieldsChanged = EditorGUI.EndChangeCheck();
            if (newBrushPreset != brushPreset)
            {
                brushPreset = newBrushPreset;
                if (brushPreset != null)
                {
                    SurfaceBrushSettings defaults = brushPreset.Defaults;
                    brushRadius = Mathf.Max(0.05f, defaults.RadiusWorldUnits);
                    brushOpacity = defaults.Opacity;
                    brushHardness = defaults.Hardness;
                    brushSpacing = Mathf.Clamp(defaults.Spacing, 0.05f, 1f);
                    brushScaleJitter = brushPreset.ScaleJitter;
                }
            }
            if (brushFieldsChanged)
                SceneView.RepaintAll();

            undoMemoryBudgetMb = EditorGUILayout.IntSlider(
                "Undo Memory Budget (MB)", undoMemoryBudgetMb, 16, 256);
            if (gpuEngine != null && gpuEngine.IsInitialized)
                gpuEngine.SetHistoryBudgetMegabytes(undoMemoryBudgetMb);

            if (profile.MaskResolution >= 3072)
            {
                long memoryBytes = (long)profile.MaskResolution * profile.MaskResolution * 4L * 2L;
                EditorGUILayout.HelpBox(
                    "Large mask pages use substantial GPU memory. Two RGBA32 working buffers at this resolution require approximately " +
                    FormatBytes(memoryBytes) + ". Use 1024 or 2048 while authoring unless you specifically need 4096.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Optional brush alpha uses the texture's red channel: white paints and black leaves the mask unchanged.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                seamAwareProjection
                    ? "Seam-aware mode rasterizes target triangles in UV0 space and evaluates the brush in world space. Duplicated UV seam charts receive the same stamp. Raster Y is platform-adjusted while source-mask sampling retains logical UV0. Normal filtering is optional; projection depth remains active."
                    : "Legacy UV-stamp mode is enabled. It is retained as a fallback, but strokes stop at UV discontinuities and may leave gaps across seams.",
                MessageType.Info);
            if (seamAwareProjection && operationToolbar == 2)
            {
                EditorGUILayout.HelpBox(
                    "Smooth currently averages neighboring UV pixels and is not island-aware. Avoid smoothing directly beside tightly packed UV chart boundaries until island padding/dilation is added.",
                    MessageType.Warning);
            }

            Texture2D pageAsset = profile.GetMaskPage(layer.MaskPageIndex);
            if (pageAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "No mask asset exists for this page yet. Create one to initialize the GPU painting session.",
                    MessageType.Info);
                if (GUILayout.Button("Create Black Mask Page (" + profile.MaskResolution + " x " + profile.MaskResolution + ")"))
                    CreateMaskPage(layer.MaskPageIndex);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load / Reload Mask Page"))
                    LoadMaskPage(layer.MaskPageIndex);
                using (new EditorGUI.DisabledScope(!IsSessionForLayer(layer)))
                {
                    if (GUILayout.Button("Save Mask PNG"))
                        SaveMaskPage();
                }
            }

            if (!IsSessionForLayer(layer))
            {
                EditorGUILayout.HelpBox("Load the selected layer's mask page before painting. A different page may currently be active.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("GPU Buffer", gpuEngine.Width + " x " + gpuEngine.Height + " (RGBA32, Linear)");
                long estimatedBytes = (long)gpuEngine.Width * gpuEngine.Height * 4L * 2L;
                EditorGUILayout.LabelField("Two-buffer GPU memory", FormatBytes(estimatedBytes));
                EditorGUILayout.LabelField("Session Status", hasUnsavedChanges ? "Unsaved changes" : "Saved / unchanged");
                showSelectedChannelPreview = EditorGUILayout.ToggleLeft(
                    "Preview selected mask channel only (grayscale)", showSelectedChannelPreview);
                Rect previewRect = GUILayoutUtility.GetRect(128f, 128f, GUILayout.ExpandWidth(false));
                Material previewMaterial = showSelectedChannelPreview
                    ? GetMaskChannelPreviewMaterial((int)layer.MaskChannel)
                    : null;
                EditorGUI.DrawPreviewTexture(
                    previewRect, gpuEngine.Current, previewMaterial, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField(
                    showSelectedChannelPreview
                        ? "Preview: " + layer.MaskChannel + " channel (grayscale)"
                        : "Preview: combined RGBA channels",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!gpuEngine.CanUndo || isPainting))
                    {
                        if (GUILayout.Button("Undo"))
                            UndoPaintStroke();
                    }
                    using (new EditorGUI.DisabledScope(!gpuEngine.CanRedo || isPainting))
                    {
                        if (GUILayout.Button("Redo"))
                            RedoPaintStroke();
                    }
                    GUILayout.Label("Undo " + gpuEngine.UndoCount + " / Redo " + gpuEngine.RedoCount,
                        GUILayout.Width(112f));
                }
                EditorGUILayout.LabelField("History memory", FormatBytes(gpuEngine.HistoryMemoryBytes));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fill Selected Layer") &&
                        EditorUtility.DisplayDialog("Fill Layer Mask",
                            "Fill only the selected layer channel with white? Other channels will be preserved. This can be undone.",
                            "Fill", "Cancel"))
                    {
                        ApplyChannelFill(layer, 1f, "Filled selected layer channel.");
                    }

                    if (GUILayout.Button("Clear Selected Layer Channel") &&
                        EditorUtility.DisplayDialog("Clear Layer Mask",
                            "Clear only the selected layer channel? Other channels on this page will be preserved. This can be undone.",
                            "Clear", "Cancel"))
                    {
                        ApplyChannelFill(layer, 0f, "Cleared selected layer channel.");
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Hold the left mouse button over the target in Scene View to paint. Hold Alt for normal Scene View navigation. " +
                "Paint writes white, Erase writes black, and Smooth averages the selected channel locally. " +
                "Fill/Clear affect the entire selected channel. Ctrl/Cmd+Z undoes and Ctrl/Cmd+Y (or Shift+Z) redoes. Save Mask PNG to persist changes.",
                MessageType.Info);
            EditorGUILayout.LabelField("Painting Status", paintStatus);

            bool hasPreviewProperty = RendererHasProperty(target, maskPreviewProperty);
            if (!hasPreviewProperty)
            {
                EditorGUILayout.HelpBox(
                    "The target renderer's materials do not expose '" + maskPreviewProperty + "'. " +
                    "Painting and saving will still work, but live surface preview needs a matching texture property in your shader.",
                    MessageType.Warning);
            }
        }

        private void DrawSplinePathSection()
        {
            EditorGUILayout.LabelField("Spline Path Painting", EditorStyles.boldLabel);
            bool newPathMode = EditorGUILayout.ToggleLeft(
                "Enable Path Authoring (click the mesh to add anchors)", pathAuthoringEnabled);
            if (newPathMode != pathAuthoringEnabled)
            {
                StopStroke();
                pathAuthoringEnabled = newPathMode;
                hasPreviousPaintHit = false;
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Path Anchors", pathPoints.Count.ToString());
            closeSplinePath = EditorGUILayout.ToggleLeft("Close Path (connect last anchor to first)", closeSplinePath);
            randomizeSplineStamps = EditorGUILayout.ToggleLeft(
                "Randomize brush rotation / size per path stamp", randomizeSplineStamps);
            if (uvRasterMesh != null)
            {
                EditorGUILayout.LabelField("Seam-Aware Raster Triangles",
                    uvRasterMesh.SourceTriangleCount + " source / " +
                    (uvRasterMesh.SourceTriangleCount - uvRasterMesh.SkippedDegenerateTriangleCount) + " rasterized");
            }
            pathAsset = (SurfacePaintPathAsset)EditorGUILayout.ObjectField(
                "Path Asset", pathAsset, typeof(SurfacePaintPathAsset), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(pathPoints.Count == 0))
                {
                    if (GUILayout.Button("Clear Anchors"))
                    {
                        pathPoints.Clear();
                        pathRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
                        pathStatus = "Path anchors cleared.";
                        SceneView.RepaintAll();
                    }
                }
                using (new EditorGUI.DisabledScope(pathAsset == null))
                {
                    if (GUILayout.Button("Load Asset Points"))
                        LoadPathAssetPoints();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(pathPoints.Count < 2 || target == null))
                {
                    if (GUILayout.Button("Save Path Asset"))
                        SavePathAsset();
                    if (GUILayout.Button("Apply Spline"))
                        ApplySplinePath();
                }
            }

            EditorGUILayout.LabelField("Path Status", pathStatus);
            EditorGUILayout.HelpBox(
                "Enable Path Authoring and click the target mesh to add anchors. Apply works in both modes: seam-aware projection paints matching UV charts; legacy mode paints only the hit UV chart and may show gaps at seams. " +
                "Paths are open unless Close Path is enabled. Per-stamp randomization is off by default to keep path texture orientation stable.",
                MessageType.Info);
        }

        private void AddPathAnchor(SurfaceSceneHit hit)
        {
            Matrix4x4 localToWorld = target.transform.localToWorldMatrix;
            if (pathPoints.Count == 0)
                pathRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);

            Vector3 localPosition = target.transform.InverseTransformPoint(hit.Point);
            Vector3 localNormal = localToWorld.transpose.MultiplyVector(hit.Normal).normalized;
            pathPoints.Add(new SurfacePaintPathPoint(localPosition, localNormal));
            pathStatus = "Added anchor " + pathPoints.Count + ". Add more points or apply the spline.";
            SceneView.RepaintAll();
            Repaint();
        }

        private void SavePathAsset()
        {
            if (target == null || pathPoints.Count < 2)
                return;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (layer == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "Select a valid paint layer before saving this path.", "OK");
                return;
            }

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Save SurfaceWeaver Path", "SurfacePaintPath", "asset",
                "Choose a folder inside Assets for this reusable path asset.");
            if (string.IsNullOrEmpty(assetPath))
                return;

            GlobalObjectId targetGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (targetGlobalId.identifierType == 2 &&
                targetGlobalId.assetGUID.ToString() == "00000000000000000000000000000000")
            {
                EditorUtility.DisplayDialog(
                    "Save Scene First",
                    "Save the scene at least once before creating a reusable path asset. Unity uses the saved scene identity to resolve this target later.",
                    "OK");
                return;
            }

            SurfacePaintPathAsset created = CreateInstance<SurfacePaintPathAsset>();
            created.Configure(
                targetGlobalId.ToString(),
                target.name,
                pathPoints,
                layer.Id,
                layer.MaskPageIndex,
                layer.MaskChannel,
                brushRadius,
                brushOpacity,
                brushHardness,
                brushSpacing,
                operationToolbar == 1,
                operationToolbar == 2,
                brushPreset,
                directBrushAlpha,
                brushRotation,
                brushScaleJitter,
                closeSplinePath,
                randomizeSplineStamps,
                pathRandomSeed);

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssets();
            pathAsset = created;
            EditorGUIUtility.PingObject(created);
            pathStatus = "Path asset saved. Points are stored in target-local space.";
        }

        private void LoadPathAssetPoints()
        {
            SurfacePaintPathAsset source = pathAsset;
            if (source == null)
                return;

            SurfacePaintTarget resolvedTarget = null;
            if (!string.IsNullOrEmpty(source.TargetGlobalObjectId) &&
                GlobalObjectId.TryParse(source.TargetGlobalObjectId, out GlobalObjectId globalId))
            {
                resolvedTarget = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as SurfacePaintTarget;
            }

            if (resolvedTarget == null)
            {
                if (target == null)
                {
                    EditorUtility.DisplayDialog(
                        "SurfaceWeaver",
                        "The original scene target could not be resolved. Open the scene containing '" +
                        source.TargetDisplayName + "' and select its SurfacePaintTarget, then load the path asset again.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog(
                    "Resolve Path Target",
                    "The original scene target could not be resolved. Load these path points onto the currently selected target '" +
                    target.name + "'? Points are stored in target-local space, so only do this when the meshes have matching layout and scale.",
                    "Use Selected Target", "Cancel"))
                    return;

                resolvedTarget = target;
            }

            if (target != resolvedTarget)
                SetTarget(resolvedTarget);

            pathAsset = source;
            pathPoints.Clear();
            for (int i = 0; i < source.Points.Count; i++)
                pathPoints.Add(source.Points[i]);

            bool matchedLayer = false;
            bool savedLayerMappingMatches = false;
            if (profile != null && profile.PaintLayers != null)
            {
                for (int i = 0; i < profile.PaintLayers.Count; i++)
                {
                    SurfaceLayerDefinition candidate = profile.PaintLayers[i];
                    if (candidate != null && candidate.Id == source.LayerId)
                    {
                        selectedLayerIndex = i;
                        matchedLayer = true;
                        savedLayerMappingMatches = candidate.MaskPageIndex == source.MaskPageIndex &&
                            candidate.MaskChannel == source.MaskChannel;
                        break;
                    }
                }
            }

            brushRadius = source.BrushRadius;
            brushOpacity = source.Opacity;
            brushHardness = source.Hardness;
            brushSpacing = source.Spacing;
            brushPreset = source.BrushPreset;
            directBrushAlpha = source.BrushAlphaOverride;
            brushRotation = source.BrushRotationDegrees;
            brushScaleJitter = source.ScaleJitter;
            closeSplinePath = source.ClosedLoop;
            randomizeSplineStamps = source.RandomizePerStamp;
            pathRandomSeed = source.RandomSeed;
            operationToolbar = source.Smooth ? 2 : (source.Erase ? 1 : 0);
            pathAuthoringEnabled = true;
            pathStatus = !matchedLayer
                ? "Loaded path points, but its saved layer ID was not found in this profile. Select the intended layer before applying."
                : savedLayerMappingMatches
                    ? "Loaded " + pathPoints.Count + " anchors and matching layer mapping from path asset."
                    : "Loaded path points. The saved layer page/channel differs from the current profile; verify the selected layer and mask page before applying.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void ApplySplinePath()
        {
            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (target == null || layer == null || !layer.Enabled ||
                !IsSessionForLayer(layer) || pathPoints.Count < 2)
            {
                pathStatus = "Assign a target, select an enabled layer, load its mask page, and add at least two anchors.";
                Repaint();
                return;
            }

            if (seamAwareProjection && !EnsureUvRasterMesh(out string rasterError))
            {
                pathStatus = rasterError;
                Repaint();
                return;
            }

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null || collider.convex || collider.sharedMesh == null)
            {
                pathStatus = "A non-convex MeshCollider using the target mesh is required to apply spline paths.";
                Repaint();
                return;
            }

            StopStroke();
            gpuEngine.SetHistoryBudgetMegabytes(undoMemoryBudgetMb);
            hasUnsavedChangesBeforeStroke = hasUnsavedChanges;
            if (!gpuEngine.BeginStroke(out string beginError))
            {
                pathStatus = beginError;
                Repaint();
                return;
            }

            bool anyStamp = false;
            bool failed = false;
            string failure = string.Empty;
            float minimumJitteredRadius = brushRadius * (1f - (randomizeSplineStamps ? brushScaleJitter : 0f));
            float spacingDistance = Mathf.Max(0.025f, minimumJitteredRadius * 2f * brushSpacing);
            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(pathRandomSeed);

            int segmentCount = closeSplinePath ? pathPoints.Count : pathPoints.Count - 1;
            try
            {
                for (int segment = 0; segment < segmentCount && !failed; segment++)
                {
                    int next = (segment + 1) % pathPoints.Count;
                    Vector3 p1 = pathPoints[segment].LocalPosition;
                    Vector3 p2 = pathPoints[next].LocalPosition;
                    float worldSegmentLength = Vector3.Distance(
                        target.transform.TransformPoint(p1),
                        target.transform.TransformPoint(p2));
                    int steps = Mathf.Clamp(
                        Mathf.CeilToInt(worldSegmentLength / spacingDistance * 1.5f), 1, 4096);

                    for (int step = segment == 0 ? 0 : 1; step <= steps; step++)
                    {
                        float t = step / (float)steps;
                        Vector3 localPosition = EvaluatePathPosition(segment, t, closeSplinePath);
                        Vector3 localNormal = EvaluatePathNormal(segment, t, closeSplinePath);
                        Vector3 worldPosition = target.transform.TransformPoint(localPosition);
                        Vector3 worldNormal = LocalNormalToWorld(localNormal);

                        if (!TryProjectPathHit(collider, worldPosition, worldNormal, out RaycastHit projectedHit))
                            continue;

                        bool stampSucceeded;
                        if (seamAwareProjection)
                        {
                            stampSucceeded = StampSurfaceWorld(
                                projectedHit.point, projectedHit.normal, layer,
                                operationToolbar == 1, operationToolbar == 2, GetActiveBrushAlpha(),
                                randomizeSplineStamps, out failure);
                        }
                        else
                        {
                            stampSucceeded = StampLegacyPathHit(
                                projectedHit, collider, layer, operationToolbar == 1,
                                operationToolbar == 2, GetActiveBrushAlpha(),
                                randomizeSplineStamps, out failure);
                        }

                        if (!stampSucceeded)
                        {
                            failed = true;
                            break;
                        }
                        anyStamp = true;
                    }
                }
            }
            catch (Exception exception)
            {
                failed = true;
                failure = "Spline application failed: " + exception.Message;
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
            }

            if (failed || !anyStamp)
            {
                gpuEngine.CancelStroke(out string cancelError);
                pathStatus = failed ? failure :
                    "No spline samples could be projected. Check that the anchors lie on the collider and the path does not cross a gap.";
                if (!string.IsNullOrEmpty(cancelError))
                    pathStatus += " Undo restore: " + cancelError;
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            gpuEngine.EndStroke();
            hasUnsavedChanges = true;
            BindWorkingMaskPreview();
            pathStatus = seamAwareProjection
                ? "Spline applied using seam-aware world projection as one Undo step. Save the mask page to persist the result."
                : "Spline applied using legacy UV projection as one Undo step. UV seam gaps are possible. Save the mask page to persist the result.";
            paintStatus = pathStatus;
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawSplinePathPreview(bool hasCurrentHit, Vector3 currentHitPoint)
        {
            if (Event.current.type != EventType.Repaint || target == null)
                return;

            if (pathPoints.Count >= 2)
            {
                List<Vector3> line = new List<Vector3>();
                int previewSegments = closeSplinePath ? pathPoints.Count : pathPoints.Count - 1;
                for (int segment = 0; segment < previewSegments; segment++)
                {
                    int sampleCount = 12;
                    for (int sample = segment == 0 ? 0 : 1; sample <= sampleCount; sample++)
                    {
                        Vector3 local = EvaluatePathPosition(segment, sample / (float)sampleCount, closeSplinePath);
                        line.Add(target.transform.TransformPoint(local));
                    }
                }
                Handles.color = new Color(1f, 0.68f, 0.12f, 0.95f);
                if (line.Count >= 2)
                    Handles.DrawAAPolyLine(3f, line.ToArray());
            }

            for (int i = 0; i < pathPoints.Count; i++)
            {
                Vector3 world = target.transform.TransformPoint(pathPoints[i].LocalPosition);
                Handles.color = Color.white;
                Handles.DotHandleCap(0, world, Quaternion.identity, HandleUtility.GetHandleSize(world) * 0.08f, EventType.Repaint);
                Handles.Label(world, "  P" + (i + 1));
            }

            if (pathAuthoringEnabled && hasCurrentHit && pathPoints.Count > 0)
            {
                Vector3 last = target.transform.TransformPoint(pathPoints[pathPoints.Count - 1].LocalPosition);
                Handles.color = new Color(0.3f, 0.85f, 1f, 0.8f);
                Handles.DrawAAPolyLine(2f, last, currentHitPoint);
            }
        }

        private Vector3 EvaluatePathPosition(int segment, float t, bool closed)
        {
            int count = pathPoints.Count;
            int i0 = closed ? (segment - 1 + count) % count : Mathf.Max(0, segment - 1);
            int i1 = segment;
            int i2 = closed ? (segment + 1) % count : Mathf.Min(count - 1, segment + 1);
            int i3 = closed ? (segment + 2) % count : Mathf.Min(count - 1, segment + 2);
            return CatmullRom(
                pathPoints[i0].LocalPosition, pathPoints[i1].LocalPosition,
                pathPoints[i2].LocalPosition, pathPoints[i3].LocalPosition, t);
        }

        private Vector3 EvaluatePathNormal(int segment, float t, bool closed)
        {
            int next = closed ? (segment + 1) % pathPoints.Count : segment + 1;
            Vector3 n1 = pathPoints[segment].LocalNormal;
            Vector3 n2 = pathPoints[next].LocalNormal;
            Vector3 normal = Vector3.Slerp(n1, n2, t);
            return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 LocalNormalToWorld(Vector3 localNormal)
        {
            if (target == null)
                return Vector3.up;
            Vector3 world = target.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(localNormal);
            return world.sqrMagnitude > 0.000001f ? world.normalized : Vector3.up;
        }

        private bool TryProjectPathPoint(
            MeshCollider collider,
            Vector3 approximatePoint,
            Vector3 normal,
            out Vector3 projectedPoint,
            out Vector3 projectedNormal)
        {
            projectedPoint = approximatePoint;
            projectedNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            if (!TryProjectPathHit(collider, approximatePoint, normal, out RaycastHit hit))
                return false;
            projectedPoint = hit.point;
            projectedNormal = hit.normal.sqrMagnitude > 0.000001f ? hit.normal.normalized : projectedNormal;
            return true;
        }

        private bool TryProjectPathHit(
            MeshCollider collider,
            Vector3 approximatePoint,
            Vector3 normal,
            out RaycastHit projectedHit)
        {
            projectedHit = default;
            if (collider == null)
                return false;

            if (normal.sqrMagnitude < 0.000001f)
                normal = Vector3.up;
            normal.Normalize();

            // Keep the snap distance local to the path sample. The previous 3x-radius
            // ray could jump to another nearby surface and then project the brush there.
            float maxSnapDistance = Mathf.Max(0.15f, brushRadius * 0.75f);
            Ray ray = new Ray(approximatePoint + normal * maxSnapDistance, -normal);
            if (!collider.Raycast(ray, out RaycastHit hit, maxSnapDistance * 2f))
                return false;

            float distance = Vector3.Distance(hit.point, approximatePoint);
            float facing = Vector3.Dot(hit.normal.normalized, normal);
            if (distance > maxSnapDistance * 1.5f || facing < 0.1f)
                return false;

            projectedHit = hit;
            return true;
        }

        private bool EnsureUvRasterMesh(out string error)
        {
            error = string.Empty;
            if (target == null || !target.TryGetMesh(out Mesh sourceMesh))
            {
                error = "Select a valid readable MeshFilter target before painting.";
                return false;
            }

            if (uvRasterMesh != null && uvRasterSourceMesh == sourceMesh && uvRasterMesh.Mesh != null)
                return true;

            DisposeUvRasterMesh();
            if (!SurfaceUvRasterMesh.TryBuild(sourceMesh, out uvRasterMesh, out error))
                return false;

            uvRasterSourceMesh = sourceMesh;
            if (uvRasterMesh.SkippedDegenerateTriangleCount > 0 ||
                uvRasterMesh.SkippedNonTriangleSubmeshCount > 0)
            {
                paintStatus = "UV raster mesh skipped " + uvRasterMesh.SkippedDegenerateTriangleCount +
                    " degenerate or invalid triangle(s) and " + uvRasterMesh.SkippedNonTriangleSubmeshCount +
                    " non-triangle submesh(es). Check the mesh before painting.";
            }
            return true;
        }

        private void DisposeUvRasterMesh()
        {
            if (uvRasterMesh != null)
            {
                uvRasterMesh.Dispose();
                uvRasterMesh = null;
            }
            uvRasterSourceMesh = null;
        }

        private bool StampSurfaceWorld(
            Vector3 worldPoint,
            Vector3 worldNormal,
            SurfaceLayerDefinition layer,
            bool erase,
            bool smooth,
            Texture2D alpha,
            bool allowPresetRandomness,
            out string error)
        {
            error = string.Empty;
            if (gpuEngine == null || !gpuEngine.IsInitialized || target == null || layer == null)
            {
                error = "Load a valid target and mask page before stamping.";
                return false;
            }
            if (!EnsureUvRasterMesh(out error))
                return false;

            float rotation = brushRotation;
            if (allowPresetRandomness && brushPreset != null && brushPreset.RandomRotation)
                rotation = UnityEngine.Random.Range(0f, 360f);
            float stampRadius = brushRadius;
            float effectiveJitter = allowPresetRandomness ? brushScaleJitter : 0f;
            if (effectiveJitter > 0f)
                stampRadius *= UnityEngine.Random.Range(1f - effectiveJitter, 1f + effectiveJitter);

            return gpuEngine.StampSurfaceMesh(
                uvRasterMesh.Mesh,
                target.transform.localToWorldMatrix,
                worldPoint,
                worldNormal,
                stampRadius,
                Mathf.Max(0.001f, stampRadius * projectionDepthScale),
                useSurfaceNormalFilter,
                surfaceNormalCutoff,
                brushOpacity,
                brushHardness,
                (int)layer.MaskChannel,
                erase,
                alpha,
                rotation,
                smooth,
                out error);
        }

        private void ApplyChannelFill(SurfaceLayerDefinition layer, float value, string successMessage)
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized || layer == null)
                return;

            StopStroke();
            if (!gpuEngine.BeginStroke(out string error))
            {
                paintStatus = error;
                Repaint();
                return;
            }

            if (gpuEngine.SetChannelValue((int)layer.MaskChannel, value, out error))
            {
                gpuEngine.EndStroke();
                hasUnsavedChanges = true;
                BindWorkingMaskPreview();
                paintStatus = successMessage + " Undo is available until the session is closed.";
            }
            else
            {
                gpuEngine.CancelStroke(out _);
                paintStatus = error;
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Surface Paint Profile", "SurfacePaintProfile", "asset",
                "Choose a folder inside Assets for the profile asset.");
            if (string.IsNullOrEmpty(path))
                return;

            SurfacePaintProfile asset = ScriptableObject.CreateInstance<SurfacePaintProfile>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            profile = asset;
            if (target != null)
                AssignProfile();
            EditorGUIUtility.PingObject(asset);
        }

        private void AssignProfile()
        {
            if (target == null || profile == null)
                return;
            Undo.RecordObject(target, "Assign Surface Paint Profile");
            target.SetProfile(profile);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        private void CreateMaskPage(int pageIndex)
        {
            string error;
            Texture2D created = SurfaceMaskAssetUtility.CreateBlackPage(
                profile, pageIndex, profile.MaskResolution, out error);
            if (created == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", error, "OK");
                return;
            }

            LoadMaskPage(pageIndex);
            paintStatus = "Created a black mask page. Black means no painted overlay.";
        }

        private void LoadMaskPage(int pageIndex)
        {
            if (profile == null)
                return;

            Texture2D asset = profile.GetMaskPage(pageIndex);
            if (asset == null)
            {
                paintStatus = "No mask asset is assigned to this page.";
                return;
            }

            RestoreSavedMaskPreview();
            DisposePaintSession();

            SurfaceGpuBrushEngine engine = new SurfaceGpuBrushEngine();
            string error;
            if (!engine.Initialize(asset, asset.width, asset.height, out error))
            {
                engine.Dispose();
                EditorUtility.DisplayDialog("SurfaceWeaver", error, "OK");
                return;
            }

            gpuEngine = engine;
            activeSessionProfile = profile;
            activeMaskPage = pageIndex;
            activeMaskAsset = asset;
            hasUnsavedChanges = false;
            BindWorkingMaskPreview();
            paintStatus = "Mask page loaded. Paint or erase in Scene View.";
            SceneView.RepaintAll();
            Repaint();
        }

        private void SaveMaskPage()
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized ||
                activeSessionProfile == null || activeMaskPage < 0)
            {
                paintStatus = "Load a mask page before saving.";
                return;
            }

            Texture2D saved;
            string error;
            if (!SurfaceMaskAssetUtility.SaveRenderTextureToAsset(
                    gpuEngine.Current,
                    activeMaskAsset,
                    activeSessionProfile,
                    activeMaskPage,
                    out saved,
                    out error))
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", error, "OK");
                paintStatus = error;
                return;
            }

            activeMaskAsset = saved;
            hasUnsavedChanges = false;
            BindWorkingMaskPreview();
            paintStatus = "Mask page saved as a PNG asset.";
            EditorGUIUtility.PingObject(saved);
            Repaint();
        }

        private void PaintAtHit(SurfaceSceneHit currentHit)
        {
            if (seamAwareProjection)
                PaintAtHitSeamAware(currentHit);
            else
                PaintAtHitLegacy(currentHit);
        }

        private void PaintAtHitSeamAware(SurfaceSceneHit currentHit)
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized || profile == null)
                return;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (layer == null || !layer.Enabled)
                return;

            bool erase = operationToolbar == 1;
            bool smooth = operationToolbar == 2;
            Texture2D alpha = GetActiveBrushAlpha();
            string error = string.Empty;
            bool stamped = false;

            if (!hasPreviousPaintHit || previousPaintHit.Target != currentHit.Target)
            {
                stamped = StampSurfaceWorld(currentHit.Point, currentHit.Normal, layer, erase, smooth, alpha, true, out error);
            }
            else
            {
                float worldDistance = Vector3.Distance(previousPaintHit.Point, currentHit.Point);
                float spacingDistance = Mathf.Max(
                    0.01f,
                    brushRadius * (1f - brushScaleJitter) * 2f * brushSpacing);
                int steps = Mathf.Clamp(Mathf.CeilToInt(worldDistance / spacingDistance), 1, 256);
                MeshCollider collider = target.GetComponent<MeshCollider>();

                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector3 approximate = Vector3.Lerp(previousPaintHit.Point, currentHit.Point, t);
                    Vector3 normal = Vector3.Slerp(previousPaintHit.Normal, currentHit.Normal, t).normalized;
                    if (!TryProjectPathPoint(collider, approximate, normal,
                            out Vector3 projected, out Vector3 projectedNormal))
                        continue;

                    approximate = projected;
                    normal = projectedNormal;

                    if (!StampSurfaceWorld(approximate, normal, layer, erase, smooth, alpha, true, out error))
                    {
                        hasPreviousPaintHit = false;
                        paintStatus = error;
                        Repaint();
                        return;
                    }
                    stamped = true;
                }
            }

            if (!stamped && !string.IsNullOrEmpty(error))
            {
                hasPreviousPaintHit = false;
                paintStatus = error;
                Repaint();
                return;
            }

            previousPaintHit = currentHit;
            hasPreviousPaintHit = true;
            BindWorkingMaskPreview();
            hasUnsavedChanges = true;
            paintStatus = smooth
                ? "Seam-aware smoothing applied to the selected layer. Save the mask page to persist changes."
                : erase
                    ? "Seam-aware erasing applied to the selected layer. Save the mask page to persist changes."
                    : "Seam-aware painting applied to the selected layer. Save the mask page to persist changes.";
            Repaint();
        }

        private void PaintAtHitLegacy(SurfaceSceneHit currentHit)
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized || profile == null)
                return;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (layer == null || !layer.Enabled)
                return;

            if (!SurfaceUvMetric.TryGetWorldPerUv(
                    currentHit, out Vector3 worldPerUvU, out Vector3 worldPerUvV, out string metricError))
            {
                paintStatus = metricError;
                hasPreviousPaintHit = false;
                Repaint();
                return;
            }

            bool erase = operationToolbar == 1;
            bool smooth = operationToolbar == 2;
            Texture2D alpha = GetActiveBrushAlpha();

            if (!hasPreviousPaintHit || previousPaintHit.Target != currentHit.Target ||
                HasUvDiscontinuity(previousPaintHit.Uv0, currentHit.Uv0))
            {
                StampLegacy(currentHit.Uv0, worldPerUvU, worldPerUvV, layer, erase, smooth, alpha);
            }
            else
            {
                float worldDistance = Vector3.Distance(previousPaintHit.Point, currentHit.Point);
                float spacingDistance = Mathf.Max(
                    0.01f,
                    brushRadius * (1f - brushScaleJitter) * 2f * brushSpacing);
                int steps = Mathf.Clamp(Mathf.CeilToInt(worldDistance / spacingDistance), 1, 256);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector2 uv = Vector2.Lerp(previousPaintHit.Uv0, currentHit.Uv0, t);
                    StampLegacy(uv, worldPerUvU, worldPerUvV, layer, erase, smooth, alpha);
                }
            }

            previousPaintHit = currentHit;
            hasPreviousPaintHit = true;
            BindWorkingMaskPreview();
            hasUnsavedChanges = true;
            paintStatus = "Legacy UV painting applied. Seam gaps may occur at UV discontinuities.";
            Repaint();
        }

        private bool StampLegacyPathHit(
            RaycastHit hit,
            MeshCollider collider,
            SurfaceLayerDefinition layer,
            bool erase,
            bool smooth,
            Texture2D alpha,
            bool allowPresetRandomness,
            out string error)
        {
            SurfaceSceneHit sceneHit = new SurfaceSceneHit(target, collider, hit);
            if (!SurfaceUvMetric.TryGetWorldPerUv(
                    sceneHit, out Vector3 worldPerUvU, out Vector3 worldPerUvV, out error))
                return false;

            float rotation = brushRotation;
            if (allowPresetRandomness && brushPreset != null && brushPreset.RandomRotation)
                rotation = UnityEngine.Random.Range(0f, 360f);
            float radius = brushRadius;
            if (allowPresetRandomness && brushScaleJitter > 0f)
                radius *= UnityEngine.Random.Range(1f - brushScaleJitter, 1f + brushScaleJitter);

            return gpuEngine.Stamp(
                sceneHit.Uv0, worldPerUvU, worldPerUvV, radius, brushOpacity, brushHardness,
                (int)layer.MaskChannel, erase, alpha, rotation, smooth, out error);
        }

        private void StampLegacy(
            Vector2 uv,
            Vector3 worldPerUvU,
            Vector3 worldPerUvV,
            SurfaceLayerDefinition layer,
            bool erase,
            bool smooth,
            Texture2D alpha,
            bool allowPresetRandomness = true)
        {
            float rotation = allowPresetRandomness && brushPreset != null && brushPreset.RandomRotation
                ? UnityEngine.Random.Range(0f, 360f)
                : brushRotation;
            float stampRadius = brushRadius;
            if (allowPresetRandomness && brushScaleJitter > 0f)
                stampRadius *= UnityEngine.Random.Range(1f - brushScaleJitter, 1f + brushScaleJitter);

            if (!gpuEngine.Stamp(
                    uv,
                    worldPerUvU,
                    worldPerUvV,
                    stampRadius,
                    brushOpacity,
                    brushHardness,
                    (int)layer.MaskChannel,
                    erase,
                    alpha,
                    rotation,
                    smooth,
                    out string error))
            {
                paintStatus = error;
            }
        }

        private static bool HasUvDiscontinuity(Vector2 previous, Vector2 current)
        {
            Vector2 delta = current - previous;
            return Mathf.Abs(delta.x) > 0.35f || Mathf.Abs(delta.y) > 0.35f;
        }

        private Texture2D GetActiveBrushAlpha()
        {
            if (directBrushAlpha != null)
                return directBrushAlpha;
            return brushPreset != null ? brushPreset.AlphaTexture : null;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!scenePreviewEnabled || target == null || !target.gameObject.activeInHierarchy)
            {
                hasSceneHit = false;
                if (isPainting)
                    StopStroke();
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null)
                return;

            if (currentEvent.alt)
            {
                hasSceneHit = false;
                if (isPainting)
                    StopStroke();
                return;
            }

            bool hitFound = SurfaceScenePicker.TryPick(
                target,
                currentEvent.mousePosition,
                out SurfaceSceneHit hit,
                out string status);

            int controlId = GUIUtility.GetControlID(
                "SurfaceWeaverScenePaint".GetHashCode(),
                FocusType.Passive);

            if (currentEvent.type == EventType.Layout && (hitFound || isPainting))
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (hitFound)
            {
                hasSceneHit = true;
                lastHitUv = hit.Uv0;
                lastHitPoint = hit.Point;
                lastHitNormal = hit.Normal;
                lastHitTriangle = hit.TriangleIndex;
                sceneHitStatus = status;
                SurfaceBrushPreview.Draw(hit, brushRadius * (1f + brushScaleJitter), previewColor);
            }
            else
            {
                hasSceneHit = false;
                sceneHitStatus = status;
            }

            if (pathAuthoringEnabled)
            {
                DrawSplinePathPreview(hitFound, hitFound ? hit.Point : Vector3.zero);

                if (currentEvent.type == EventType.Layout && (hitFound || pathPoints.Count > 0))
                    HandleUtility.AddDefaultControl(controlId);

                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 && hitFound)
                {
                    AddPathAnchor(hit);
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.KeyDown &&
                         currentEvent.keyCode == KeyCode.Backspace && pathPoints.Count > 0)
                {
                    pathPoints.RemoveAt(pathPoints.Count - 1);
                    pathStatus = "Removed the last spline anchor.";
                    Repaint();
                    SceneView.RepaintAll();
                    currentEvent.Use();
                }

                if (currentEvent.type == EventType.MouseMove ||
                    currentEvent.type == EventType.MouseDrag ||
                    currentEvent.type == EventType.Repaint)
                {
                    Repaint();
                    sceneView.Repaint();
                }
                return;
            }

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 && hitFound && CanPaint())
            {
                string beginError;
                gpuEngine.SetHistoryBudgetMegabytes(undoMemoryBudgetMb);
                hasUnsavedChangesBeforeStroke = hasUnsavedChanges;
                if (!gpuEngine.BeginStroke(out beginError))
                {
                    paintStatus = beginError;
                    Repaint();
                    currentEvent.Use();
                    return;
                }

                GUIUtility.hotControl = controlId;
                paintingControlId = controlId;
                isPainting = true;
                hasPreviousPaintHit = false;
                PaintAtHit(hit);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag &&
                     currentEvent.button == 0 && isPainting &&
                     GUIUtility.hotControl == paintingControlId)
            {
                if (!CanPaint())
                {
                    StopStroke();
                }
                else if (hitFound)
                {
                    PaintAtHit(hit);
                }
                else
                {
                    hasPreviousPaintHit = false;
                }
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp &&
                     currentEvent.button == 0 && isPainting &&
                     GUIUtility.hotControl == paintingControlId)
            {
                StopStroke();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.KeyDown &&
                     currentEvent.keyCode == KeyCode.Escape && isPainting)
            {
                CancelActiveStroke();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.KeyDown &&
                     (currentEvent.control || currentEvent.command) && !isPainting &&
                     (currentEvent.keyCode == KeyCode.Z || currentEvent.keyCode == KeyCode.Y) &&
                     gpuEngine != null && gpuEngine.IsInitialized && CanPaint())
            {
                if (currentEvent.keyCode == KeyCode.Y || currentEvent.shift)
                    RedoPaintStroke();
                else
                    UndoPaintStroke();
                currentEvent.Use();
            }

            if (currentEvent.type == EventType.MouseMove ||
                currentEvent.type == EventType.MouseDrag ||
                currentEvent.type == EventType.Repaint)
            {
                Repaint();
                sceneView.Repaint();
            }
        }

        private bool CanPaint()
        {
            SurfaceLayerDefinition layer = GetSelectedLayer();
            return paintingEnabled && !pathAuthoringEnabled && IsSessionForLayer(layer) &&
                   profile != null && target != null && target.Profile == profile &&
                   layer != null && layer.Enabled && target.UvChannel == 0;
        }

        private bool IsSessionForLayer(SurfaceLayerDefinition layer)
        {
            return layer != null && gpuEngine != null && gpuEngine.IsInitialized &&
                   activeSessionProfile == profile && activeMaskPage == layer.MaskPageIndex;
        }

        private SurfaceLayerDefinition GetSelectedLayer()
        {
            if (profile == null || profile.PaintLayers == null ||
                selectedLayerIndex < 0 || selectedLayerIndex >= profile.PaintLayers.Count)
                return null;
            return profile.PaintLayers[selectedLayerIndex];
        }

        private void StopStroke()
        {
            bool wasPainting = isPainting;
            isPainting = false;
            hasPreviousPaintHit = false;
            if (paintingControlId != 0 && GUIUtility.hotControl == paintingControlId)
                GUIUtility.hotControl = 0;
            paintingControlId = 0;

            if (wasPainting && gpuEngine != null && gpuEngine.IsInitialized)
            {
                gpuEngine.EndStroke();
                paintStatus = gpuEngine.CanUndo
                    ? "Stroke committed. Undo is available."
                    : "Stroke finished, but no history snapshot was retained. Increase the Undo memory budget or reduce mask resolution.";
                Repaint();
            }
        }

        private void CancelActiveStroke()
        {
            bool wasPainting = isPainting;
            isPainting = false;
            hasPreviousPaintHit = false;
            if (paintingControlId != 0 && GUIUtility.hotControl == paintingControlId)
                GUIUtility.hotControl = 0;
            paintingControlId = 0;

            if (wasPainting && gpuEngine != null && gpuEngine.IsInitialized)
            {
                if (gpuEngine.CancelStroke(out string error))
                {
                    hasUnsavedChanges = hasUnsavedChangesBeforeStroke;
                    BindWorkingMaskPreview();
                    paintStatus = "Active stroke cancelled.";
                }
                else
                {
                    paintStatus = error;
                }
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void UndoPaintStroke()
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized)
                return;
            StopStroke();
            if (gpuEngine.UndoLastStroke(out string error))
            {
                hasUnsavedChanges = true;
                BindWorkingMaskPreview();
                paintStatus = "Last stroke undone.";
            }
            else
            {
                paintStatus = error;
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void RedoPaintStroke()
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized)
                return;
            StopStroke();
            if (gpuEngine.RedoLastStroke(out string error))
            {
                hasUnsavedChanges = true;
                BindWorkingMaskPreview();
                paintStatus = "Stroke redone.";
            }
            else
            {
                paintStatus = error;
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private Material GetMaskChannelPreviewMaterial(int channel)
        {
            if (maskChannelPreviewMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/SurfaceWeaver/MaskChannelPreview");
                if (shader == null || !shader.isSupported)
                    return null;

                maskChannelPreviewMaterial = new Material(shader)
                {
                    name = "SurfaceWeaver Mask Channel Preview (Editor)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (maskChannelPreviewMaterial != null && cachedPreviewChannel != channel)
            {
                maskChannelPreviewMaterial.SetFloat("_ChannelIndex", channel);
                cachedPreviewChannel = channel;
            }

            return maskChannelPreviewMaterial;
        }

        private void BindWorkingMaskPreview()
        {
            if (gpuEngine == null || !gpuEngine.IsInitialized)
                return;
            BindMaskPreview(gpuEngine.Current);
        }

        private void RestoreSavedMaskPreview()
        {
            if (target == null || profile == null)
                return;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (layer == null)
                return;

            Texture2D saved = profile.GetMaskPage(layer.MaskPageIndex);
            BindMaskPreview(saved);
        }

        private void BindMaskPreview(Texture texture)
        {
            if (target == null)
                return;

            ResolveMaskPreviewPropertyForCurrentTarget();
            if (string.IsNullOrWhiteSpace(maskPreviewProperty))
                return;

            if (!target.TryGetRenderer(out MeshRenderer renderer) || renderer == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            int matchingIndex = -1;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty(maskPreviewProperty))
                {
                    matchingIndex = i;
                    break;
                }
            }

            if (matchingIndex < 0)
            {
                RestorePreviewMaterial();
                return;
            }

            Material slotMaterial = materials[matchingIndex];
            bool samePreviewSlot = previewRenderer == renderer &&
                                   previewMaterialIndex == matchingIndex &&
                                   previewMaterialInstance != null &&
                                   slotMaterial == previewMaterialInstance;

            if (!samePreviewSlot)
            {
                RestorePreviewMaterial();

                // RestorePreviewMaterial may have changed the slot array. Read it again.
                materials = renderer.sharedMaterials;
                if (matchingIndex >= materials.Length || materials[matchingIndex] == null)
                    return;

                previewOriginalMaterial = materials[matchingIndex];
                previewMaterialInstance = new Material(previewOriginalMaterial)
                {
                    name = previewOriginalMaterial.name + " (SurfaceWeaver Live Preview)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewRenderer = renderer;
                previewMaterialIndex = matchingIndex;

                materials[matchingIndex] = previewMaterialInstance;
                renderer.sharedMaterials = materials;
            }

            if (previewMaterialInstance != null && previewMaterialInstance.HasProperty(maskPreviewProperty))
            {
                previewMaterialInstance.SetTexture(maskPreviewProperty, texture);
                // The clone is HideAndDontSave. Do not mark the source material asset dirty.
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Restores the exact material slot replaced by the temporary editor preview instance.
        /// No MaterialPropertyBlock is created or modified by SurfaceWeaver's live preview.
        /// </summary>
        private void RestorePreviewMaterial()
        {
            if (previewRenderer != null && previewMaterialInstance != null)
            {
                Material[] materials = previewRenderer.sharedMaterials;
                if (previewMaterialIndex >= 0 && previewMaterialIndex < materials.Length &&
                    materials[previewMaterialIndex] == previewMaterialInstance)
                {
                    materials[previewMaterialIndex] = previewOriginalMaterial;
                    previewRenderer.sharedMaterials = materials;
                }
            }

            if (previewMaterialInstance != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(previewMaterialInstance);
                else
                    UnityEngine.Object.DestroyImmediate(previewMaterialInstance);
            }

            previewRenderer = null;
            previewOriginalMaterial = null;
            previewMaterialInstance = null;
            previewMaterialIndex = -1;
        }

        /// <summary>
        /// Keeps the live editor preview compatible with both the original Shader Graph contract
        /// (_PaintMask) and the v0.7 layered URP shader contract (_MaskPage0).
        /// The layered shader samples the packed RGBA page through _MaskPage0; it intentionally
        /// does not expose the legacy _PaintMask property.
        /// </summary>
        private void ResolveMaskPreviewPropertyForCurrentTarget()
        {
            if (target == null)
                return;

            // Preserve an explicitly configured property whenever the current renderer supports it.
            if (RendererHasProperty(target, maskPreviewProperty))
                return;

            // Prefer the v0.7 packed page when the generated SurfaceWeaver material is assigned.
            if (RendererHasProperty(target, "_MaskPage0"))
            {
                maskPreviewProperty = "_MaskPage0";
                return;
            }

            // Retain compatibility with older Shader Graph materials.
            if (RendererHasProperty(target, "_PaintMask"))
            {
                maskPreviewProperty = "_PaintMask";
                return;
            }
        }

        private static bool RendererHasProperty(SurfacePaintTarget paintTarget, string propertyName)
        {
            if (paintTarget == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            MeshRenderer renderer;
            if (!paintTarget.TryGetRenderer(out renderer))
                return false;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty(propertyName))
                    return true;
            }
            return false;
        }

        private void DisposePaintSession()
        {
            if (gpuEngine != null)
            {
                if (hasUnsavedChanges)
                    Debug.LogWarning("SurfaceWeaver: the active mask page has unsaved changes. Save Mask PNG before closing or switching the session.");
                gpuEngine.Dispose();
                gpuEngine = null;
            }

            activeSessionProfile = null;
            activeMaskAsset = null;
            activeMaskPage = -1;
            hasUnsavedChanges = false;
        }

        private static string FormatBytes(long bytes)
        {
            double megabytes = bytes / (1024.0 * 1024.0);
            return megabytes.ToString("0.0") + " MiB (approx., excludes source texture and editor overhead)";
        }

        private void DrawDiagnosticsSection()
        {
            EditorGUILayout.LabelField("Mesh Diagnostics", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Validate UV Channel"))
                {
                    if (target.TryGetMesh(out Mesh mesh))
                        lastReport = MeshUvValidator.Validate(mesh, target.UvChannel);
                }
            }

            if (lastReport == null)
                return;

            EditorGUILayout.LabelField("Result", lastReport.IsValid ? "No blocking issues found" : "Blocking issues found");
            EditorGUILayout.LabelField("Vertices", lastReport.VertexCount.ToString());
            EditorGUILayout.LabelField("Triangles", lastReport.TriangleCount.ToString());
            EditorGUILayout.LabelField("UV Entries", lastReport.UvCount.ToString());
            EditorGUILayout.LabelField("Out-of-range UVs", lastReport.OutOfRangeUvCount.ToString());
            EditorGUILayout.LabelField("Degenerate UV triangles", lastReport.DegenerateUvTriangleCount.ToString());
            EditorGUILayout.LabelField("Overlapping UV triangle pairs", lastReport.OverlappingUvTrianglePairCount.ToString());
            EditorGUILayout.LabelField("UV seam edges", lastReport.UvSeamEdgeCount.ToString());
            EditorGUILayout.LabelField("Overlap candidate tests", lastReport.OverlapCandidatePairTests.ToString());
            EditorGUILayout.LabelField("Overlap analysis", lastReport.OverlapAnalysisCompleted ? "Complete" : "Partial / safety limit reached");
            foreach (string error in lastReport.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in lastReport.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            foreach (string info in lastReport.Infos)
                EditorGUILayout.HelpBox(info, MessageType.Info);
        }

        private void DrawRoadmapSection()
        {
            EditorGUILayout.LabelField("Development Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Included: custom mesh targets, reusable profiles, UV range/degenerate/overlap/seam diagnostics, " +
                "Scene View picking, world-space brush preview, GPU paint/erase/smooth stamps, selected-channel fill/clear, " +
                "packed RGBA pages, starter brushes, stroke Undo/Redo and PNG persistence.\n\n" +
                "Added in v0.6: seam-aware world-space UV raster projection and reusable Catmull-Rom spline/path painting. " +
                "Still planned: UV gutter padding/dilation, mip-bleed analysis, editable/variable-width paths, scatter placement, " +
                "multi-object batch authoring, production multi-layer URP shader, runtime API and package validation.",
                MessageType.None);
        }
    }
}
