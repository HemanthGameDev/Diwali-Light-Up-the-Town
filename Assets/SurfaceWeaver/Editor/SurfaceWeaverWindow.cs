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
        private static SurfaceWeaverWindow activeWindow;

        private SurfacePaintTarget target;
        private SurfacePaintProfile profile;
        private MeshUvValidationReport lastReport;
        private bool showUvIslandDetails = true;
        private bool showUvIslandOverlapDetails = true;
        private bool showSameIslandOverlapDetails = true;
        private bool showUvGutterPaddingSection = true;
        private int uvGutterPaddingPixels = 8;
        private bool showMaskDilationSection = true;
        private int maskDilationRadiusPixels = 4;
        private bool showMipMapBleedingPreventionSection = true;
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
        private int selectedPathPointIndex = -1;
        private string pathStatus = "Click the target mesh in Scene View to add spline anchors.";
        private int pathRandomSeed = 1;
        private bool pathPressureAffectsWidth = true;
        private bool pathPressureAffectsOpacity = true;
        private bool showPathWidthPreview = true;
        // A live spline preview keeps one pre-preview mask snapshot pending so profile edits
        // can rebuild the visible result without stacking repeated stamps or Undo entries.
        private bool splinePreviewActive;
        private bool splinePreviewRebuildQueued;
        private bool hasUnsavedChangesBeforeSplinePreview;

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
            activeWindow = this;
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
            CommitSplinePreview("Committed live spline preview when the Painter window closed.");
            EditorApplication.delayCall -= ProcessQueuedSplinePreviewRebuild;
            splinePreviewRebuildQueued = false;
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

            if (activeWindow == this)
                activeWindow = null;
        }

        /// <summary>
        /// Temporarily restores the source material before the target Inspector applies
        /// profile bindings. This prevents an Apply click from editing the temporary preview clone.
        /// </summary>
        public static void SuspendPreviewForMaterialApply(SurfacePaintTarget selectedTarget)
        {
            if (activeWindow != null && activeWindow.target == selectedTarget)
                activeWindow.RestorePreviewMaterial();
        }

        /// <summary>Rebuilds the live Painter preview after the source material has been updated.</summary>
        public static void ResumePreviewAfterMaterialApply(SurfacePaintTarget selectedTarget)
        {
            if (activeWindow == null || activeWindow.target != selectedTarget)
                return;

            if (activeWindow.gpuEngine != null && activeWindow.gpuEngine.IsInitialized)
                activeWindow.BindWorkingMaskPreview();
            else
                activeWindow.RestoreSavedMaskPreview();

            activeWindow.Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Safely hands one target from the single-target Painter to the Multi-Mesh Painter.
        /// Any pending spline/brush work is committed and saved before the shared mask asset is
        /// opened by the second GPU session. The old GPU session is disposed to prevent stale
        /// working textures from overwriting multi-mesh results when the user returns.
        /// </summary>
        public static bool PrepareForMultiMeshPainting(
            SurfacePaintTarget selectedTarget,
            out bool painterSessionPrepared,
            out string error)
        {
            painterSessionPrepared = false;
            error = string.Empty;
            if (activeWindow == null || activeWindow.target != selectedTarget)
                return true;

            painterSessionPrepared = true;
            activeWindow.CommitSplinePreview("Committed the live spline preview before opening Multi-Mesh Painter.");
            activeWindow.StopStroke();
            if (activeWindow.hasUnsavedChanges)
                activeWindow.SaveMaskPage();

            if (activeWindow.hasUnsavedChanges)
            {
                error = "The single-target Painter has unsaved changes that could not be saved. Save its mask page before opening Multi-Mesh Painter.";
                painterSessionPrepared = false;
                return false;
            }

            activeWindow.RestorePreviewMaterial();
            activeWindow.DisposePaintSession();
            activeWindow.paintStatus = "Mask session released to Multi-Mesh Painter. It will reload the saved mask when you return.";
            activeWindow.Repaint();
            return true;
        }

        /// <summary>Reloads the saved mask after a Multi-Mesh Painter session has finished.
        /// This avoids resuming an older GPU working texture over newer multi-target changes.
        /// </summary>
        public static void ResumeAfterMultiMeshPainting(SurfacePaintTarget selectedTarget)
        {
            if (activeWindow == null || activeWindow.target != selectedTarget || activeWindow.profile == null)
                return;

            SurfaceLayerDefinition layer = activeWindow.GetSelectedLayer();
            if (layer != null && activeWindow.profile.GetMaskPage(layer.MaskPageIndex) != null)
                activeWindow.LoadMaskPage(layer.MaskPageIndex);
            else
                activeWindow.RestoreSavedMaskPreview();

            activeWindow.Repaint();
            SceneView.RepaintAll();
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

            CommitSplinePreview("Committed the active spline preview before changing targets.");
            StopStroke();
            RestorePreviewMaterial();
            DisposePaintSession();
            DisposeUvRasterMesh();
            pathPoints.Clear();
            selectedPathPointIndex = -1;
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
            DrawUvGutterPaddingSection();
            EditorGUILayout.Space(8f);
            DrawMaskDilationSection();
            EditorGUILayout.Space(8f);
            DrawMipMapBleedingPreventionSection();
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
            {
                QueueSplinePreviewRebuild();
                SceneView.RepaintAll();
            }

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
            EditorGUILayout.HelpBox(
                "Profile tint, tiling and opacity are defaults. Once a material is created, its tuned values are preserved by Update Profile Bindings. Use Reset Material Tuning from Profile on the target Inspector only when you intentionally want to overwrite the material values.",
                MessageType.Info);

            if (serializedProfile.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(profile);
                RefreshLivePreviewProfileBindings();
            }

            if (GUILayout.Button("Add Paint Layer"))
            {
                Undo.RecordObject(profile, "Add Surface Layer");
                profile.AddLayer("Surface " + (profile.PaintLayers.Count + 1));
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                RefreshLivePreviewProfileBindings();
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
            int previousLayerIndex = selectedLayerIndex;
            selectedLayerIndex = EditorGUILayout.Popup("Paint Layer", selectedLayerIndex, layerNames);
            if (selectedLayerIndex != previousLayerIndex)
                CommitSplinePreview("Committed the live spline preview before changing paint layers.");

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
                    "The layered URP material reads this layer's assigned RGBA channel. With a SurfaceWeaver layered material assigned, the selected channel is previewed on the mesh while you paint.",
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
            {
                QueueSplinePreviewRebuild();
                SceneView.RepaintAll();
            }

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
                    using (new EditorGUI.DisabledScope((!gpuEngine.CanUndo && !splinePreviewActive) || isPainting))
                    {
                        if (GUILayout.Button(splinePreviewActive ? "Cancel Preview (Undo)" : "Undo"))
                            UndoPaintStroke();
                    }
                    using (new EditorGUI.DisabledScope(!gpuEngine.CanRedo || isPainting || splinePreviewActive))
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
                if (!newPathMode)
                    CommitSplinePreview("Committed the live spline preview when leaving Path Authoring.");
                StopStroke();
                pathAuthoringEnabled = newPathMode;
                hasPreviousPaintHit = false;
                selectedPathPointIndex = -1;
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Path Anchors", pathPoints.Count.ToString());
            EditorGUILayout.LabelField(
                "Selected Anchor",
                selectedPathPointIndex >= 0 && selectedPathPointIndex < pathPoints.Count
                    ? "P" + (selectedPathPointIndex + 1)
                    : "None (click an anchor in Scene View or choose below)");

            // The dropdown is a reliable fallback for selecting older anchors when Scene View
            // handles overlap or are difficult to click. It does not change the active paint target.
            if (pathPoints.Count > 0)
            {
                string[] anchorOptions = new string[pathPoints.Count + 1];
                anchorOptions[0] = "None";
                for (int i = 0; i < pathPoints.Count; i++)
                    anchorOptions[i + 1] = "P" + (i + 1);

                int currentAnchorOption = Mathf.Clamp(selectedPathPointIndex + 1, 0, anchorOptions.Length - 1);
                EditorGUI.BeginChangeCheck();
                int selectedAnchorOption = EditorGUILayout.Popup(
                    "Edit Anchor", currentAnchorOption, anchorOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    selectedPathPointIndex = selectedAnchorOption - 1;
                    pathStatus = selectedPathPointIndex >= 0
                        ? "Selected anchor P" + (selectedPathPointIndex + 1) + ". Drag its center to move it or edit its tangent handles."
                        : "Anchor selection cleared.";
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            if (selectedPathPointIndex >= 0 && selectedPathPointIndex < pathPoints.Count)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Selected Anchor Profile", EditorStyles.boldLabel);
                SurfacePaintPathPoint selectedPoint = pathPoints[selectedPathPointIndex];
                EditorGUI.BeginChangeCheck();
                float editedWidthScale = EditorGUILayout.Slider(
                    "Width Scale", selectedPoint.WidthScale, 0.05f, 4f);
                float editedPressure = EditorGUILayout.Slider(
                    "Pressure", selectedPoint.Pressure, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    SetPathPointDynamics(selectedPathPointIndex, editedWidthScale, editedPressure);
                }
                EditorGUILayout.HelpBox(
                    "Width Scale changes the brush radius along the path. Pressure can optionally control width and opacity below. " +
                    "Values are interpolated smoothly between anchors.", MessageType.None);
            }

            EditorGUILayout.LabelField("Path Profile", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            showPathWidthPreview = EditorGUILayout.Toggle("Show Width Preview", showPathWidthPreview);
            pathPressureAffectsWidth = EditorGUILayout.Toggle("Pressure Affects Width", pathPressureAffectsWidth);
            pathPressureAffectsOpacity = EditorGUILayout.Toggle("Pressure Affects Opacity", pathPressureAffectsOpacity);
            closeSplinePath = EditorGUILayout.ToggleLeft("Close Path (connect last anchor to first)", closeSplinePath);
            randomizeSplineStamps = EditorGUILayout.ToggleLeft(
                "Randomize brush rotation / size per path stamp", randomizeSplineStamps);
            if (EditorGUI.EndChangeCheck())
            {
                QueueSplinePreviewRebuild();
                Repaint();
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedPathPointIndex < 0 || selectedPathPointIndex >= pathPoints.Count))
                {
                    if (GUILayout.Button("Reset Selected Handles"))
                        ResetPathTangents(selectedPathPointIndex);
                    if (GUILayout.Button("Reset Width / Pressure"))
                        SetPathPointDynamics(selectedPathPointIndex, 1f, 1f);
                    if (GUILayout.Button("Deselect Anchor"))
                    {
                        selectedPathPointIndex = -1;
                        SceneView.RepaintAll();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasAnyCustomPathTangents()))
                {
                    if (GUILayout.Button("Reset All Handles"))
                        ResetAllPathTangents();
                }
                using (new EditorGUI.DisabledScope(!HasAnyCustomPathDynamics()))
                {
                    if (GUILayout.Button("Reset All Profiles"))
                        ResetAllPathDynamics();
                }
                using (new EditorGUI.DisabledScope(pathPoints.Count == 0))
                {
                    if (GUILayout.Button("Clear Anchors"))
                    {
                        CommitSplinePreview("Committed the live spline preview before clearing anchors.");
                        pathPoints.Clear();
                        selectedPathPointIndex = -1;
                        pathRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
                        pathStatus = "Path anchors cleared.";
                        SceneView.RepaintAll();
                    }
                }
            }

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
                using (new EditorGUI.DisabledScope(pathAsset == null))
                {
                    if (GUILayout.Button("Load Asset Points"))
                        LoadPathAssetPoints();
                }
                using (new EditorGUI.DisabledScope(pathPoints.Count < 2 || target == null))
                {
                    if (GUILayout.Button("Save Path Asset"))
                        SavePathAsset();
                    if (!splinePreviewActive)
                    {
                        if (GUILayout.Button("Apply Spline Preview"))
                            ApplySplinePath();
                    }
                    else
                    {
                        if (GUILayout.Button("Commit Preview"))
                            CommitSplinePreview();
                    }
                }
            }

            if (splinePreviewActive)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        "Live preview is active. Width, pressure, path shape and brush setting changes rebuild the mask preview immediately. " +
                        "Commit keeps the result as one Undo step; Cancel restores the mask to its pre-preview state.",
                        MessageType.Info);
                }
                if (GUILayout.Button("Cancel Preview"))
                    CancelSplinePreview();
            }

            EditorGUILayout.LabelField("Path Status", pathStatus);
            EditorGUILayout.HelpBox(
                "Click the target mesh to add anchors. Click an anchor to select it, then drag its center to move it on the target. " +
                "The selected anchor displays incoming/outgoing tangent handles; drag them to reshape the Bézier curve. " +
                "Reset Selected Handles restores automatic tangents. Backspace removes the selected anchor (or the last anchor when none is selected); Delete removes the selected anchor. " +
                "Each anchor has Width Scale and Pressure values. The width rails preview the resulting brush radius; pressure can independently affect width and opacity. " +
                "Apply Spline Preview paints a live, editable preview: profile and path changes rebuild it against the original mask. Commit keeps the result as one Undo step; Cancel restores the original mask. " +
                "Closed-path behavior and reusable path assets are preserved.",
                MessageType.Info);
        }

        private bool HasAnyCustomPathTangents()
        {
            for (int i = 0; i < pathPoints.Count; i++)
            {
                if (pathPoints[i].HasCustomTangents)
                    return true;
            }
            return false;
        }

        private bool HasAnyCustomPathDynamics()
        {
            for (int i = 0; i < pathPoints.Count; i++)
            {
                if (pathPoints[i].HasCustomWidthPressure &&
                    (!Mathf.Approximately(pathPoints[i].WidthScale, 1f) ||
                     !Mathf.Approximately(pathPoints[i].Pressure, 1f)))
                    return true;
            }
            return false;
        }

        private void SetPathPointDynamics(int index, float widthScale, float pressure)
        {
            if (index < 0 || index >= pathPoints.Count)
                return;

            SurfacePaintPathPoint point = pathPoints[index];
            pathPoints[index] = new SurfacePaintPathPoint(
                point.LocalPosition, point.LocalNormal,
                point.LocalInTangent, point.LocalOutTangent, point.HasCustomTangents,
                widthScale, pressure, true);
            pathStatus = "Updated width/pressure profile for anchor P" + (index + 1) + ".";
            QueueSplinePreviewRebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        private void ResetAllPathDynamics()
        {
            for (int i = 0; i < pathPoints.Count; i++)
                SetPathPointDynamics(i, 1f, 1f);
            pathStatus = "Reset width/pressure profiles for all anchors.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void ResetPathTangents(int index)
        {
            if (index < 0 || index >= pathPoints.Count)
                return;

            SurfacePaintPathPoint point = pathPoints[index];
            pathPoints[index] = new SurfacePaintPathPoint(
                point.LocalPosition, point.LocalNormal,
                Vector3.zero, Vector3.zero, false,
                point.WidthScale, point.Pressure, point.HasCustomWidthPressure);
            pathStatus = "Restored automatic tangents for anchor P" + (index + 1) + ".";
            QueueSplinePreviewRebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        private void ResetAllPathTangents()
        {
            for (int i = 0; i < pathPoints.Count; i++)
            {
                SurfacePaintPathPoint point = pathPoints[i];
                pathPoints[i] = new SurfacePaintPathPoint(
                    point.LocalPosition, point.LocalNormal,
                    Vector3.zero, Vector3.zero, false,
                    point.WidthScale, point.Pressure, point.HasCustomWidthPressure);
            }
            pathStatus = "Restored automatic tangents for all path anchors.";
            QueueSplinePreviewRebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        private void AddPathAnchor(SurfaceSceneHit hit)
        {
            Matrix4x4 localToWorld = target.transform.localToWorldMatrix;
            if (pathPoints.Count == 0)
                pathRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);

            Vector3 localPosition = target.transform.InverseTransformPoint(hit.Point);
            Vector3 localNormal = localToWorld.transpose.MultiplyVector(hit.Normal).normalized;
            pathPoints.Add(new SurfacePaintPathPoint(
                localPosition, localNormal, Vector3.zero, Vector3.zero, false, 1f, 1f, true));
            selectedPathPointIndex = pathPoints.Count - 1;
            pathStatus = "Added anchor " + pathPoints.Count + ". Select it to edit its position or tangent handles.";
            QueueSplinePreviewRebuild();
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
                pathRandomSeed,
                pathPressureAffectsWidth,
                pathPressureAffectsOpacity);

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

            CommitSplinePreview("Committed the previous live preview before loading another path asset.");

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
            selectedPathPointIndex = -1;
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
            pathPressureAffectsWidth = source.PressureAffectsWidth;
            pathPressureAffectsOpacity = source.PressureAffectsOpacity;
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
            if (splinePreviewActive)
            {
                CommitSplinePreview();
                return;
            }

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
            hasUnsavedChangesBeforeSplinePreview = hasUnsavedChanges;
            if (!gpuEngine.BeginStroke(out string beginError))
            {
                pathStatus = beginError;
                Repaint();
                return;
            }

            if (!StampSplinePathSamples(out bool anyProjectedHit, out string failure))
            {
                gpuEngine.CancelStroke(out string cancelError);
                pathStatus = failure;
                if (!string.IsNullOrEmpty(cancelError))
                    pathStatus += " Undo restore: " + cancelError;
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // Keep the pre-preview snapshot pending. Parameter edits restore this baseline
            // and stamp the updated path, keeping the whole edit as one logical Undo step.
            splinePreviewActive = true;
            hasUnsavedChanges = true;
            BindWorkingMaskPreview();
            pathStatus = anyProjectedHit
                ? "Live spline preview active. Adjust width, pressure or path shape to see changes immediately. Commit Preview to keep it as one Undo step, or Cancel Preview to restore the original mask."
                : "Live spline preview active, but the current pressure profile is fully transparent. Adjust pressure, opacity settings or width to inspect the result. Commit or Cancel when ready.";
            paintStatus = pathStatus;
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>Stamps the current spline profile into the active GPU mask without opening/closing a history stroke.</summary>
        private bool StampSplinePathSamples(out bool anyProjectedHit, out string failure)
        {
            anyProjectedHit = false;
            failure = string.Empty;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (target == null || layer == null || !layer.Enabled ||
                !IsSessionForLayer(layer) || pathPoints.Count < 2)
            {
                failure = "The spline preview needs an enabled layer, an active mask page and at least two anchors.";
                return false;
            }

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null || collider.convex || collider.sharedMesh == null)
            {
                failure = "A non-convex MeshCollider using the target mesh is required to preview spline paths.";
                return false;
            }

            bool failed = false;
            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(pathRandomSeed);
            int segmentCount = closeSplinePath ? pathPoints.Count : pathPoints.Count - 1;
            try
            {
                for (int segment = 0; segment < segmentCount && !failed; segment++)
                {
                    float minimumProfileRadius = float.PositiveInfinity;
                    const int profileSpacingSamples = 24;
                    for (int profileSample = 0; profileSample <= profileSpacingSamples; profileSample++)
                    {
                        float profileT = profileSample / (float)profileSpacingSamples;
                        minimumProfileRadius = Mathf.Min(minimumProfileRadius,
                            GetPathSampleRadius(segment, profileT, closeSplinePath));
                    }
                    if (float.IsInfinity(minimumProfileRadius) || minimumProfileRadius <= 0f)
                        minimumProfileRadius = Mathf.Max(0.01f, brushRadius);

                    float minimumJitteredRadius = minimumProfileRadius *
                        (1f - (randomizeSplineStamps ? brushScaleJitter : 0f));
                    float spacingDistance = Mathf.Max(0.01f, minimumJitteredRadius * 2f * brushSpacing);
                    float worldSegmentLength = EstimatePathSegmentLength(segment, closeSplinePath);
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

                        anyProjectedHit = true;
                        float widthScale = EvaluatePathWidthScale(segment, t, closeSplinePath);
                        float pressure = EvaluatePathPressure(segment, t, closeSplinePath);
                        float radiusMultiplier = widthScale *
                            (pathPressureAffectsWidth ? pressure : 1f);
                        float opacityMultiplier = pathPressureAffectsOpacity ? pressure : 1f;
                        if (opacityMultiplier <= 0.0001f)
                            continue;

                        bool stampSucceeded;
                        if (seamAwareProjection)
                        {
                            stampSucceeded = StampSurfaceWorld(
                                projectedHit.point, projectedHit.normal, layer,
                                operationToolbar == 1, operationToolbar == 2, GetActiveBrushAlpha(),
                                randomizeSplineStamps, out failure,
                                radiusMultiplier, opacityMultiplier);
                        }
                        else
                        {
                            stampSucceeded = StampLegacyPathHit(
                                projectedHit, collider, layer, operationToolbar == 1,
                                operationToolbar == 2, GetActiveBrushAlpha(),
                                randomizeSplineStamps, radiusMultiplier, opacityMultiplier, out failure);
                        }

                        if (!stampSucceeded)
                        {
                            failed = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failed = true;
                failure = "Spline preview failed: " + exception.Message;
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
            }

            if (failed)
                return false;

            if (!anyProjectedHit)
            {
                failure = "No spline samples could be projected. Check that the anchors lie on the collider and the path does not cross a gap.";
                return false;
            }

            return true;
        }

        private void QueueSplinePreviewRebuild()
        {
            if (!splinePreviewActive || splinePreviewRebuildQueued)
                return;

            splinePreviewRebuildQueued = true;
            EditorApplication.delayCall += ProcessQueuedSplinePreviewRebuild;
        }

        private void ProcessQueuedSplinePreviewRebuild()
        {
            EditorApplication.delayCall -= ProcessQueuedSplinePreviewRebuild;
            splinePreviewRebuildQueued = false;
            if (this == null || !splinePreviewActive)
                return;

            RefreshLiveSplinePreview();
        }

        private void RefreshLiveSplinePreview()
        {
            if (!splinePreviewActive || gpuEngine == null || !gpuEngine.IsInitialized)
                return;

            if (!gpuEngine.ResetPendingStrokePreview(out string resetError))
            {
                CancelSplinePreview();
                pathStatus = "Could not refresh the live preview: " + resetError;
                paintStatus = pathStatus;
                Repaint();
                return;
            }

            if (!StampSplinePathSamples(out bool anyProjectedHit, out string failure))
            {
                gpuEngine.CancelStroke(out string cancelError);
                splinePreviewActive = false;
                hasUnsavedChanges = hasUnsavedChangesBeforeSplinePreview;
                if (!string.IsNullOrEmpty(cancelError))
                    failure += " Undo restore: " + cancelError;
                pathStatus = "Live preview was cancelled because the updated path could not be stamped. " + failure;
                BindWorkingMaskPreview();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            hasUnsavedChanges = true;
            BindWorkingMaskPreview();
            pathStatus = anyProjectedHit
                ? "Live preview updated. Commit Preview to keep the latest settings as one Undo step, or Cancel Preview to restore the original mask."
                : "Live preview updated with no projected samples.";
            paintStatus = pathStatus;
            Repaint();
            SceneView.RepaintAll();
        }

        private void CommitSplinePreview(string message = null)
        {
            if (!splinePreviewActive)
                return;

            // A slider/handle may have queued a rebuild immediately before the user
            // commits or saves. Process it synchronously so the committed mask always
            // contains the latest displayed profile values.
            if (splinePreviewRebuildQueued)
                ProcessQueuedSplinePreviewRebuild();
            if (!splinePreviewActive)
                return;

            bool previewChangedMask = gpuEngine != null && gpuEngine.IsInitialized &&
                                      gpuEngine.HasPendingStrokeChanges;
            if (gpuEngine != null && gpuEngine.IsInitialized)
                gpuEngine.EndStroke();
            if (!previewChangedMask)
                hasUnsavedChanges = hasUnsavedChangesBeforeSplinePreview;

            splinePreviewActive = false;
            splinePreviewRebuildQueued = false;
            EditorApplication.delayCall -= ProcessQueuedSplinePreviewRebuild;
            if (!string.IsNullOrEmpty(message))
                pathStatus = message;
            else
                pathStatus = "Live spline preview committed as one Undo step. Save the mask page to persist the result.";
            paintStatus = pathStatus;
            Repaint();
            SceneView.RepaintAll();
        }

        private void CancelSplinePreview()
        {
            if (!splinePreviewActive)
                return;

            string error = string.Empty;
            bool restored = gpuEngine != null && gpuEngine.IsInitialized && gpuEngine.CancelStroke(out error);
            splinePreviewActive = false;
            splinePreviewRebuildQueued = false;
            EditorApplication.delayCall -= ProcessQueuedSplinePreviewRebuild;
            hasUnsavedChanges = restored
                ? hasUnsavedChangesBeforeSplinePreview
                : true;
            BindWorkingMaskPreview();
            pathStatus = restored
                ? "Live spline preview cancelled. The mask has been restored to its pre-preview state."
                : "Could not fully cancel the live spline preview. " + error;
            paintStatus = pathStatus;
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawSplinePathPreview(bool hasCurrentHit, Vector3 currentHitPoint)
        {
            if (target == null)
                return;

            Event currentEvent = Event.current;
            if (currentEvent == null)
                return;

            if (currentEvent.type == EventType.Repaint && pathPoints.Count >= 2)
            {
                List<Vector3> line = new List<Vector3>();
                int previewSegments = closeSplinePath ? pathPoints.Count : pathPoints.Count - 1;
                for (int segment = 0; segment < previewSegments; segment++)
                {
                    const int sampleCount = 24;
                    for (int sample = segment == 0 ? 0 : 1; sample <= sampleCount; sample++)
                    {
                        Vector3 local = EvaluatePathPosition(segment, sample / (float)sampleCount, closeSplinePath);
                        line.Add(target.transform.TransformPoint(local));
                    }
                }

                Handles.color = new Color(1f, 0.68f, 0.12f, 0.95f);
                if (line.Count >= 2)
                    Handles.DrawAAPolyLine(3f, line.ToArray());

                if (showPathWidthPreview)
                    DrawPathWidthRails(previewSegments);
            }

            for (int i = 0; i < pathPoints.Count; i++)
            {
                SurfacePaintPathPoint point = pathPoints[i];
                Vector3 world = target.transform.TransformPoint(point.LocalPosition);
                float handleSize = HandleUtility.GetHandleSize(world);
                bool selected = i == selectedPathPointIndex;

                int anchorControlId = GUIUtility.GetControlID(
                    ("SurfaceWeaverPathAnchor" + i).GetHashCode(), FocusType.Passive);
                bool clickedAnchor = currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 && HandleUtility.nearestControl == anchorControlId;

                Handles.color = selected
                    ? new Color(0.2f, 0.9f, 1f, 1f)
                    : Color.white;
                EditorGUI.BeginChangeCheck();
                Vector3 movedWorld = Handles.FreeMoveHandle(
                    anchorControlId,
                    world,
                    handleSize * (selected ? 0.11f : 0.085f),
                    Vector3.zero,
                    Handles.SphereHandleCap);
                bool anchorMoved = EditorGUI.EndChangeCheck();

                if (clickedAnchor)
                {
                    selectedPathPointIndex = i;
                    pathStatus = "Selected anchor P" + (i + 1) + ". Drag its center to move it or edit its tangent handles.";
                    Repaint();
                    SceneView.RepaintAll();
                }

                if (anchorMoved)
                {
                    // Every anchor owns a live handle. Selecting the handle being dragged
                    // ensures older points remain editable after new anchors are added.
                    selectedPathPointIndex = i;
                    pathStatus = "Moving anchor P" + (i + 1) + ".";
                    MovePathAnchorToWorld(i, movedWorld);
                }

                if (currentEvent.type == EventType.Repaint)
                    Handles.Label(world, "  P" + (i + 1));

                if (selectedPathPointIndex == i)
                    DrawSelectedPathTangentHandles(i, handleSize);
            }

            if (currentEvent.type == EventType.Repaint && pathAuthoringEnabled &&
                hasCurrentHit && pathPoints.Count > 0)
            {
                Vector3 last = target.transform.TransformPoint(pathPoints[pathPoints.Count - 1].LocalPosition);
                Handles.color = new Color(0.3f, 0.85f, 1f, 0.8f);
                Handles.DrawAAPolyLine(2f, last, currentHitPoint);
            }
        }

        private void DrawSelectedPathTangentHandles(int index, float anchorHandleSize)
        {
            if (index < 0 || index >= pathPoints.Count)
                return;

            bool hasIncoming = closeSplinePath || index > 0;
            bool hasOutgoing = closeSplinePath || index < pathPoints.Count - 1;
            if (!hasIncoming && !hasOutgoing)
                return;

            SurfacePaintPathPoint point = pathPoints[index];
            Vector3 anchorWorld = target.transform.TransformPoint(point.LocalPosition);
            Vector3 incoming = GetIncomingPathTangent(index, closeSplinePath);
            Vector3 outgoing = GetOutgoingPathTangent(index, closeSplinePath);
            float tangentHandleSize = anchorHandleSize * 0.075f;

            if (hasIncoming)
            {
                Vector3 incomingWorld = anchorWorld + target.transform.TransformVector(incoming);
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.color = new Color(0.25f, 0.85f, 0.45f, 0.95f);
                    Handles.DrawAAPolyLine(2f, anchorWorld, incomingWorld);
                    Handles.Label(incomingWorld, " In");
                }

                int incomingControlId = GUIUtility.GetControlID(
                    ("SurfaceWeaverPathInTangent" + index).GetHashCode(), FocusType.Passive);
                Handles.color = new Color(0.25f, 1f, 0.5f, 1f);
                EditorGUI.BeginChangeCheck();
                Vector3 movedIncoming = Handles.FreeMoveHandle(
                    incomingControlId, incomingWorld, tangentHandleSize,
                    Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                    SetPathTangentFromWorldHandle(index, true, movedIncoming, incoming, outgoing);
            }

            if (hasOutgoing)
            {
                // Re-read after the incoming handle in case it was edited in this event.
                point = pathPoints[index];
                incoming = GetIncomingPathTangent(index, closeSplinePath);
                outgoing = GetOutgoingPathTangent(index, closeSplinePath);
                Vector3 outgoingWorld = anchorWorld + target.transform.TransformVector(outgoing);
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.color = new Color(1f, 0.55f, 0.18f, 0.95f);
                    Handles.DrawAAPolyLine(2f, anchorWorld, outgoingWorld);
                    Handles.Label(outgoingWorld, " Out");
                }

                int outgoingControlId = GUIUtility.GetControlID(
                    ("SurfaceWeaverPathOutTangent" + index).GetHashCode(), FocusType.Passive);
                Handles.color = new Color(1f, 0.6f, 0.2f, 1f);
                EditorGUI.BeginChangeCheck();
                Vector3 movedOutgoing = Handles.FreeMoveHandle(
                    outgoingControlId, outgoingWorld, tangentHandleSize,
                    Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                    SetPathTangentFromWorldHandle(index, false, movedOutgoing, incoming, outgoing);
            }
        }

        private void SetPathTangentFromWorldHandle(
            int index, bool incomingHandle, Vector3 movedWorld, Vector3 currentIncoming, Vector3 currentOutgoing)
        {
            if (index < 0 || index >= pathPoints.Count || target == null)
                return;

            SurfacePaintPathPoint point = pathPoints[index];
            Vector3 anchorWorld = target.transform.TransformPoint(point.LocalPosition);
            Vector3 movedLocalOffset = target.transform.InverseTransformVector(movedWorld - anchorWorld);
            Vector3 inTangent = incomingHandle ? movedLocalOffset : currentIncoming;
            Vector3 outTangent = incomingHandle ? currentOutgoing : movedLocalOffset;

            pathPoints[index] = new SurfacePaintPathPoint(
                point.LocalPosition, point.LocalNormal, inTangent, outTangent, true,
                point.WidthScale, point.Pressure, point.HasCustomWidthPressure);
            pathStatus = (incomingHandle ? "Edited incoming" : "Edited outgoing") +
                " tangent for anchor P" + (index + 1) + ". Save the path asset to preserve the edit.";
            QueueSplinePreviewRebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        private void MovePathAnchorToWorld(int index, Vector3 proposedWorld)
        {
            if (index < 0 || index >= pathPoints.Count || target == null)
                return;

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null || collider.convex || collider.sharedMesh == null)
            {
                pathStatus = "Anchor movement requires a non-convex MeshCollider on the target so the anchor can stay on the surface.";
                Repaint();
                return;
            }

            Vector3 snappedWorld = collider.ClosestPoint(proposedWorld);
            float snapDistance = Vector3.Distance(proposedWorld, snappedWorld);
            Renderer targetRenderer = target.GetComponent<Renderer>();
            float allowedSnapDistance = targetRenderer != null
                ? Mathf.Max(brushRadius * 2f, targetRenderer.bounds.extents.magnitude * 0.25f)
                : Mathf.Max(brushRadius * 2f, 0.5f);
            if (snapDistance > allowedSnapDistance)
            {
                pathStatus = "Anchor was not moved because the dragged point is too far from the target surface. Drag closer to the mesh.";
                Repaint();
                return;
            }

            SurfacePaintPathPoint point = pathPoints[index];
            Vector3 worldNormal = LocalNormalToWorld(point.LocalNormal);
            float normalProbe = Mathf.Max(0.05f, brushRadius * 0.05f);
            RaycastHit normalHit;
            if (collider.Raycast(new Ray(snappedWorld + worldNormal * normalProbe, -worldNormal),
                    out normalHit, normalProbe * 2f))
            {
                snappedWorld = normalHit.point;
                worldNormal = normalHit.normal;
            }
            else if (collider.Raycast(new Ray(snappedWorld - worldNormal * normalProbe, worldNormal),
                         out normalHit, normalProbe * 2f))
            {
                snappedWorld = normalHit.point;
                worldNormal = normalHit.normal;
            }

            Vector3 localPosition = target.transform.InverseTransformPoint(snappedWorld);
            Vector3 localNormal = target.transform.localToWorldMatrix.transpose.MultiplyVector(worldNormal).normalized;
            pathPoints[index] = new SurfacePaintPathPoint(
                localPosition, localNormal,
                point.LocalInTangent, point.LocalOutTangent, point.HasCustomTangents,
                point.WidthScale, point.Pressure, point.HasCustomWidthPressure);
            pathStatus = "Moved anchor P" + (index + 1) + " on the target surface.";
            QueueSplinePreviewRebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        private Vector3 EvaluatePathPosition(int segment, float t, bool closed)
        {
            int count = pathPoints.Count;
            if (count < 2)
                return count == 1 ? pathPoints[0].LocalPosition : Vector3.zero;

            int startIndex = segment;
            int endIndex = closed ? (segment + 1) % count : segment + 1;
            Vector3 start = pathPoints[startIndex].LocalPosition;
            Vector3 end = pathPoints[endIndex].LocalPosition;
            Vector3 control1 = start + GetOutgoingPathTangent(startIndex, closed);
            Vector3 control2 = end + GetIncomingPathTangent(endIndex, closed);
            return CubicBezier(start, control1, control2, end, t);
        }

        private Vector3 GetOutgoingPathTangent(int index, bool closed)
        {
            int count = pathPoints.Count;
            if (index < 0 || index >= count || count < 2)
                return Vector3.zero;

            SurfacePaintPathPoint point = pathPoints[index];
            if (point.HasCustomTangents)
                return point.LocalOutTangent;

            if (!closed && index == count - 1)
                return Vector3.zero;
            if (!closed && index == 0)
                return (pathPoints[1].LocalPosition - point.LocalPosition) / 6f;

            Vector3 previous = pathPoints[(index - 1 + count) % count].LocalPosition;
            Vector3 next = pathPoints[(index + 1) % count].LocalPosition;
            return (next - previous) / 6f;
        }

        private Vector3 GetIncomingPathTangent(int index, bool closed)
        {
            int count = pathPoints.Count;
            if (index < 0 || index >= count || count < 2)
                return Vector3.zero;

            SurfacePaintPathPoint point = pathPoints[index];
            if (point.HasCustomTangents)
                return point.LocalInTangent;

            if (!closed && index == 0)
                return Vector3.zero;
            if (!closed && index == count - 1)
                return (pathPoints[count - 2].LocalPosition - point.LocalPosition) / 6f;

            Vector3 previous = pathPoints[(index - 1 + count) % count].LocalPosition;
            Vector3 next = pathPoints[(index + 1) % count].LocalPosition;
            return (previous - next) / 6f;
        }

        private float EvaluatePathWidthScale(int segment, float t, bool closed)
        {
            return EvaluatePathScalarProfile(segment, t, closed, true);
        }

        private float EvaluatePathPressure(int segment, float t, bool closed)
        {
            return EvaluatePathScalarProfile(segment, t, closed, false);
        }

        private float EvaluatePathScalarProfile(int segment, float t, bool closed, bool width)
        {
            int count = pathPoints.Count;
            if (count == 0)
                return width ? 1f : 1f;
            if (count == 1)
                return width ? pathPoints[0].WidthScale : pathPoints[0].Pressure;

            int i1 = Mathf.Clamp(segment, 0, count - 1);
            int i2 = closed ? (i1 + 1) % count : Mathf.Min(i1 + 1, count - 1);
            int i0 = closed ? (i1 - 1 + count) % count : Mathf.Max(i1 - 1, 0);
            int i3 = closed ? (i2 + 1) % count : Mathf.Min(i2 + 1, count - 1);

            float p0 = width ? pathPoints[i0].WidthScale : pathPoints[i0].Pressure;
            float p1 = width ? pathPoints[i1].WidthScale : pathPoints[i1].Pressure;
            float p2 = width ? pathPoints[i2].WidthScale : pathPoints[i2].Pressure;
            float p3 = width ? pathPoints[i3].WidthScale : pathPoints[i3].Pressure;
            float value = CatmullRomScalar(p0, p1, p2, p3, Mathf.Clamp01(t));
            return width ? Mathf.Clamp(value, 0.05f, 4f) : Mathf.Clamp01(value);
        }

        private static float CatmullRomScalar(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private float GetPathSampleRadius(int segment, float t, bool closed)
        {
            float width = EvaluatePathWidthScale(segment, t, closed);
            float pressure = EvaluatePathPressure(segment, t, closed);
            float pressureScale = pathPressureAffectsWidth ? pressure : 1f;
            return Mathf.Max(0.01f, brushRadius * width * pressureScale);
        }

        private void DrawPathWidthRails(int segmentCount)
        {
            if (target == null || pathPoints.Count < 2)
                return;

            List<Vector3> leftRail = new List<Vector3>();
            List<Vector3> rightRail = new List<Vector3>();
            const int samplesPerSegment = 24;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                int startSample = segment == 0 ? 0 : 1;
                for (int sample = startSample; sample <= samplesPerSegment; sample++)
                {
                    float t = sample / (float)samplesPerSegment;
                    Vector3 localPosition = EvaluatePathPosition(segment, t, closeSplinePath);
                    Vector3 before = EvaluatePathPosition(segment, Mathf.Max(0f, t - 0.01f), closeSplinePath);
                    Vector3 after = EvaluatePathPosition(segment, Mathf.Min(1f, t + 0.01f), closeSplinePath);
                    Vector3 tangent = after - before;
                    Vector3 normal = EvaluatePathNormal(segment, t, closeSplinePath);
                    Vector3 side = Vector3.Cross(normal, tangent);
                    if (side.sqrMagnitude < 0.000001f)
                        side = Vector3.Cross(Vector3.up, tangent);
                    if (side.sqrMagnitude < 0.000001f)
                        side = Vector3.right;
                    side.Normalize();

                    float halfWidth = GetPathSampleRadius(segment, t, closeSplinePath);
                    Vector3 worldCenter = target.transform.TransformPoint(localPosition);
                    Vector3 worldSide = target.transform.TransformVector(side).normalized;
                    float worldScale = target.transform.TransformVector(side * halfWidth).magnitude;
                    Vector3 offset = worldSide * worldScale;
                    leftRail.Add(worldCenter + offset);
                    rightRail.Add(worldCenter - offset);
                }
            }

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.55f);
            if (leftRail.Count >= 2)
            {
                Handles.DrawAAPolyLine(1.5f, leftRail.ToArray());
                Handles.DrawAAPolyLine(1.5f, rightRail.ToArray());
            }
        }

        private float EstimatePathSegmentLength(int segment, bool closed)
        {
            if (target == null || pathPoints.Count < 2)
                return 0f;

            const int sampleCount = 16;
            Vector3 previous = target.transform.TransformPoint(EvaluatePathPosition(segment, 0f, closed));
            float length = 0f;
            for (int i = 1; i <= sampleCount; i++)
            {
                Vector3 current = target.transform.TransformPoint(
                    EvaluatePathPosition(segment, i / (float)sampleCount, closed));
                length += Vector3.Distance(previous, current);
                previous = current;
            }
            return length;
        }

        private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float t2 = t * t;
            return oneMinusT2 * oneMinusT * p0 +
                3f * oneMinusT2 * t * p1 +
                3f * oneMinusT * t2 * p2 +
                t2 * t * p3;
        }

        private Vector3 EvaluatePathNormal(int segment, float t, bool closed)
        {
            int next = closed ? (segment + 1) % pathPoints.Count : segment + 1;
            Vector3 n1 = pathPoints[segment].LocalNormal;
            Vector3 n2 = pathPoints[next].LocalNormal;
            Vector3 normal = Vector3.Slerp(n1, n2, t);
            return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
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
            out string error,
            float radiusMultiplier = 1f,
            float opacityMultiplier = 1f)
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
            float stampRadius = brushRadius * Mathf.Clamp(radiusMultiplier, 0.01f, 4f);
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
                brushOpacity * Mathf.Clamp01(opacityMultiplier),
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

            CommitSplinePreview("Committed the live spline preview before applying a channel fill.");
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
            CommitSplinePreview("Committed the live spline preview before saving the mask page.");
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
            float radiusMultiplier,
            float opacityMultiplier,
            out string error)
        {
            SurfaceSceneHit sceneHit = new SurfaceSceneHit(target, collider, hit);
            if (!SurfaceUvMetric.TryGetWorldPerUv(
                    sceneHit, out Vector3 worldPerUvU, out Vector3 worldPerUvV, out error))
                return false;

            float rotation = brushRotation;
            if (allowPresetRandomness && brushPreset != null && brushPreset.RandomRotation)
                rotation = UnityEngine.Random.Range(0f, 360f);
            float radius = brushRadius * Mathf.Clamp(radiusMultiplier, 0.01f, 4f);
            if (allowPresetRandomness && brushScaleJitter > 0f)
                radius *= UnityEngine.Random.Range(1f - brushScaleJitter, 1f + brushScaleJitter);

            return gpuEngine.Stamp(
                sceneHit.Uv0, worldPerUvU, worldPerUvV, radius,
                brushOpacity * Mathf.Clamp01(opacityMultiplier), brushHardness,
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

            if (currentEvent.type == EventType.Layout && (hitFound || isPainting) && !pathAuthoringEnabled)
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

                // Register a default control only after path handles have had a chance to
                // register their own controls. This keeps mesh clicks inside path authoring
                // without stealing clicks/drags from anchors and tangent handles.
                if (currentEvent.type == EventType.Layout && (hitFound || pathPoints.Count > 0))
                    HandleUtility.AddDefaultControl(controlId);

                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 && hitFound && GUIUtility.hotControl == 0)
                {
                    AddPathAnchor(hit);
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.KeyDown && pathPoints.Count > 0 &&
                         (currentEvent.keyCode == KeyCode.Backspace || currentEvent.keyCode == KeyCode.Delete))
                {
                    int removeIndex = selectedPathPointIndex >= 0 && selectedPathPointIndex < pathPoints.Count
                        ? selectedPathPointIndex
                        : (currentEvent.keyCode == KeyCode.Backspace ? pathPoints.Count - 1 : -1);
                    if (removeIndex >= 0)
                    {
                        pathPoints.RemoveAt(removeIndex);
                        selectedPathPointIndex = -1;
                        pathStatus = "Removed spline anchor P" + (removeIndex + 1) + ".";
                        if (splinePreviewActive && pathPoints.Count < 2)
                            CancelSplinePreview();
                        else
                            QueueSplinePreviewRebuild();
                        Repaint();
                        SceneView.RepaintAll();
                        currentEvent.Use();
                    }
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
                CommitSplinePreview("Committed the live spline preview before starting a brush stroke.");
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
            if (splinePreviewActive)
            {
                CancelSplinePreview();
                return;
            }
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
            if (splinePreviewActive)
            {
                pathStatus = "Commit or cancel the live spline preview before redoing another stroke.";
                Repaint();
                return;
            }
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

            if (previewMaterialInstance == null ||
                !previewMaterialInstance.HasProperty(maskPreviewProperty))
                return;

            if (profile != null && previewMaterialInstance.shader != null &&
                previewMaterialInstance.shader.name == SurfacePaintMaterialBinder.ShaderName)
            {
                // Refresh profile-driven maps, RGBA channel addresses and overlay count
                // on every paint update, while retaining the source material's tuning.
                SurfacePaintMaterialBinder.CopyMaterialTuning(
                    previewOriginalMaterial, previewMaterialInstance);
                SurfacePaintMaterialBinder.ApplyProfileToMaterial(
                    profile,
                    previewMaterialInstance,
                    texture,
                    true,
                    out _);
            }
            else
            {
                // Compatibility path for older Shader Graph materials using _PaintMask.
                previewMaterialInstance.SetTexture(maskPreviewProperty, texture);
            }

            // The clone is HideAndDontSave. Do not mark the source material asset dirty.
            SceneView.RepaintAll();
        }

        /// <summary>Refreshes the temporary material when profile data changes in the Painter.</summary>
        private void RefreshLivePreviewProfileBindings()
        {
            if (target == null || profile == null || previewMaterialInstance == null)
                return;

            Texture currentMask = gpuEngine != null && gpuEngine.IsInitialized
                ? (Texture)gpuEngine.Current
                : profile.GetMaskPage(0);
            BindMaskPreview(currentMask);
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
            CommitSplinePreview("Committed the live spline preview before disposing the mask session.");
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

            string validationResult = !lastReport.IsValid
                ? "Blocking errors found"
                : lastReport.Warnings.Count > 0
                    ? "Completed with warnings — review the diagnostics below"
                    : "No issues detected";
            EditorGUILayout.LabelField("Result", validationResult);
            EditorGUILayout.LabelField("Vertices", lastReport.VertexCount.ToString());
            EditorGUILayout.LabelField("Triangles", lastReport.TriangleCount.ToString());
            EditorGUILayout.LabelField("UV Entries", lastReport.UvCount.ToString());
            EditorGUILayout.LabelField("Out-of-range UVs", lastReport.OutOfRangeUvCount.ToString());
            EditorGUILayout.LabelField("Degenerate UV triangles", lastReport.DegenerateUvTriangleCount.ToString());
            EditorGUILayout.LabelField("Overlapping UV triangle pairs", lastReport.OverlappingUvTrianglePairCount.ToString());
            EditorGUILayout.LabelField("UV seam edges", lastReport.UvSeamEdgeCount.ToString());
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("UV Island Analysis", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Connected UV islands", lastReport.UvIslandCount.ToString());
            EditorGUILayout.LabelField("Fully stacked island pairs", lastReport.StackedUvIslandPairCount.ToString());
            EditorGUILayout.LabelField("Fully mirrored island pairs", lastReport.MirroredUvIslandPairCount.ToString());
            EditorGUILayout.LabelField("Contained island overlap pairs", lastReport.ContainedUvIslandOverlapPairCount.ToString());
            EditorGUILayout.LabelField("Partial island overlap pairs", lastReport.PartialUvIslandOverlapPairCount.ToString());
            EditorGUILayout.LabelField("Overlapping island pairs", lastReport.OverlappingUvIslandPairCount.ToString());
            EditorGUILayout.LabelField("Triangle overlaps within connected islands", lastReport.SameIslandOverlappingTrianglePairCount.ToString());
            EditorGUILayout.LabelField("Mirrored overlaps within islands", lastReport.MirroredOverlapWithinIslandTrianglePairCount.ToString());
            EditorGUILayout.LabelField("Same-orientation overlaps within islands", lastReport.SameOrientationOverlapWithinIslandTrianglePairCount.ToString());
            EditorGUILayout.LabelField("Island pair checks", lastReport.IslandPairCandidateTests.ToString());
            EditorGUILayout.LabelField("Island triangle-pair checks", lastReport.IslandOverlapCandidateTrianglePairTests.ToString());
            EditorGUILayout.LabelField("Island overlap analysis", lastReport.IslandOverlapAnalysisCompleted ? "Complete" : "Partial / safety limit reached");
            EditorGUILayout.LabelField("Triangle overlap candidate tests", lastReport.OverlapCandidatePairTests.ToString());
            EditorGUILayout.LabelField("Triangle overlap analysis", lastReport.OverlapAnalysisCompleted ? "Complete" : "Partial / safety limit reached");

            if (lastReport.UvIslandSummaries.Count > 0)
            {
                showUvIslandDetails = EditorGUILayout.Foldout(
                    showUvIslandDetails,
                    "Island Bounds (" + lastReport.UvIslandSummaries.Count + ")",
                    true);
                if (showUvIslandDetails)
                {
                    int visibleCount = Mathf.Min(lastReport.UvIslandSummaries.Count, 20);
                    for (int i = 0; i < visibleCount; i++)
                        EditorGUILayout.LabelField(lastReport.UvIslandSummaries[i], EditorStyles.miniLabel);
                    if (lastReport.UvIslandSummaries.Count > visibleCount)
                        EditorGUILayout.LabelField("... and " + (lastReport.UvIslandSummaries.Count - visibleCount) + " more island(s).", EditorStyles.miniLabel);
                }
            }

            if (lastReport.SameIslandOverlapSummaries.Count > 0)
            {
                showSameIslandOverlapDetails = EditorGUILayout.Foldout(
                    showSameIslandOverlapDetails,
                    "Overlaps Within Connected Islands (" + lastReport.SameIslandOverlappingTrianglePairCount + ")",
                    true);
                if (showSameIslandOverlapDetails)
                {
                    int visibleCount = Mathf.Min(lastReport.SameIslandOverlapSummaries.Count, 20);
                    for (int i = 0; i < visibleCount; i++)
                        EditorGUILayout.LabelField(lastReport.SameIslandOverlapSummaries[i], EditorStyles.miniLabel);
                    if (lastReport.SameIslandOverlapSummaries.Count > visibleCount)
                        EditorGUILayout.LabelField("... and more internal overlap pair(s) not listed.", EditorStyles.miniLabel);
                }
            }

            if (lastReport.UvIslandOverlapSummaries.Count > 0)
            {
                showUvIslandOverlapDetails = EditorGUILayout.Foldout(
                    showUvIslandOverlapDetails,
                    "Island Overlap Pairs (" + lastReport.OverlappingUvIslandPairCount + ")",
                    true);
                if (showUvIslandOverlapDetails)
                {
                    int visibleCount = Mathf.Min(lastReport.UvIslandOverlapSummaries.Count, 20);
                    for (int i = 0; i < visibleCount; i++)
                        EditorGUILayout.LabelField(lastReport.UvIslandOverlapSummaries[i], EditorStyles.miniLabel);
                    if (lastReport.UvIslandOverlapSummaries.Count > visibleCount)
                        EditorGUILayout.LabelField("... and more overlap pair(s) not listed.", EditorStyles.miniLabel);
                }
            }

            foreach (string error in lastReport.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in lastReport.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            foreach (string info in lastReport.Infos)
                EditorGUILayout.HelpBox(info, MessageType.Info);
        }

        private void DrawUvGutterPaddingSection()
        {
            showUvGutterPaddingSection = EditorGUILayout.Foldout(
                showUvGutterPaddingSection, "UV Gutter Padding", true);
            if (!showUvGutterPaddingSection)
                return;

            EditorGUILayout.HelpBox(
                "Copies the nearest UV-island edge texels outward into unused mask-page space. " +
                "It preserves every pixel covered by the mesh and copies all four RGBA channels together. " +
                "The operation is one Undo step; use Save Mask PNG to persist it. It does not modify mesh UVs.",
                MessageType.Info);

            uvGutterPaddingPixels = EditorGUILayout.IntSlider(
                "Gutter Width (Pixels)", uvGutterPaddingPixels, 1, 32);

            SurfaceLayerDefinition selectedLayer = GetSelectedLayer();
            bool sessionReady = target != null && profile != null &&
                                target.TryGetMesh(out Mesh _) &&
                                target.UvChannel == 0 &&
                                IsSessionForLayer(selectedLayer) &&
                                !isPainting;

            if (!sessionReady)
            {
                EditorGUILayout.HelpBox(
                    target != null && target.UvChannel != 0
                        ? "The current painter and mask shader use UV0. Set the target UV Channel to 0 before applying gutter padding."
                        : "Select a valid mesh target and load the active mask page for the selected layer before applying gutter padding.",
                    MessageType.Warning);
            }

            if (sessionReady)
            {
                long pixelCount = (long)gpuEngine.Width * gpuEngine.Height;
                long queuePixels = Math.Min(pixelCount, 2000000L);
                long estimatedCpuBytes = pixelCount * 11L + queuePixels * sizeof(int);
                EditorGUILayout.LabelField("Estimated CPU staging memory", FormatBytes(estimatedCpuBytes));
                if (gpuEngine.Width >= 4096 || gpuEngine.Height >= 4096)
                {
                    EditorGUILayout.HelpBox(
                        "4096 x 4096 mask processing can require roughly 200 MiB of temporary CPU memory in addition to the active GPU buffers. Verify at 512/1024/2048 first and profile 4096 separately.",
                        MessageType.Warning);
                }
            }

            using (new EditorGUI.DisabledScope(!sessionReady))
            {
                if (GUILayout.Button("Apply UV Gutter Padding to Active Mask"))
                    ApplyUvGutterPadding();
            }
        }


        private void DrawMaskDilationSection()
        {
            showMaskDilationSection = EditorGUILayout.Foldout(
                showMaskDilationSection, "UV-Constrained Mask Dilation", true);
            if (!showMaskDilationSection)
                return;

            SurfaceLayerDefinition selectedLayer = GetSelectedLayer();
            EditorGUILayout.HelpBox(
                "Expands the selected layer's non-zero mask values into nearby texels covered by the mesh UV islands. " +
                "Only the selected RGBA channel is changed; pixels outside the mesh UVs and other channels are preserved. " +
                "This changes the painted surface mask (unlike UV Gutter Padding, which copies values outside islands). " +
                "The operation is one Undo step; use Save Mask PNG to persist it.",
                MessageType.Info);

            maskDilationRadiusPixels = EditorGUILayout.IntSlider(
                "Dilation Radius (Pixels)", maskDilationRadiusPixels, 1, 32);
            if (selectedLayer != null)
            {
                EditorGUILayout.LabelField(
                    "Target Channel", selectedLayer.MaskChannel.ToString());
            }

            bool sessionReady = target != null && profile != null && selectedLayer != null &&
                                target.TryGetMesh(out Mesh _) && target.UvChannel == 0 &&
                                IsSessionForLayer(selectedLayer) && !isPainting;
            if (!sessionReady)
            {
                EditorGUILayout.HelpBox(
                    target != null && target.UvChannel != 0
                        ? "The current mask workflow uses UV0. Set the target UV Channel to 0 before applying mask dilation."
                        : "Select a valid mesh target and load the selected layer's mask page before applying mask dilation.",
                    MessageType.Warning);
            }

            if (sessionReady)
            {
                long pixelCount = (long)gpuEngine.Width * gpuEngine.Height;
                EditorGUILayout.LabelField(
                    "Estimated additional processor memory",
                    FormatBytes(pixelCount * 3L + (long)Mathf.Max(gpuEngine.Width, gpuEngine.Height) * 8L));
                if (gpuEngine.Width >= 4096 || gpuEngine.Height >= 4096)
                {
                    EditorGUILayout.HelpBox(
                        "At 4096 x 4096, UV ownership and channel staging require about 48 MiB of additional CPU memory, " +
                        "excluding the GPU buffers, readback texture and Color32 readback array. Verify at 512/1024/2048 first.",
                        MessageType.Warning);
                }
            }

            using (new EditorGUI.DisabledScope(!sessionReady))
            {
                if (GUILayout.Button("Apply Mask Dilation to Selected Channel") &&
                    EditorUtility.DisplayDialog(
                        "SurfaceWeaver Mask Dilation",
                        "Expand the existing values in the selected " + selectedLayer.MaskChannel +
                        " channel by " + maskDilationRadiusPixels +
                        " pixel(s), constrained to the target's UV islands?\n\n" +
                        "This changes the painted surface mask. You can Undo this operation before saving.",
                        "Dilate", "Cancel"))
                {
                    ApplyMaskDilation();
                }
            }
        }

        private void ApplyMaskDilation()
        {
            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (target == null || profile == null || layer == null || gpuEngine == null ||
                !gpuEngine.IsInitialized || !target.TryGetMesh(out Mesh mesh) ||
                target.UvChannel != 0 || !IsSessionForLayer(layer))
            {
                paintStatus = "Load the selected layer's mask page before applying mask dilation.";
                Repaint();
                return;
            }

            int channelIndex = (int)layer.MaskChannel;
            if (channelIndex < 0 || channelIndex > 3)
            {
                paintStatus = "The selected layer does not reference a valid RGBA mask channel.";
                Repaint();
                return;
            }

            StopStroke();
            Texture2D readback = gpuEngine.CreateReadbackTexture(out string error);
            if (readback == null)
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver Mask Dilation", error, "OK");
                Repaint();
                return;
            }

            Color32[] pixels;
            try
            {
                pixels = readback.GetPixels32();
            }
            catch (Exception exception)
            {
                error = "Could not read the active mask pixels: " + exception.Message;
                DestroyImmediate(readback);
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver Mask Dilation", error, "OK");
                Repaint();
                return;
            }
            DestroyImmediate(readback);

            SurfaceUvMaskDilationProcessor.Result result =
                default(SurfaceUvMaskDilationProcessor.Result);
            bool dilationSucceeded = false;
            try
            {
                dilationSucceeded = SurfaceUvMaskDilationProcessor.TryApplyDilation(
                    mesh,
                    target.UvChannel,
                    pixels,
                    gpuEngine.Width,
                    gpuEngine.Height,
                    channelIndex,
                    maskDilationRadiusPixels,
                    out result,
                    out error);
            }
            catch (Exception exception)
            {
                error = "Mask dilation failed safely before the mask was updated: " + exception.Message;
                dilationSucceeded = false;
            }

            if (!dilationSucceeded)
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver Mask Dilation", error, "OK");
                Repaint();
                return;
            }

            if (result.ChangedPixels == 0)
            {
                paintStatus = "No additional covered texels needed changing for the selected " + layer.MaskChannel +
                              " channel at the requested radius. " +
                              "No Undo state was created.";
                Repaint();
                return;
            }

            if (!gpuEngine.ApplyPixelChanges(pixels, out error))
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver Mask Dilation", error, "OK");
                Repaint();
                return;
            }

            hasUnsavedChanges = true;
            BindWorkingMaskPreview();
            paintStatus = "Dilated " + layer.MaskChannel + " across " + result.ChangedPixels.ToString("N0") +
                          " surface texel(s) from " + result.SeedPixels.ToString("N0") +
                          " painted seed texel(s) on " + result.IslandCount +
                          " UV island(s). Other channels and pixels outside the UV islands were preserved. " +
                          "One Undo step is available; save the mask page to persist it.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void ApplyUvGutterPadding()
        {
            if (target == null || profile == null || gpuEngine == null || !gpuEngine.IsInitialized ||
                !target.TryGetMesh(out Mesh mesh) || target.UvChannel != 0 || !IsSessionForLayer(GetSelectedLayer()))
            {
                paintStatus = "Load the selected layer's mask page before applying UV gutter padding.";
                Repaint();
                return;
            }

            StopStroke();
            Texture2D readback = gpuEngine.CreateReadbackTexture(out string error);
            if (readback == null)
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver UV Gutter Padding", error, "OK");
                Repaint();
                return;
            }

            Color32[] pixels;
            try
            {
                pixels = readback.GetPixels32();
            }
            catch (Exception exception)
            {
                error = "Could not read the active mask pixels: " + exception.Message;
                DestroyImmediate(readback);
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver UV Gutter Padding", error, "OK");
                Repaint();
                return;
            }
            DestroyImmediate(readback);

            // Initialize the result before entering the try block. If TryApplyPadding throws
            // before assigning its out parameter, the catch path still returns safely and
            // C# definite-assignment analysis can prove every later read is valid.
            SurfaceUvGutterProcessor.Result result = default(SurfaceUvGutterProcessor.Result);
            bool paddingSucceeded = false;
            try
            {
                paddingSucceeded = SurfaceUvGutterProcessor.TryApplyPadding(
                    mesh,
                    target.UvChannel,
                    pixels,
                    gpuEngine.Width,
                    gpuEngine.Height,
                    uvGutterPaddingPixels,
                    out result,
                    out error);
            }
            catch (Exception exception)
            {
                error = "UV gutter processing failed safely before the mask was updated: " + exception.Message;
                paddingSucceeded = false;
            }

            if (!paddingSucceeded)
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver UV Gutter Padding", error, "OK");
                Repaint();
                return;
            }

            if (!gpuEngine.ApplyPixelChanges(pixels, out error))
            {
                paintStatus = error;
                EditorUtility.DisplayDialog("SurfaceWeaver UV Gutter Padding", error, "OK");
                Repaint();
                return;
            }

            hasUnsavedChanges = true;
            BindWorkingMaskPreview();
            paintStatus = "Added " + result.PaddedPixels.ToString("N0") +
                          " gutter texels around " + result.IslandCount +
                          " UV island(s). The operation is one Undo step; save the mask page to keep it.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawMipMapBleedingPreventionSection()
        {
            showMipMapBleedingPreventionSection = EditorGUILayout.Foldout(
                showMipMapBleedingPreventionSection, "Mip-map Bleeding Prevention", true);
            if (!showMipMapBleedingPreventionSection)
                return;

            EditorGUILayout.HelpBox(
                "Mask pages use bleed-safe sampling: mipmaps are disabled for packed RGBA mask textures, " +
                "and the URP layered shader samples the mask at mip level 0. This prevents generated lower mip levels " +
                "from averaging mask values across neighboring UV islands. Bilinear filtering and Clamp wrapping remain enabled. " +
                "Albedo textures keep their normal mipmaps. Trade-off: very distant/minified surfaces can show more mask-edge aliasing; " +
                "test this on the target camera and mobile device.",
                MessageType.Info);

            Texture2D maskPage = activeMaskAsset;
            if (maskPage == null && profile != null)
            {
                SurfaceLayerDefinition selectedLayer = GetSelectedLayer();
                if (selectedLayer != null)
                    maskPage = profile.GetMaskPage(selectedLayer.MaskPageIndex);
            }

            if (maskPage == null)
            {
                EditorGUILayout.HelpBox(
                    "Load a mask page or assign a mask page to the selected profile layer to inspect its import settings.",
                    MessageType.Warning);
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(maskPage);
            TextureImporter importer = string.IsNullOrEmpty(assetPath)
                ? null
                : AssetImporter.GetAtPath(assetPath) as TextureImporter;

            EditorGUILayout.LabelField("Mask Page", maskPage.name);
            if (importer == null)
            {
                EditorGUILayout.HelpBox(
                    "The selected mask page is not an imported texture asset. Bleed-safe importer settings can only be applied to PNG or other imported Texture2D assets.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                "Mip-map Status",
                importer.mipmapEnabled ? "Enabled — mip bleeding risk" : "Disabled — bleed-safe");
            EditorGUILayout.LabelField("Filter / Wrap", importer.filterMode + " / " + importer.wrapMode);
            EditorGUILayout.LabelField("Color Space / Compression",
                (importer.sRGBTexture ? "sRGB" : "Linear") + " / " + importer.textureCompression);

            if (importer.mipmapEnabled)
            {
                EditorGUILayout.HelpBox(
                    "This mask page currently has mipmaps enabled. Saving it through SurfaceWeaver will apply the bleed-safe policy automatically. " +
                    "You can also migrate the existing asset now without changing its mask pixels.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This mask page has mipmaps disabled. The layered shader also samples mask level 0 as a safeguard.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(assetPath)))
            {
                if (GUILayout.Button("Apply Bleed-Safe Import Settings to Current Mask Page") &&
                    EditorUtility.DisplayDialog(
                        "SurfaceWeaver Bleed-Safe Mask Settings",
                        "Apply these settings to the current mask texture asset?\n\n" +
                        "- Disable mipmaps\n" +
                        "- Use Linear color space\n" +
                        "- Use Bilinear filtering and Clamp wrapping\n" +
                        "- Use Uncompressed texture data\n\n" +
                        "The PNG pixel data and asset GUID are not intentionally changed. This may increase distant mask-edge aliasing.",
                        "Apply Settings", "Cancel"))
                {
                    Texture2D refreshed;
                    string error;
                    if (SurfaceMaskAssetUtility.ApplyBleedSafeSettings(maskPage, out refreshed, out error))
                    {
                        if (activeMaskAsset == maskPage)
                            activeMaskAsset = refreshed;
                        if (gpuEngine != null && gpuEngine.IsInitialized)
                            BindWorkingMaskPreview();
                        paintStatus = "Applied bleed-safe import settings to " + refreshed.name + ". Mask mipmaps are disabled.";
                        Repaint();
                        SceneView.RepaintAll();
                    }
                    else
                    {
                        paintStatus = error;
                        EditorUtility.DisplayDialog("SurfaceWeaver Bleed-Safe Mask Settings", error, "OK");
                        Repaint();
                    }
                }
            }
        }

        private void DrawRoadmapSection()
        {
            EditorGUILayout.LabelField("Development Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "v0.7 UV quality and rendering: profile-driven multi-layer URP rendering, packed RGBA mask binding, per-layer material controls, " +
                "UV island grouping and mirrored/stacked/contained/partial overlap diagnostics. v0.7.9 adds island-aware UV gutter padding; " +
                "v0.7.10 adds selected-channel mask dilation constrained to UV islands (verify both in the consuming Unity project). " +
                "Remaining v0.7 work: mip-map bleeding prevention and mobile shader optimization.\n\n" +
                "v0.8 advanced authoring: editable spline handles/closed paths, variable width and pressure, " +
                "multi-mesh editing/batch export, object and vegetation scatter, Flood Fill and additional surface tools.\n\n" +
                "v0.9 quality: automated tests, Graphics API compatibility, large-mesh profiling, memory optimization and edge-case testing. " +
                "v1.0: professional Editor UI, Lite/Pro editions, demo content, documentation/tutorials and Asset Store release preparation.",
                MessageType.None);
        }
    }
}
