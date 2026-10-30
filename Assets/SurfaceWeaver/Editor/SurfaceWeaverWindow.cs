using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>SurfaceWeaver authoring window with first-pass Scene View picking and brush preview.</summary>
    public sealed class SurfaceWeaverWindow : EditorWindow
    {
        private SurfacePaintTarget target;
        private SurfacePaintProfile profile;
        private MeshUvValidationReport lastReport;
        private Vector2 scroll;
        private bool scenePreviewEnabled = true;
        private float brushRadius = 2f;
        private Color previewColor = new Color(1f, 0.65f, 0.12f, 0.95f);
        private bool hasSceneHit;
        private Vector2 lastHitUv;
        private Vector3 lastHitPoint;
        private Vector3 lastHitNormal;
        private int lastHitTriangle;
        private string sceneHitStatus = "Move the pointer over the selected target in Scene View.";

        [MenuItem("Window/SurfaceWeaver/Painter", false, 2100)]
        public static void OpenWindow()
        {
            SurfaceWeaverWindow window = GetWindow<SurfaceWeaverWindow>();
            window.titleContent = new GUIContent("SurfaceWeaver");
            window.minSize = new Vector2(360f, 460f);
            window.Show();
        }

        public static void OpenFor(SurfacePaintTarget selectedTarget)
        {
            OpenWindow();
            SurfaceWeaverWindow window = GetWindow<SurfaceWeaverWindow>();
            window.target = selectedTarget;
            window.profile = selectedTarget != null ? selectedTarget.Profile : null;
            window.lastReport = null;
            window.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            if (Selection.activeGameObject != null)
                target = Selection.activeGameObject.GetComponent<SurfacePaintTarget>();

            if (target != null)
                profile = target.Profile;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject == null)
                return;

            SurfacePaintTarget selected =
                Selection.activeGameObject.GetComponent<SurfacePaintTarget>();

            if (selected != null)
            {
                target = selected;
                profile = target.Profile;
                lastReport = null;
                hasSceneHit = false;
                sceneHitStatus = "Move the pointer over the selected target in Scene View.";
                Repaint();
            }
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
                EditorStyles.miniLabel
            );
            EditorGUILayout.HelpBox(
                "Scene View milestone: target-specific picking, UV0 readout and a world-space brush preview. " +
                "Painting is not enabled in this build yet.",
                MessageType.Info
            );
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target Mesh", EditorStyles.boldLabel);

            target = (SurfacePaintTarget)EditorGUILayout.ObjectField(
                "Paint Target",
                target,
                typeof(SurfacePaintTarget),
                true
            );

            using (new EditorGUI.DisabledScope(target != null ||
                                               Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Add Target Component to Selected Mesh"))
                    AddTargetToSelection();
            }

            if (target == null)
                return;

            if (!target.TryGetMesh(out Mesh mesh))
            {
                EditorGUILayout.HelpBox(
                    "The selected target does not have a readable MeshFilter and shared mesh reference.",
                    MessageType.Error
                );
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
                    "OK"
                );
                return;
            }

            target = Undo.AddComponent<SurfacePaintTarget>(selected);
            profile = target.Profile;
            lastReport = null;
            EditorGUIUtility.PingObject(target);
        }


        private void DrawScenePreviewSection()
        {
            EditorGUILayout.LabelField("Scene View Brush Preview", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            scenePreviewEnabled = EditorGUILayout.ToggleLeft(
                "Enable Scene View Preview",
                scenePreviewEnabled
            );

            using (new EditorGUI.DisabledScope(!scenePreviewEnabled))
            {
                brushRadius = EditorGUILayout.Slider(
                    "Brush Radius (World Units)",
                    brushRadius,
                    0.05f,
                    20f
                );

                previewColor = EditorGUILayout.ColorField(
                    "Preview Color",
                    previewColor
                );
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
                EditorGUILayout.HelpBox(
                    "Assign a SurfacePaintTarget to enable Scene View picking.",
                    MessageType.Info
                );
                return;
            }

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null)
            {
                EditorGUILayout.HelpBox(
                    "A non-convex MeshCollider is required for UV picking. Click Add / Refresh MeshCollider.",
                    MessageType.Warning
                );
            }
            else if (target.TryGetMesh(out Mesh mesh) && collider.sharedMesh != mesh)
            {
                EditorGUILayout.HelpBox(
                    "MeshCollider does not reference the target MeshFilter mesh. Refresh it before picking.",
                    MessageType.Warning
                );
            }
            else if (collider.convex)
            {
                EditorGUILayout.HelpBox(
                    "Disable Convex on the target MeshCollider.",
                    MessageType.Warning
                );
            }

            if (target.UvChannel != 0)
            {
                EditorGUILayout.HelpBox(
                    "This milestone uses RaycastHit.textureCoord (UV0). Set UV Channel to 0 for Scene View preview.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.LabelField("Picking Status", sceneHitStatus);
            if (hasSceneHit)
            {
                EditorGUILayout.LabelField("UV0", $"({lastHitUv.x:0.0000}, {lastHitUv.y:0.0000})");
                EditorGUILayout.LabelField("World Position", lastHitPoint.ToString("F3"));
                EditorGUILayout.LabelField("World Normal", lastHitNormal.ToString("F3"));
                EditorGUILayout.LabelField("Triangle Index", lastHitTriangle.ToString());
            }

            EditorGUILayout.HelpBox(
                "Hover over the selected mesh in Scene View. Hold Alt for normal Scene View navigation. " +
                "The ring is a world-space radius aligned to the hit surface normal. Hover preview does not modify the mesh or any texture.",
                MessageType.None
            );
        }

        private void AddOrRefreshMeshCollider()
        {
            if (target == null || !target.TryGetMesh(out Mesh mesh))
            {
                EditorUtility.DisplayDialog(
                    "SurfaceWeaver",
                    "The target does not have a valid MeshFilter mesh.",
                    "OK"
                );
                return;
            }

            if (!mesh.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "SurfaceWeaver",
                    "Enable Read/Write in the mesh Model Import Settings, then reimport the model.",
                    "OK"
                );
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
            hasSceneHit = false;
            sceneHitStatus = "MeshCollider refreshed. Move the pointer over the target in Scene View.";
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!scenePreviewEnabled || target == null || !target.gameObject.activeInHierarchy)
            {
                hasSceneHit = false;
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.alt)
            {
                hasSceneHit = false;
                return;
            }

            if (SurfaceScenePicker.TryPick(
                    target,
                    currentEvent.mousePosition,
                    out SurfaceSceneHit hit,
                    out string status))
            {
                hasSceneHit = true;
                lastHitUv = hit.Uv0;
                lastHitPoint = hit.Point;
                lastHitNormal = hit.Normal;
                lastHitTriangle = hit.TriangleIndex;
                sceneHitStatus = status;

                SurfaceBrushPreview.Draw(hit, brushRadius, previewColor);

                // Claim the default Scene View control while hovering the target so a preview click
                // does not accidentally select another object. Preview interaction itself is read-only.
                if (currentEvent.type == EventType.Layout)
                {
                    HandleUtility.AddDefaultControl(
                        GUIUtility.GetControlID(FocusType.Passive)
                    );
                }
            }
            else
            {
                hasSceneHit = false;
                sceneHitStatus = status;
            }

            if (currentEvent.type == EventType.MouseMove ||
                currentEvent.type == EventType.MouseDrag)
            {
                Repaint();
                sceneView.Repaint();
            }
        }

        private void DrawProfileSection()
        {
            EditorGUILayout.LabelField("Surface Profile", EditorStyles.boldLabel);

            profile = (SurfacePaintProfile)EditorGUILayout.ObjectField(
                "Profile",
                profile,
                typeof(SurfacePaintProfile),
                false
            );

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

            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("baseAlbedo"),
                new GUIContent("Base Albedo")
            );
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("baseTint"),
                new GUIContent("Base Tint")
            );
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("baseTiling"),
                new GUIContent("Base Tiling")
            );
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("maskStorageMode"),
                new GUIContent("Mask Storage")
            );
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("maskResolution"),
                new GUIContent("Mask Resolution")
            );
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("maskPages"),
                new GUIContent("Mask Pages"),
                true
            );

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(
                serializedProfile.FindProperty("paintLayers"),
                new GUIContent("Paint Layers"),
                true
            );

            if (serializedProfile.ApplyModifiedProperties())
                EditorUtility.SetDirty(profile);

            if (GUILayout.Button("Add Paint Layer"))
            {
                Undo.RecordObject(profile, "Add Surface Layer");
                profile.AddLayer("Surface " + (profile.PaintLayers.Count + 1));
                EditorUtility.SetDirty(profile);
            }

            EditorGUILayout.HelpBox(
                "Layer data is stored in a reusable ScriptableObject. Mask pages store up to four " +
                "independent grayscale layer masks each (R, G, B and A).",
                MessageType.None
            );
        }

        private void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Surface Paint Profile",
                "SurfacePaintProfile",
                "asset",
                "Choose a folder inside Assets for the profile asset."
            );

            if (string.IsNullOrEmpty(path))
                return;

            SurfacePaintProfile asset = ScriptableObject.CreateInstance<SurfacePaintProfile>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            profile = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void AssignProfile()
        {
            if (target == null || profile == null)
                return;

            Undo.RecordObject(target, "Assign Surface Paint Profile");
            target.SetProfile(profile);
            EditorUtility.SetDirty(target);
        }

        private void DrawDiagnosticsSection()
        {
            EditorGUILayout.LabelField("Mesh Diagnostics", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Validate UV Channel"))
                {
                    target.TryGetMesh(out Mesh mesh);
                    lastReport = MeshUvValidator.Validate(mesh, target.UvChannel);
                }
            }

            if (lastReport == null)
                return;

            EditorGUILayout.LabelField(
                "Result",
                lastReport.IsValid ? "No blocking issues found" : "Blocking issues found"
            );
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
                "Included now: target component, reusable profiles, layer definitions, first-pass UV diagnostics, " +
                "Scene View target picking, UV0 readout and a world-space brush footprint.\n\n" +
                "Next: GPU mask stamping, undoable strokes, brush alpha support and persistent mask assets.",
                MessageType.None
            );
        }
    }
}
