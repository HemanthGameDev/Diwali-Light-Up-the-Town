using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Multi-target brush painting workspace. Each target owns an independent GPU mask session;
    /// one drag can touch several targets and is grouped as one logical Undo/Redo transaction.
    /// Spline authoring remains in the original single-target Painter.
    /// </summary>
    public sealed class SurfaceWeaverMultiMeshPainterWindow : EditorWindow
    {
        private sealed class TargetSession : IDisposable
        {
            public SurfacePaintTarget Target;
            public SurfacePaintProfile Profile;
            public Mesh Mesh;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
            public int LayerIndex;
            public SurfaceLayerDefinition Layer;
            public int MaskPageIndex;
            public Texture2D MaskAsset;
            public SurfaceGpuBrushEngine Engine;
            public SurfaceUvRasterMesh RasterMesh;
            public bool Dirty;
            public bool DirtyBeforeStroke;
            public Material OriginalMaterial;
            public Material PreviewMaterial;
            public int PreviewMaterialIndex = -1;
            public string PreviewProperty;
            public string Status;

            public void Dispose()
            {
                RestorePreview();
                if (RasterMesh != null)
                {
                    RasterMesh.Dispose();
                    RasterMesh = null;
                }
                if (Engine != null)
                {
                    Engine.Dispose();
                    Engine = null;
                }
            }

            public void RestorePreview()
            {
                if (Renderer != null && PreviewMaterial != null)
                {
                    Material[] materials = Renderer.sharedMaterials;
                    if (PreviewMaterialIndex >= 0 && PreviewMaterialIndex < materials.Length &&
                        materials[PreviewMaterialIndex] == PreviewMaterial)
                    {
                        materials[PreviewMaterialIndex] = OriginalMaterial;
                        Renderer.sharedMaterials = materials;
                    }
                }

                if (PreviewMaterial != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(PreviewMaterial);
                    else
                        UnityEngine.Object.DestroyImmediate(PreviewMaterial);
                }
                PreviewMaterial = null;
                OriginalMaterial = null;
                PreviewMaterialIndex = -1;
                PreviewProperty = null;
            }
            
        }

        private sealed class TargetRow
        {
            public string GlobalId;
            public SurfacePaintTarget Target;
            public int LayerIndex;
            public bool IsValid;
            public string Status;
            public TargetSession Session;
        }

        private sealed class StrokeGroup
        {
            public readonly List<TargetSession> Sessions = new List<TargetSession>();
        }

        private SurfacePaintTargetSet targetSet;
        private readonly List<TargetRow> rows = new List<TargetRow>();
        private readonly List<TargetSession> strokeTouched = new List<TargetSession>();
        private readonly List<StrokeGroup> undoGroups = new List<StrokeGroup>();
        private readonly List<StrokeGroup> redoGroups = new List<StrokeGroup>();
        private Vector2 scroll;
        private string report = "Assign a Target Set and validate its targets to begin.";
        private bool paintingArmed;
        private bool isPainting;
        private int paintingControlId;
        private bool hasPreviousHit;
        private TargetSession previousHitSession;
        private SurfaceSceneHit previousHit;
        private Vector2 previousMousePosition;
        private int operation;
        private SurfaceBrushPreset brushPreset;
        private float brushRadius = 2f;
        private float brushOpacity = 0.5f;
        private float brushHardness = 0.5f;
        private float brushSpacing = 0.2f;
        private float brushScaleJitter;
        private float brushRotation;
        private bool useNormalFilter;
        private float normalCutoff = 0.5f;
        private float projectionDepthScale = 2f;
        private int totalUndoBudgetMb = 128;
        private Color previewColor = new Color(0.2f, 0.85f, 1f, 0.8f);
        private bool showOnlyReady = false;
        private readonly HashSet<SurfacePaintTarget> painterSessionsPrepared = new HashSet<SurfacePaintTarget>();

        [MenuItem("Window/SurfaceWeaver/Multi-Mesh Painter", false, 2111)]
        public static void Open()
        {
            SurfaceWeaverMultiMeshPainterWindow window = GetWindow<SurfaceWeaverMultiMeshPainterWindow>();
            window.titleContent = new GUIContent("SW Multi-Painter");
            window.minSize = new Vector2(470f, 560f);
            window.Show();
        }

        public static void OpenFor(SurfacePaintTargetSet set)
        {
            Open();
            SurfaceWeaverMultiMeshPainterWindow window = GetWindow<SurfaceWeaverMultiMeshPainterWindow>();
            if (window.targetSet != set)
            {
                if (window.HasUnsavedChanges())
                {
                    bool save = EditorUtility.DisplayDialog(
                        "Save Multi-Mesh Changes?",
                        "The current multi-mesh workspace has unsaved mask changes. Save them before switching target sets?",
                        "Save and Switch", "Cancel");
                    if (!save || !window.SaveAllChanges(false))
                    {
                        window.Repaint();
                        return;
                    }
                }
                else if (!window.SaveAllChanges(false))
                {
                    window.report = "Could not switch target sets because one or more masks failed to save.";
                    window.Repaint();
                    return;
                }
                window.DisposeRows();
                window.targetSet = set;
                window.RefreshTargets();
            }
            window.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            if (targetSet != null && rows.Count == 0)
                RefreshTargets();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            FinishStroke();
            if (HasUnsavedChanges())
            {
                if (!SaveAllChanges(false))
                    Debug.LogWarning("SurfaceWeaver Multi-Mesh Painter: one or more mask sessions could not be auto-saved when the window closed. Reopen the workspace and save them manually.");
            }

            DisposeRows();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("SURFACEWEAVER — MULTI-MESH PAINTER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("One continuous brush stroke can paint across registered meshes. Each target keeps its own mask, preview and history.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            DrawTargetSetSection();
            EditorGUILayout.Space(8f);
            DrawTargetRows();
            EditorGUILayout.Space(8f);
            DrawBrushControls();
            EditorGUILayout.Space(8f);
            DrawActions();
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Arm Scene Painting, then drag across any ready target in the Scene View. A stroke that crosses meshes is committed as one grouped Undo step. Each target must use a distinct saved mask-page asset. The original Painter and spline editor remain single-target and are not modified by this workspace.",
                MessageType.Info);
            if (!string.IsNullOrEmpty(report))
                EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSetSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                SurfacePaintTargetSet next = (SurfacePaintTargetSet)EditorGUILayout.ObjectField(
                    "Target Set", targetSet, typeof(SurfacePaintTargetSet), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (HasUnsavedChanges() && !EditorUtility.DisplayDialog(
                            "Save Multi-Mesh Changes?",
                            "The current multi-mesh painting session contains unsaved mask changes. Save them before switching target sets?",
                            "Save and Switch", "Cancel"))
                    {
                        next = targetSet;
                    }
                    else
                    {
                        if (!SaveAllChanges(false))
                        {
                            report = "Could not switch target sets because one or more masks failed to save. Resolve the row errors and retry.";
                            next = targetSet;
                        }
                        else
                        {
                            DisposeRows();
                            targetSet = next;
                            RefreshTargets();
                        }
                    }
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                {
                    FinishStroke();
                    if (!HasUnsavedChanges() || EditorUtility.DisplayDialog(
                            "Refresh Multi-Mesh Targets",
                            "Save unsaved mask changes before refreshing the target list?",
                            "Save and Refresh", "Cancel"))
                    {
                        if (SaveAllChanges(false))
                        {
                            DisposeRows();
                            RefreshTargets();
                        }
                        else
                        {
                            report = "Refresh cancelled because one or more masks failed to save. Resolve the row errors and retry.";
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find All Loaded in Set"))
                {
                    if (targetSet != null && (!HasUnsavedChanges() || EditorUtility.DisplayDialog(
                            "Save Multi-Mesh Changes?",
                            "Save all dirty mask pages before adding loaded scene targets?",
                            "Save and Continue", "Cancel")))
                    {
                        if (!SaveAllChanges(false))
                        {
                            report = "Could not discover additional targets because one or more masks failed to save. Resolve the row errors and retry.";
                            Repaint();
                            return;
                        }
                        SurfacePaintTarget[] all = Resources.FindObjectsOfTypeAll<SurfacePaintTarget>();
                        int added = 0;
                        for (int i = 0; i < all.Length; i++)
                        {
                            SurfacePaintTarget candidate = all[i];
                            if (candidate == null || EditorUtility.IsPersistent(candidate) ||
                                !candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded ||
                                string.IsNullOrEmpty(candidate.gameObject.scene.path))
                                continue;
                            string id = GlobalObjectId.GetGlobalObjectIdSlow(candidate).ToString();
                            if (targetSet.Add(id)) added++;
                        }
                        EditorUtility.SetDirty(targetSet);
                        AssetDatabase.SaveAssets();
                        DisposeRows();
                        RefreshTargets();
                        report = "Added " + added + " loaded scene target(s) to the set.";
                    }
                }
                if (GUILayout.Button("Select All Targets"))
                {
                    Selection.objects = rows.Where(r => r.Target != null)
                        .Select(r => (UnityEngine.Object)r.Target.gameObject).ToArray();
                }
            }

            showOnlyReady = EditorGUILayout.ToggleLeft("Show only ready targets", showOnlyReady);
            EditorGUILayout.LabelField("Targets: " + rows.Count + "   Ready: " + rows.Count(r => r.IsValid), EditorStyles.boldLabel);
        }

        private void DrawTargetRows()
        {
            EditorGUILayout.LabelField("Per-Target Layer Mapping", EditorStyles.boldLabel);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("The assigned Target Set is empty or has no resolved scene targets.", MessageType.Info);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow row = rows[i];
                if (showOnlyReady && !row.IsValid)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(row.Target != null ? row.Target.name : "Missing scene target", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (row.Target != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                            EditorGUIUtility.PingObject(row.Target.gameObject);
                    }

                    if (row.Target == null)
                    {
                        EditorGUILayout.HelpBox(row.Status, MessageType.Warning);
                        continue;
                    }

                    SurfacePaintProfile profile = row.Target.Profile;
                    if (profile != null && profile.PaintLayers != null && profile.PaintLayers.Count > 0)
                    {
                        string[] labels = profile.PaintLayers.Select((l, index) =>
                            l != null ? index + ": " + l.DisplayName + " [P" + l.MaskPageIndex + "/" + l.MaskChannel + "]" : index + ": Missing Layer").ToArray();
                        using (new EditorGUI.DisabledScope(isPainting || (row.Session != null && row.Session.Dirty)))
                        {
                            EditorGUI.BeginChangeCheck();
                            int nextIndex = EditorGUILayout.Popup("Paint Layer", Mathf.Clamp(row.LayerIndex, 0, labels.Length - 1), labels);
                            if (EditorGUI.EndChangeCheck())
                            {
                                row.LayerIndex = nextIndex;
                                if (row.Session != null)
                                {
                                    row.Session.Dispose();
                                    row.Session = null;
                                }
                                if (painterSessionsPrepared.Remove(row.Target))
                                    SurfaceWeaverWindow.ResumeAfterMultiMeshPainting(row.Target);
                                ValidateRows();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(row.Status))
                        EditorGUILayout.HelpBox(row.Status, row.IsValid ? MessageType.None : MessageType.Warning);
                    if (row.Session != null)
                        EditorGUILayout.LabelField("Session", (row.Session.Engine != null ? "Loaded" : "Not loaded") +
                    (row.Session.Dirty ? " — Save required" : " — Clean"));
                }
            }
        }

        private void DrawBrushControls()
        {
            EditorGUILayout.LabelField("Multi-Mesh Brush", EditorStyles.boldLabel);
            operation = GUILayout.Toolbar(operation, new[] { "Paint", "Erase", "Smooth" });

            EditorGUI.BeginChangeCheck();
            brushPreset = (SurfaceBrushPreset)EditorGUILayout.ObjectField("Brush Preset", brushPreset, typeof(SurfaceBrushPreset), false);
            if (EditorGUI.EndChangeCheck() && brushPreset != null)
            {
                SurfaceBrushSettings defaults = brushPreset.Defaults;
                brushRadius = Mathf.Clamp(defaults.RadiusWorldUnits, 0.05f, 20f);
                brushOpacity = Mathf.Clamp01(defaults.Opacity);
                brushHardness = Mathf.Clamp01(defaults.Hardness);
                brushSpacing = Mathf.Clamp(defaults.Spacing, 0.01f, 2f);
                brushScaleJitter = Mathf.Clamp(brushPreset.ScaleJitter, 0f, 0.75f);
            }

            brushRadius = EditorGUILayout.Slider("Radius (world units)", brushRadius, 0.05f, 20f);
            brushOpacity = EditorGUILayout.Slider("Opacity", brushOpacity, 0f, 1f);
            brushHardness = EditorGUILayout.Slider("Hardness", brushHardness, 0f, 1f);
            brushSpacing = EditorGUILayout.Slider("Spacing", brushSpacing, 0.05f, 1f);
            brushScaleJitter = EditorGUILayout.Slider("Scale Jitter", brushScaleJitter, 0f, 0.75f);
            brushRotation = EditorGUILayout.Slider("Rotation", brushRotation, 0f, 360f);
            useNormalFilter = EditorGUILayout.ToggleLeft("Filter by surface normal", useNormalFilter);
            using (new EditorGUI.DisabledScope(!useNormalFilter))
            {
                normalCutoff = EditorGUILayout.Slider("Normal Cutoff", normalCutoff, -1f, 1f);
            }
            projectionDepthScale = EditorGUILayout.Slider("Projection Depth × Radius", projectionDepthScale, 0.25f, 8f);
            totalUndoBudgetMb = EditorGUILayout.IntSlider("Total Undo Budget (MB)", totalUndoBudgetMb, 32, 512);
            EditorGUILayout.HelpBox("Undo memory is divided across the targets touched by this workspace. Only targets actually touched by a stroke allocate a GPU session.", MessageType.None);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color old = GUI.color;
                GUI.color = paintingArmed ? new Color(0.55f, 1f, 0.6f) : old;
                if (GUILayout.Button(paintingArmed ? "Disarm Scene Painting" : "Arm Scene Painting", GUILayout.Height(30f)))
                {
                    if (isPainting)
                        FinishStroke();
                    paintingArmed = !paintingArmed;
                    report = paintingArmed
                        ? "Scene painting armed. Drag over any ready target in Scene View."
                        : "Scene painting disarmed.";
                }
                GUI.color = old;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(isPainting || undoGroups.Count == 0))
                {
                    if (GUILayout.Button("Undo Stroke")) UndoGroupedStroke();
                }
                using (new EditorGUI.DisabledScope(isPainting || redoGroups.Count == 0))
                {
                    if (GUILayout.Button("Redo Stroke")) RedoGroupedStroke();
                }
                using (new EditorGUI.DisabledScope(!HasUnsavedChanges() || isPainting))
                {
                    if (GUILayout.Button("Save All Masks")) SaveAllChanges(true);
                }
            }

            if (isPainting)
                EditorGUILayout.HelpBox("Painting stroke active across " + strokeTouched.Count + " target(s). Release the mouse to commit, or press Escape to cancel the whole stroke.", MessageType.Info);
            if (HasUnsavedChanges())
                EditorGUILayout.HelpBox("There are unsaved mask changes. Save All Masks before closing or switching target sets. The window also attempts an automatic save when it closes.", MessageType.Warning);
        }

        private void RefreshTargets()
        {
            rows.Clear();
            if (targetSet == null || targetSet.TargetGlobalObjectIds == null)
            {
                Repaint();
                return;
            }

            for (int i = 0; i < targetSet.TargetGlobalObjectIds.Count; i++)
            {
                string id = targetSet.TargetGlobalObjectIds[i];
                SurfacePaintTarget target = Resolve(id);
                TargetRow row = new TargetRow
                {
                    GlobalId = id,
                    Target = target,
                    LayerIndex = 0
                };
                rows.Add(row);
            }
            ValidateRows();
            Repaint();
            SceneView.RepaintAll();
        }

        private void ValidateRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow row = rows[i];
                row.IsValid = ValidateTarget(row, out row.Status);
            }

            // A packed mask asset cannot safely be edited as two independent target sessions.
            // Require unique mask-page assets so each mesh has independent persistence and Undo.
            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow a = rows[i];
                if (!a.IsValid || a.Target == null)
                    continue;
                SurfaceLayerDefinition layerA = GetLayer(a);
                Texture2D maskA = layerA != null ? a.Target.Profile.GetMaskPage(layerA.MaskPageIndex) : null;
                if (maskA == null) continue;

                for (int j = i + 1; j < rows.Count; j++)
                {
                    TargetRow b = rows[j];
                    if (!b.IsValid || b.Target == null) continue;
                    SurfaceLayerDefinition layerB = GetLayer(b);
                    Texture2D maskB = layerB != null ? b.Target.Profile.GetMaskPage(layerB.MaskPageIndex) : null;
                    if (maskB != null && maskA == maskB)
                    {
                        a.IsValid = false;
                        b.IsValid = false;
                        a.Status = "Shares the selected mask-page asset with '" + b.Target.name + "'. Assign a separate mask PNG/profile page to each target before multi-mesh painting.";
                        b.Status = "Shares the selected mask-page asset with '" + a.Target.name + "'. Assign a separate mask PNG/profile page to each target before multi-mesh painting.";
                    }
                }
            }
        }

        private static bool ValidateTarget(TargetRow row, out string message)
        {
            message = string.Empty;
            if (row.Target == null)
            {
                message = "Scene target could not be resolved. Open its saved scene and refresh the workspace.";
                return false;
            }
            SurfacePaintTarget target = row.Target;
            if (!target.gameObject.activeInHierarchy)
            {
                message = "Target is inactive. Activate it before painting.";
                return false;
            }
            SurfacePaintProfile profile = target.Profile;
            if (profile == null)
            {
                message = "No SurfacePaintProfile is assigned.";
                return false;
            }
            if (profile.MaskStorageMode != SurfaceMaskStorageMode.PackedRgbaPages)
            {
                message = "Multi-Mesh Painter currently supports Packed RGBA Pages only.";
                return false;
            }
            if (profile.PaintLayers == null || row.LayerIndex < 0 || row.LayerIndex >= profile.PaintLayers.Count)
            {
                message = "Selected layer index is not available in this profile.";
                return false;
            }
            SurfaceLayerDefinition layer = profile.PaintLayers[row.LayerIndex];
            if (layer == null || !layer.Enabled)
            {
                message = "Selected layer is missing or disabled.";
                return false;
            }
            if (layer.MaskPageIndex != 0)
            {
                message = "The current v0.7 URP material supports mask page 0 only. Select a layer stored on page 0.";
                return false;
            }
            if (profile.GetMaskPage(layer.MaskPageIndex) == null)
            {
                message = "Selected layer has no saved mask-page texture. Create or assign its mask page first.";
                return false;
            }
            if (target.UvChannel != 0)
            {
                message = "Current multi-mesh painting requires UV0.";
                return false;
            }
            if (!target.TryGetMesh(out Mesh mesh) || mesh == null || !mesh.isReadable)
            {
                message = "Target needs a readable MeshFilter/sharedMesh with UV0.";
                return false;
            }
            if (!target.TryGetRenderer(out MeshRenderer renderer) || renderer == null)
            {
                message = "A MeshRenderer is required.";
                return false;
            }
            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null || collider.convex || collider.sharedMesh != mesh)
            {
                message = "A non-convex MeshCollider on the target, referencing the same mesh, is required.";
                return false;
            }
            message = "Ready — layer '" + layer.DisplayName + "', page 0 / " + layer.MaskChannel + ".";
            return true;
        }

        private static SurfaceLayerDefinition GetLayer(TargetRow row)
        {
            if (row == null || row.Target == null || row.Target.Profile == null ||
                row.Target.Profile.PaintLayers == null || row.LayerIndex < 0 ||
                row.LayerIndex >= row.Target.Profile.PaintLayers.Count)
                return null;
            return row.Target.Profile.PaintLayers[row.LayerIndex];
        }

        private static SurfacePaintTarget Resolve(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId globalId))
                return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as SurfacePaintTarget;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!paintingArmed || rows.Count == 0)
            {
                if (isPainting) FinishStroke();
                return;
            }

            Event evt = Event.current;
            if (evt == null || evt.alt)
            {
                if (isPainting && evt != null && evt.alt)
                    CancelStroke();
                return;
            }

            bool hitFound = TryPick(evt.mousePosition, out TargetRow hitRow, out SurfaceSceneHit hit);
            int controlId = GUIUtility.GetControlID("SurfaceWeaverMultiMeshPaint".GetHashCode(), FocusType.Passive);
            if (evt.type == EventType.Layout && (hitFound || isPainting))
                HandleUtility.AddDefaultControl(controlId);

            if (hitFound)
                SurfaceBrushPreview.Draw(hit, brushRadius * (1f + brushScaleJitter), previewColor);

            if (evt.type == EventType.MouseDown && evt.button == 0 && hitFound &&
                (GUIUtility.hotControl == 0 || GUIUtility.hotControl == controlId))
            {
                strokeTouched.Clear();
                hasPreviousHit = false;
                isPainting = true;
                paintingControlId = controlId;
                GUIUtility.hotControl = controlId;
                previousMousePosition = evt.mousePosition;
                ProcessHit(hitRow, hit);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0 && isPainting && GUIUtility.hotControl == paintingControlId)
            {
                ProcessScreenSegment(previousMousePosition, evt.mousePosition);
                previousMousePosition = evt.mousePosition;
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && isPainting && GUIUtility.hotControl == paintingControlId)
            {
                FinishStroke();
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && isPainting)
            {
                CancelStroke();
                evt.Use();
            }

            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag || evt.type == EventType.Repaint)
            {
                Repaint();
                sceneView.Repaint();
            }
        }

        private void ProcessScreenSegment(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / 3f), 1, 192);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 sample = Vector2.Lerp(from, to, i / (float)steps);
                if (TryPick(sample, out TargetRow row, out SurfaceSceneHit hit))
                    ProcessHit(row, hit);
                else
                    hasPreviousHit = false;
            }
        }

        private bool TryPick(Vector2 mousePosition, out TargetRow bestRow, out SurfaceSceneHit bestHit)
        {
            bestRow = null;
            bestHit = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow row = rows[i];
                if (!row.IsValid || row.Target == null)
                    continue;
                MeshCollider collider = row.Target.GetComponent<MeshCollider>();
                if (collider == null || !collider.enabled)
                    continue;
                if (!collider.Raycast(ray, out RaycastHit rayHit, 100000f) || rayHit.distance >= closest)
                    continue;
                closest = rayHit.distance;
                bestRow = row;
                bestHit = new SurfaceSceneHit(row.Target, collider, rayHit);
            }
            return bestRow != null;
        }

        private void ProcessHit(TargetRow row, SurfaceSceneHit hit)
        {
            if (row == null || !row.IsValid)
            {
                hasPreviousHit = false;
                return;
            }
            if (!EnsureSessionLoaded(row, out string loadError))
            {
                row.Status = loadError;
                row.IsValid = false;
                hasPreviousHit = false;
                Repaint();
                return;
            }

            TargetSession session = row.Session;
            if (!strokeTouched.Contains(session))
            {
                session.DirtyBeforeStroke = session.Dirty;
                session.Engine.SetHistoryBudgetMegabytes(Mathf.Max(8, totalUndoBudgetMb / Mathf.Max(1, rows.Count(r => r.IsValid))));
                if (!session.Engine.BeginStroke(out string beginError))
                {
                    row.Status = beginError;
                    hasPreviousHit = false;
                    return;
                }
                strokeTouched.Add(session);
            }

            bool stamped = false;
            if (hasPreviousHit && previousHitSession == session)
            {
                float worldDistance = Vector3.Distance(previousHit.Point, hit.Point);
                float spacingDistance = Mathf.Max(0.01f, brushRadius * (1f - brushScaleJitter) * 2f * brushSpacing);
                int steps = Mathf.Clamp(Mathf.CeilToInt(worldDistance / spacingDistance), 1, 256);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector3 point = Vector3.Lerp(previousHit.Point, hit.Point, t);
                    Vector3 normal = Vector3.Slerp(previousHit.Normal, hit.Normal, t).normalized;
                    float projectionDepth = Mathf.Max(0.05f, brushRadius * projectionDepthScale);
                    if (TryProjectToTarget(session.Collider, point, normal, projectionDepth, out RaycastHit projected))
                    {
                        if (!Stamp(session, projected.point, projected.normal, out string stampError))
                        {
                            row.Status = stampError;
                            hasPreviousHit = false;
                            Repaint();
                            return;
                        }
                        stamped = true;
                    }
                }
            }
            else
            {
                if (!Stamp(session, hit.Point, hit.Normal, out string stampError))
                {
                    row.Status = stampError;
                    hasPreviousHit = false;
                    Repaint();
                    return;
                }
                stamped = true;
            }

            if (stamped)
            {
                session.Dirty = true;
                BindPreview(session, session.Engine.Current);
            }
            previousHitSession = session;
            previousHit = hit;
            hasPreviousHit = true;
            report = "Painting " + session.Target.name + " / " + session.Layer.DisplayName + ". One stroke can cross all ready targets.";
            Repaint();
        }

        private bool Stamp(TargetSession session, Vector3 point, Vector3 normal, out string error)
        {
            error = string.Empty;
            float radius = brushRadius;
            if (brushScaleJitter > 0f)
                radius *= UnityEngine.Random.Range(1f - brushScaleJitter, 1f + brushScaleJitter);
            float rotation = brushPreset != null && brushPreset.RandomRotation
                ? UnityEngine.Random.Range(0f, 360f)
                : brushRotation;
            bool erase = operation == 1;
            bool smooth = operation == 2;
            float depth = Mathf.Max(0.05f, radius * projectionDepthScale);
            return session.Engine.StampSurfaceMesh(
                session.RasterMesh.Mesh,
                session.Target.transform.localToWorldMatrix,
                point,
                normal,
                radius,
                depth,
                useNormalFilter,
                normalCutoff,
                brushOpacity,
                brushHardness,
                (int)session.Layer.MaskChannel,
                erase,
                brushPreset != null ? brushPreset.AlphaTexture : null,
                rotation,
                smooth,
                out error);
        }

        private static bool TryProjectToTarget(MeshCollider collider, Vector3 point, Vector3 normal, float depth, out RaycastHit hit)
        {
            hit = default;
            if (collider == null)
                return false;
            Vector3 direction = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            depth = Mathf.Max(0.05f, depth);
            Ray ray = new Ray(point + direction * depth, -direction);
            return collider.Raycast(ray, out hit, depth * 2f + 0.05f);
        }

        private bool EnsureSessionLoaded(TargetRow row, out string error)
        {
            error = string.Empty;
            if (row.Session != null && row.Session.Engine != null && row.Session.Engine.IsInitialized)
                return true;

            SurfacePaintTarget target = row.Target;
            SurfacePaintProfile profile = target != null ? target.Profile : null;
            SurfaceLayerDefinition layer = GetLayer(row);
            if (target == null || profile == null || layer == null)
            {
                error = "Target, profile or selected layer is unavailable.";
                return false;
            }

            Texture2D mask = profile.GetMaskPage(layer.MaskPageIndex);
            if (mask == null)
            {
                error = "Selected mask page is missing. Assign or create it before painting.";
                return false;
            }
            if (!target.TryGetMesh(out Mesh mesh) || mesh == null || !mesh.isReadable)
            {
                error = "Target mesh is missing or not readable.";
                return false;
            }
            if (!SurfaceUvRasterMesh.TryBuild(mesh, out SurfaceUvRasterMesh raster, out error))
                return false;

            // Hand the target off from the single-target Painter, saving and disposing its
            // old GPU session first so stale working buffers cannot overwrite these results.
            if (!SurfaceWeaverWindow.PrepareForMultiMeshPainting(target, out bool painterPrepared, out error))
            {
                raster.Dispose();
                return false;
            }
            if (painterPrepared)
                painterSessionsPrepared.Add(target);

            SurfaceGpuBrushEngine engine = new SurfaceGpuBrushEngine();
            if (!engine.Initialize(mask, mask.width, mask.height, out error))
            {
                engine.Dispose();
                raster.Dispose();
                return false;
            }

            TargetSession session = new TargetSession
            {
                Target = target,
                Profile = profile,
                Mesh = mesh,
                Collider = target.GetComponent<MeshCollider>(),
                Renderer = target.GetComponent<MeshRenderer>(),
                LayerIndex = row.LayerIndex,
                Layer = layer,
                MaskPageIndex = layer.MaskPageIndex,
                MaskAsset = mask,
                Engine = engine,
                RasterMesh = raster,
                Dirty = false
            };
            row.Session = session;
            BindPreview(session, engine.Current);
            if (session.PreviewMaterial == null)
                row.Status = "Ready to paint and save, but no _MaskPage0 or _PaintMask material property was found for live preview.";
            return true;
        }

        private static void BindPreview(TargetSession session, Texture mask)
        {
            if (session == null || session.Target == null || session.Renderer == null || mask == null)
                return;

            Material[] materials = session.Renderer.sharedMaterials;
            int match = -1;
            string property = null;
            bool layered = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;
                if (material.shader != null && material.shader.name == SurfacePaintMaterialBinder.ShaderName && material.HasProperty("_MaskPage0"))
                {
                    match = i;
                    property = "_MaskPage0";
                    layered = true;
                    break;
                }
                if (material.HasProperty("_PaintMask"))
                {
                    match = i;
                    property = "_PaintMask";
                    break;
                }
            }
            if (match < 0)
                return;

            if (session.PreviewMaterial == null || session.PreviewMaterialIndex != match ||
                materials[match] != session.PreviewMaterial)
            {
                session.RestorePreview();
                materials = session.Renderer.sharedMaterials;
                if (match >= materials.Length || materials[match] == null)
                    return;
                session.OriginalMaterial = materials[match];
                session.PreviewMaterial = new Material(session.OriginalMaterial)
                {
                    name = session.OriginalMaterial.name + " (SurfaceWeaver Multi-Mesh Preview)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                session.PreviewMaterialIndex = match;
                session.PreviewProperty = property;
                materials[match] = session.PreviewMaterial;
                session.Renderer.sharedMaterials = materials;
            }

            if (session.PreviewMaterial == null || !session.PreviewMaterial.HasProperty(property))
                return;
            if (layered)
            {
                SurfacePaintMaterialBinder.CopyMaterialTuning(session.OriginalMaterial, session.PreviewMaterial);
                SurfacePaintMaterialBinder.ApplyProfileToMaterial(
                    session.Profile, session.PreviewMaterial, mask, true, out _);
            }
            else
            {
                session.PreviewMaterial.SetTexture(property, mask);
            }
            SceneView.RepaintAll();
        }

        private void FinishStroke()
        {
            if (!isPainting && strokeTouched.Count == 0)
                return;
            isPainting = false;
            if (paintingControlId != 0 && GUIUtility.hotControl == paintingControlId)
                GUIUtility.hotControl = 0;
            paintingControlId = 0;
            hasPreviousHit = false;

            StrokeGroup group = new StrokeGroup();
            for (int i = 0; i < strokeTouched.Count; i++)
            {
                TargetSession session = strokeTouched[i];
                if (session == null || session.Engine == null)
                    continue;
                bool changed = session.Engine.HasPendingStrokeChanges;
                session.Engine.EndStroke();
                if (changed)
                    group.Sessions.Add(session);
            }
            strokeTouched.Clear();
            if (group.Sessions.Count > 0)
            {
                undoGroups.Add(group);
                redoGroups.Clear();
                report = "Committed one multi-target stroke across " + group.Sessions.Count + " mesh(es). Undo reverses the entire cross-target stroke.";
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void CancelStroke()
        {
            isPainting = false;
            if (paintingControlId != 0 && GUIUtility.hotControl == paintingControlId)
                GUIUtility.hotControl = 0;
            paintingControlId = 0;
            hasPreviousHit = false;
            for (int i = strokeTouched.Count - 1; i >= 0; i--)
            {
                TargetSession session = strokeTouched[i];
                if (session == null || session.Engine == null)
                    continue;
                session.Engine.CancelStroke(out _);
                session.Dirty = session.DirtyBeforeStroke;
                BindPreview(session, session.Engine.Current);
            }
            strokeTouched.Clear();
            report = "Cancelled the active multi-target stroke. All touched masks were restored to their pre-stroke state.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void UndoGroupedStroke()
        {
            if (isPainting || undoGroups.Count == 0)
                return;
            StrokeGroup group = undoGroups[undoGroups.Count - 1];
            for (int i = 0; i < group.Sessions.Count; i++)
            {
                TargetSession session = group.Sessions[i];
                if (session == null || session.Engine == null || !session.Engine.CanUndo)
                {
                    report = "Grouped Undo could not run because a target session no longer has the expected history. Refresh sessions only after saving all masks.";
                    return;
                }
            }
            for (int i = group.Sessions.Count - 1; i >= 0; i--)
            {
                TargetSession session = group.Sessions[i];
                if (!session.Engine.UndoLastStroke(out string error))
                {
                    report = "Grouped Undo failed: " + error;
                    return;
                }
                session.Dirty = true;
                BindPreview(session, session.Engine.Current);
            }
            undoGroups.RemoveAt(undoGroups.Count - 1);
            redoGroups.Add(group);
            report = "Undid one logical stroke across " + group.Sessions.Count + " target(s).";
            Repaint();
        }

        private void RedoGroupedStroke()
        {
            if (isPainting || redoGroups.Count == 0)
                return;
            StrokeGroup group = redoGroups[redoGroups.Count - 1];
            for (int i = 0; i < group.Sessions.Count; i++)
            {
                TargetSession session = group.Sessions[i];
                if (session == null || session.Engine == null || !session.Engine.CanRedo)
                {
                    report = "Grouped Redo could not run because a target session no longer has the expected redo history.";
                    return;
                }
            }
            for (int i = 0; i < group.Sessions.Count; i++)
            {
                TargetSession session = group.Sessions[i];
                if (!session.Engine.RedoLastStroke(out string error))
                {
                    report = "Grouped Redo failed: " + error;
                    return;
                }
                session.Dirty = true;
                BindPreview(session, session.Engine.Current);
            }
            redoGroups.RemoveAt(redoGroups.Count - 1);
            undoGroups.Add(group);
            report = "Redid one logical stroke across " + group.Sessions.Count + " target(s).";
            Repaint();
        }

        private bool SaveAllChanges(bool showSuccess)
        {
            if (isPainting)
                FinishStroke();
            bool success = true;
            int savedCount = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                TargetSession session = rows[i].Session;
                if (session == null || !session.Dirty || session.Engine == null || !session.Engine.IsInitialized)
                    continue;

                if (session.Profile.GetMaskPage(session.MaskPageIndex) != session.MaskAsset)
                {
                    rows[i].Status = "Mask page reference changed while this GPU session was active. Refresh the workspace after saving or resolving the external profile edit.";
                    success = false;
                    continue;
                }

                if (!SurfaceMaskAssetUtility.SaveRenderTextureToAsset(
                        session.Engine.Current,
                        session.MaskAsset,
                        session.Profile,
                        session.MaskPageIndex,
                        out Texture2D saved,
                        out string error))
                {
                    rows[i].Status = error;
                    success = false;
                    continue;
                }

                session.MaskAsset = saved;
                session.Dirty = false;
                BindPreview(session, session.Engine.Current);
                rows[i].Status = "Saved mask page " + session.MaskPageIndex + " for '" + session.Target.name + "'.";
                savedCount++;
            }
            AssetDatabase.SaveAssets();
            report = success
                ? "Saved " + savedCount + " dirty mask page(s)."
                : "Saved " + savedCount + " mask page(s), but one or more targets failed. Check their row messages.";
            Repaint();
            return success;
        }

        private bool HasUnsavedChanges()
        {
            return rows.Any(row => row.Session != null && row.Session.Dirty);
        }

        private void DisposeRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Session != null)
                {
                    rows[i].Session.Dispose();
                    rows[i].Session = null;
                }
                if (rows[i].Target != null && painterSessionsPrepared.Contains(rows[i].Target))
                    SurfaceWeaverWindow.ResumeAfterMultiMeshPainting(rows[i].Target);
            }
            painterSessionsPrepared.Clear();
            rows.Clear();
            strokeTouched.Clear();
            undoGroups.Clear();
            redoGroups.Clear();
        }
    }
}
