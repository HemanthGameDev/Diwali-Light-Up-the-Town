using System;
using System.IO;
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
        private int undoMemoryBudgetMb = 64;
        private Color previewColor = new Color(1f, 0.65f, 0.12f, 0.95f);
        private Texture2D directBrushAlpha;
        private SurfaceBrushPreset brushPreset;
        private int operationToolbar;
        private int selectedLayerIndex;
        private string maskPreviewProperty = "_PaintMask";

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
            RestoreSavedMaskPreview();
            DisposePaintSession();
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
            RestoreSavedMaskPreview();
            DisposePaintSession();
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
                "GPU mask painting milestone: paint or erase one packed RGBA mask channel on a custom mesh. " +
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
                RestoreSavedMaskPreview();
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
            operationToolbar = GUILayout.Toolbar(operationToolbar, new[] { "Paint", "Erase" });
            paintingEnabled = EditorGUILayout.ToggleLeft("Enable Scene View Painting", paintingEnabled);
            brushRadius = EditorGUILayout.Slider("Radius (World Units)", brushRadius, 0.05f, 20f);
            brushOpacity = EditorGUILayout.Slider("Opacity", brushOpacity, 0.01f, 1f);
            brushHardness = EditorGUILayout.Slider("Hardness", brushHardness, 0f, 1f);
            brushSpacing = EditorGUILayout.Slider("Spacing (Diameter)", brushSpacing, 0.05f, 1f);
            brushRotation = EditorGUILayout.Slider("Brush Rotation", brushRotation, 0f, 360f);
            brushScaleJitter = EditorGUILayout.Slider("Scale Randomness", brushScaleJitter, 0f, 0.75f);
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
                Rect previewRect = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(previewRect, gpuEngine.Current, null, ScaleMode.ScaleToFit);

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
                    if (GUILayout.Button("Clear Selected Layer Channel") &&
                        EditorUtility.DisplayDialog("Clear Layer Mask", "Clear only the selected layer channel? Other channels on this page will be preserved. This can be undone.", "Clear", "Cancel"))
                    {
                        StopStroke();
                        string error;
                        if (gpuEngine.BeginStroke(out error))
                        {
                            if (gpuEngine.ClearChannel((int)layer.MaskChannel, out error))
                            {
                                gpuEngine.EndStroke();
                                hasUnsavedChanges = true;
                                BindWorkingMaskPreview();
                                paintStatus = "Selected layer channel cleared. Undo is available until the session is closed.";
                            }
                            else
                            {
                                gpuEngine.CancelStroke(out _);
                                paintStatus = error;
                            }
                        }
                        else
                        {
                            paintStatus = error;
                        }
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Hold the left mouse button over the target in Scene View to paint. Hold Alt for normal Scene View navigation. " +
                "Paint writes white into the selected channel; Erase writes black. Ctrl/Cmd+Z undoes and Ctrl/Cmd+Y (or Shift+Z) redoes. Save Mask PNG to persist changes.",
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
            if (gpuEngine == null || !gpuEngine.IsInitialized || profile == null)
                return;

            SurfaceLayerDefinition layer = GetSelectedLayer();
            if (layer == null || !layer.Enabled)
                return;

            Vector3 worldPerUvU;
            Vector3 worldPerUvV;
            string metricError;
            if (!SurfaceUvMetric.TryGetWorldPerUv(
                    currentHit, out worldPerUvU, out worldPerUvV, out metricError))
            {
                paintStatus = metricError;
                hasPreviousPaintHit = false;
                Repaint();
                return;
            }

            bool erase = operationToolbar == 1;
            Texture2D alpha = GetActiveBrushAlpha();

            if (!hasPreviousPaintHit || previousPaintHit.Target != currentHit.Target ||
                HasUvDiscontinuity(previousPaintHit.Uv0, currentHit.Uv0))
            {
                Stamp(currentHit.Uv0, worldPerUvU, worldPerUvV, layer, erase, alpha);
            }
            else
            {
                float worldDistance = Vector3.Distance(previousPaintHit.Point, currentHit.Point);
                float spacingDistance = Mathf.Max(
                    0.01f,
                    brushRadius * (1f + brushScaleJitter) * 2f * brushSpacing);
                int steps = Mathf.Clamp(Mathf.CeilToInt(worldDistance / spacingDistance), 1, 256);

                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector2 uv = Vector2.Lerp(previousPaintHit.Uv0, currentHit.Uv0, t);
                    Stamp(uv, worldPerUvU, worldPerUvV, layer, erase, alpha);
                }
            }

            previousPaintHit = currentHit;
            hasPreviousPaintHit = true;
            BindWorkingMaskPreview();
            hasUnsavedChanges = true;
            paintStatus = erase
                ? "Erasing selected layer. Save the mask page to persist changes."
                : "Painting selected layer. Save the mask page to persist changes.";
            Repaint();
        }

        private void Stamp(
            Vector2 uv,
            Vector3 worldPerUvU,
            Vector3 worldPerUvV,
            SurfaceLayerDefinition layer,
            bool erase,
            Texture2D alpha)
        {
            float rotation = brushRotation;
            if (brushPreset != null && brushPreset.RandomRotation)
                rotation = UnityEngine.Random.Range(0f, 360f);

            float stampRadius = brushRadius;
            if (brushScaleJitter > 0f)
                stampRadius *= UnityEngine.Random.Range(1f - brushScaleJitter, 1f + brushScaleJitter);

            string error;
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
                    out error))
            {
                paintStatus = error;
            }
        }

        private Texture2D GetActiveBrushAlpha()
        {
            if (directBrushAlpha != null)
                return directBrushAlpha;
            return brushPreset != null ? brushPreset.AlphaTexture : null;
        }

        private static bool HasUvDiscontinuity(Vector2 previous, Vector2 current)
        {
            Vector2 delta = current - previous;
            return Mathf.Abs(delta.x) > 0.35f || Mathf.Abs(delta.y) > 0.35f;
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
            return paintingEnabled && IsSessionForLayer(layer) &&
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
            if (target == null || string.IsNullOrWhiteSpace(maskPreviewProperty))
                return;

            MeshRenderer renderer;
            if (!target.TryGetRenderer(out renderer))
                return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture(maskPreviewProperty, texture);
            renderer.SetPropertyBlock(block);
            EditorUtility.SetDirty(renderer);
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
                "Included: custom mesh targets, reusable profiles, first-pass UV diagnostics, Scene View picking, " +
                "world-space brush preview, GPU paint/erase stamps, packed RGBA page selection and PNG persistence.\n\n" +
                "Next: robust stroke undo/redo, UV seam/overlap diagnostics, brush scatter/jitter, smoothing and production shader integration.",
                MessageType.None);
        }
    }
}
