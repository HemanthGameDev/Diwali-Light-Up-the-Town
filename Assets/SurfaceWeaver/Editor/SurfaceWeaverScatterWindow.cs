using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Scene View prefab scatter tool. It only raycasts registered SurfacePaintTargets and
    /// creates ordinary prefab instances under a generated scene container. Existing Painter,
    /// spline, mask and target components are not modified.
    /// </summary>
    public sealed class SurfaceWeaverScatterWindow : EditorWindow
    {
        private enum ScatterMode { Paint, Erase }

        private struct TargetHit
        {
            public SurfacePaintTarget Target;
            public MeshCollider Collider;
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;
        }

        [SerializeField] private SurfacePaintTargetSet targetSet;
        [SerializeField] private SurfaceScatterProfile profile;
        [SerializeField] private ScatterMode mode;
        [SerializeField] private bool scenePlacementEnabled;
        [SerializeField] private bool showAdvanced;
        [SerializeField] private Vector2 scroll;

        private SerializedObject serializedProfile;
        private readonly List<SurfacePaintTarget> cachedTargets = new List<SurfacePaintTarget>();
        private bool isStroke;
        private int activeControlId;
        private Vector3 lastStampPoint;
        private int stampIndex;
        private string status = "Assign a Target Set and Scatter Profile, then enable Scene Placement.";

        private const float MaxRayDistance = 100000f;
        private const string RootPrefix = "SurfaceWeaver Scatter - ";
        private const int ControlHint = 0x53575343; // SWSC

        [MenuItem("Window/SurfaceWeaver/Object & Vegetation Scatter", false, 2120)]
        public static void Open()
        {
            SurfaceWeaverScatterWindow window = GetWindow<SurfaceWeaverScatterWindow>();
            window.titleContent = new GUIContent("SW Scatter");
            window.minSize = new Vector2(390f, 470f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshSerializedProfile();
            RefreshTargetCache();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            isStroke = false;
            if (GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            scenePlacementEnabled = false;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("SURFACEWEAVER — OBJECT & VEGETATION SCATTER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Place weighted prefab instances on registered mesh targets.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "This is a separate Editor authoring tool. It does not alter the Painter, spline paths, " +
                "profiles, mask pages or target transforms. Scene placement raycasts only against the " +
                "SurfacePaintTargets in the assigned Target Set. Placed prefabs are ordinary scene objects " +
                "parented under a SurfaceWeaver Scatter container and can be moved or deleted normally.",
                MessageType.Info);

            DrawAssetFields();
            DrawProfileSettings();
            DrawPlacementControls();
            DrawStatus();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetFields()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                targetSet = (SurfacePaintTargetSet)EditorGUILayout.ObjectField(
                    "Target Set", targetSet, typeof(SurfacePaintTargetSet), false);
                profile = (SurfaceScatterProfile)EditorGUILayout.ObjectField(
                    "Scatter Profile", profile, typeof(SurfaceScatterProfile), false);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshSerializedProfile();
                    RefreshTargetCache();
                    SceneView.RepaintAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Profile…"))
                        CreateProfile();
                    if (GUILayout.Button("Refresh Targets"))
                    {
                        RefreshTargetCache();
                        ValidateTargets(out int ready, out int total, out string details);
                        status = details;
                    }
                }
            }
        }

        private void DrawProfileSettings()
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Create or assign a Scatter Profile to configure prefab choices and placement rules.", MessageType.Warning);
                return;
            }

            if (serializedProfile == null || serializedProfile.targetObject != profile)
                RefreshSerializedProfile();
            if (serializedProfile == null)
                return;

            serializedProfile.Update();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Prefab Library", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("prefabs"), new GUIContent("Weighted Prefabs"), true);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Placement Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("brushRadius"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("instancesPerStamp"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("strokeSpacing"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("minimumInstanceDistance"));

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Surface & Randomization", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("alignToSurfaceNormal"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("maximumSlopeAngle"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("surfaceOffset"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("projectionDistance"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("exclusionLayers"), new GUIContent("Exclusion Layers"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("exclusionRadius"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("randomYaw"));
                if (profile.randomYaw)
                    EditorGUILayout.PropertyField(serializedProfile.FindProperty("yawRange"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("randomPitchAndRoll"));
                if (profile.randomPitchAndRoll)
                {
                    EditorGUILayout.PropertyField(serializedProfile.FindProperty("pitchRange"));
                    EditorGUILayout.PropertyField(serializedProfile.FindProperty("rollRange"));
                }
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("randomSeed"));
                EditorGUI.indentLevel--;
            }

            if (serializedProfile.ApplyModifiedProperties())
                SceneView.RepaintAll();
        }

        private void DrawPlacementControls()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene View Tool", EditorStyles.boldLabel);
                mode = (ScatterMode)GUILayout.Toolbar((int)mode, new[] { "Paint Objects", "Erase Objects" });
                using (new EditorGUI.DisabledScope(!CanEnablePlacement()))
                {
                    string label = scenePlacementEnabled ? "Stop Scene Placement" : "Enable Scene Placement";
                    if (GUILayout.Button(label, GUILayout.Height(30f)))
                    {
                        scenePlacementEnabled = !scenePlacementEnabled;
                        isStroke = false;
                        status = scenePlacementEnabled
                            ? "Scene placement enabled. Drag in a Scene View over a registered target. Hold Alt to navigate."
                            : "Scene placement disabled.";
                        SceneView.RepaintAll();
                    }
                }
                if (scenePlacementEnabled)
                    EditorGUILayout.HelpBox("Drag with the left mouse button in the Scene View. Hold Alt to orbit/pan. Each brush stamp is Undoable.", MessageType.Info);
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(status, scenePlacementEnabled ? MessageType.Info : MessageType.None);
        }

        private bool CanEnablePlacement()
        {
            if (targetSet == null || targetSet.Count == 0 || cachedTargets.Count == 0 || profile == null)
                return false;
            return mode == ScatterMode.Erase || HasUsablePrefab();
        }

        private bool HasUsablePrefab()
        {
            if (profile == null || profile.prefabs == null)
                return false;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry entry = profile.prefabs[i];
                if (entry != null && entry.prefab != null && entry.weight > 0f)
                    return true;
            }
            return false;
        }

        private void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create SurfaceWeaver Scatter Profile", "SurfaceScatterProfile", "asset",
                "Choose a folder inside Assets for this reusable scatter profile.");
            if (string.IsNullOrEmpty(path))
                return;

            SurfaceScatterProfile created = CreateInstance<SurfaceScatterProfile>();
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            profile = created;
            RefreshSerializedProfile();
            EditorGUIUtility.PingObject(created);
            status = "Created a Scatter Profile. Add one or more prefab entries to its Weighted Prefabs list.";
        }

        private void RefreshSerializedProfile()
        {
            serializedProfile = profile != null ? new SerializedObject(profile) : null;
        }

        private void ValidateTargets(out int ready, out int total, out string details)
        {
            ready = 0;
            total = targetSet != null ? targetSet.Count : 0;
            int unresolved = Mathf.Max(0, total - cachedTargets.Count);
            int missingCollider = 0;
            foreach (SurfacePaintTarget target in ResolveTargets())
            {
                if (target == null) { unresolved++; continue; }
                if (TryGetUsableCollider(target, out _)) ready++;
                else missingCollider++;
            }
            details = string.Format("Scatter targets: {0} in set; {1} ready; {2} unresolved; {3} missing a compatible non-convex MeshCollider.",
                total, ready, unresolved, missingCollider);
        }

        private void RefreshTargetCache()
        {
            cachedTargets.Clear();
            if (targetSet == null || targetSet.TargetGlobalObjectIds == null)
                return;

            for (int i = 0; i < targetSet.TargetGlobalObjectIds.Count; i++)
            {
                string value = targetSet.TargetGlobalObjectIds[i];
                if (string.IsNullOrWhiteSpace(value) || !GlobalObjectId.TryParse(value, out GlobalObjectId id))
                    continue;
                SurfacePaintTarget target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as SurfacePaintTarget;
                if (target != null && target.gameObject.scene.IsValid() && target.gameObject.scene.isLoaded && !cachedTargets.Contains(target))
                    cachedTargets.Add(target);
            }
        }

        private IEnumerable<SurfacePaintTarget> ResolveTargets()
        {
            for (int i = 0; i < cachedTargets.Count; i++)
                if (cachedTargets[i] != null)
                    yield return cachedTargets[i];
        }

        private static bool TryGetUsableCollider(SurfacePaintTarget target, out MeshCollider collider)
        {
            collider = null;
            if (target == null || !target.gameObject.activeInHierarchy || !target.TryGetMesh(out Mesh mesh) || mesh == null)
                return false;
            if (!target.TryGetRenderer(out MeshRenderer renderer) || renderer == null || !renderer.enabled)
                return false;

            collider = target.GetComponent<MeshCollider>();
            return collider != null && collider.enabled && !collider.convex && collider.sharedMesh == mesh;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!scenePlacementEnabled || !CanEnablePlacement())
                return;

            Event evt = Event.current;
            if (evt == null)
                return;
            if (evt.alt)
            {
                // Let Scene View navigation take over if Alt is pressed mid-stroke.
                isStroke = false;
                if (GUIUtility.hotControl == activeControlId)
                    GUIUtility.hotControl = 0;
                return;
            }

            activeControlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            if (evt.type == EventType.Layout)
                HandleUtility.AddDefaultControl(activeControlId);

            bool hasHit = TryPickTarget(evt.mousePosition, out TargetHit hit);
            if (evt.type == EventType.Repaint && hasHit)
                DrawBrushPreview(hit);

            if (evt.type == EventType.MouseDown && evt.button == 0 && hasHit)
            {
                GUIUtility.hotControl = activeControlId;
                isStroke = true;
                lastStampPoint = hit.Point;
                ProcessStamp(hit);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && isStroke && GUIUtility.hotControl == activeControlId && evt.button == 0)
            {
                if (hasHit && Vector3.Distance(lastStampPoint, hit.Point) >= profile.strokeSpacing)
                {
                    ProcessStamp(hit);
                    lastStampPoint = hit.Point;
                }
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && isStroke && GUIUtility.hotControl == activeControlId)
            {
                isStroke = false;
                GUIUtility.hotControl = 0;
                evt.Use();
                return;
            }

        }

        private bool TryPickTarget(Vector2 guiPosition, out TargetHit nearest)
        {
            nearest = default;
            bool found = false;
            float closest = MaxRayDistance;
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            foreach (SurfacePaintTarget target in ResolveTargets())
            {
                if (!TryGetUsableCollider(target, out MeshCollider collider))
                    continue;
                if (!collider.Raycast(ray, out RaycastHit hit, MaxRayDistance) || hit.distance >= closest)
                    continue;

                closest = hit.distance;
                nearest = new TargetHit
                {
                    Target = target,
                    Collider = collider,
                    Point = hit.point,
                    Normal = hit.normal.normalized,
                    Distance = hit.distance
                };
                found = true;
            }
            return found;
        }

        private void DrawBrushPreview(TargetHit hit)
        {
            Handles.color = mode == ScatterMode.Paint
                ? new Color(0.2f, 0.85f, 0.35f, 0.9f)
                : new Color(0.95f, 0.25f, 0.2f, 0.9f);
            Handles.DrawWireDisc(hit.Point + hit.Normal * profile.surfaceOffset, hit.Normal, profile.brushRadius);
            Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.10f);
            Handles.DrawSolidDisc(hit.Point + hit.Normal * profile.surfaceOffset, hit.Normal, profile.brushRadius);
            Handles.color = Color.white;
            Handles.Label(hit.Point + hit.Normal * (profile.surfaceOffset + 0.05f), hit.Target.name);
        }

        private void ProcessStamp(TargetHit hit)
        {
            if (mode == ScatterMode.Paint)
                PlaceStamp(hit);
            else
                EraseStamp(hit);
            SceneView.RepaintAll();
        }

        private void PlaceStamp(TargetHit hit)
        {
            if (!TryBuildTargetContainer(hit.Target, out Transform targetContainer))
            {
                status = "Could not create or find a scene container for this target.";
                Repaint();
                return;
            }

            List<Vector3> occupied = CollectOccupiedPositions();
            System.Random random = new System.Random(unchecked(profile.randomSeed + stampIndex++));
            int placed = 0;
            int attempts = Mathf.Max(profile.instancesPerStamp * 24, 24);
            Vector3 normal = hit.Normal.sqrMagnitude > 0.0001f ? hit.Normal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.forward);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("SurfaceWeaver Scatter Objects");
            for (int i = 0; i < attempts && placed < profile.instancesPerStamp; i++)
            {
                Vector2 sample = RandomInsideCircle(random) * profile.brushRadius;
                Vector3 origin = hit.Point + tangent * sample.x + bitangent * sample.y + normal * profile.projectionDistance;
                Ray ray = new Ray(origin, -normal);
                if (!hit.Collider.Raycast(ray, out RaycastHit candidate, profile.projectionDistance * 2f))
                    continue;
                if (Vector3.Angle(Vector3.up, candidate.normal) > profile.maximumSlopeAngle)
                    continue;
                if (IsBlockedByExclusion(candidate.point + candidate.normal * profile.surfaceOffset, hit.Collider))
                    continue;
                if (IsTooClose(candidate.point, occupied, profile.minimumInstanceDistance))
                    continue;

                SurfaceScatterPrefabEntry entry = ChoosePrefab(random);
                if (entry == null || entry.prefab == null)
                    break;

                GameObject instance = PrefabUtility.InstantiatePrefab(entry.prefab, hit.Target.gameObject.scene) as GameObject;
                if (instance == null)
                    continue;
                Undo.RegisterCreatedObjectUndo(instance, "Place SurfaceWeaver Scatter Instance");
                Undo.SetTransformParent(instance.transform, targetContainer, "Parent SurfaceWeaver Scatter Instance");
                Undo.RecordObject(instance.transform, "Configure SurfaceWeaver Scatter Instance");

                float scaleMin = Mathf.Max(0.01f, Mathf.Min(entry.minimumScale, entry.maximumScale));
                float scaleMax = Mathf.Max(scaleMin, Mathf.Max(entry.minimumScale, entry.maximumScale));
                float scale = NextFloat(random, scaleMin, scaleMax);
                float yaw = profile.randomYaw ? NextFloat(random, -profile.yawRange * 0.5f, profile.yawRange * 0.5f) : 0f;
                float pitch = profile.randomPitchAndRoll ? NextFloat(random, -profile.pitchRange, profile.pitchRange) : 0f;
                float roll = profile.randomPitchAndRoll ? NextFloat(random, -profile.rollRange, profile.rollRange) : 0f;
                Quaternion align = profile.alignToSurfaceNormal ? Quaternion.FromToRotation(Vector3.up, candidate.normal) : Quaternion.identity;
                Quaternion variation = Quaternion.Euler(pitch, yaw, roll);
                instance.transform.SetPositionAndRotation(candidate.point + candidate.normal * profile.surfaceOffset,
                    align * Quaternion.Euler(entry.rotationOffset) * variation);
                instance.transform.localScale = instance.transform.localScale * scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

                occupied.Add(candidate.point);
                placed++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            status = "Placed " + placed + " instance(s) on " + hit.Target.name + ". Undo removes the latest stamp.";
            Repaint();
            if (placed > 0)
                EditorSceneManager.MarkSceneDirty(hit.Target.gameObject.scene);
        }

        private void EraseStamp(TargetHit hit)
        {
            if (!TryBuildTargetContainer(hit.Target, out Transform targetContainer))
            {
                status = "No scatter container exists for this target yet.";
                Repaint();
                return;
            }

            List<GameObject> toDelete = new List<GameObject>();
            for (int i = 0; i < targetContainer.childCount; i++)
            {
                Transform child = targetContainer.GetChild(i);
                if (child != null && Vector3.Distance(child.position, hit.Point) <= profile.brushRadius)
                    toDelete.Add(child.gameObject);
            }

            if (toDelete.Count == 0)
            {
                status = "No scattered instances found inside the eraser radius.";
                Repaint();
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Erase SurfaceWeaver Scatter Instances");
            for (int i = 0; i < toDelete.Count; i++)
                Undo.DestroyObjectImmediate(toDelete[i]);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(hit.Target.gameObject.scene);
            status = "Erased " + toDelete.Count + " instance(s). Undo restores them.";
            Repaint();
        }

        private bool TryBuildTargetContainer(SurfacePaintTarget target, out Transform targetContainer)
        {
            targetContainer = null;
            if (target == null || profile == null)
                return false;

            string profileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
            string profileKey = string.IsNullOrEmpty(profileGuid) ? profile.name : profileGuid;
            string rootName = RootPrefix + profile.name + " [" + profileKey + "]";
            Scene scene = target.gameObject.scene;
            GameObject root = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                {
                    root = roots[i];
                    break;
                }
            }

            if (root == null)
            {
                root = new GameObject(rootName);
                if (root.scene != scene)
                    SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create SurfaceWeaver Scatter Container");
            }

            GlobalObjectId targetId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            string targetKey = "Target - " + target.name + " [" + targetId + "]";
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child != null && child.name == targetKey)
                {
                    targetContainer = child;
                    return true;
                }
            }

            GameObject group = new GameObject(targetKey);
            Undo.RegisterCreatedObjectUndo(group, "Create SurfaceWeaver Target Scatter Group");
            Undo.SetTransformParent(group.transform, root.transform, "Parent SurfaceWeaver Target Group");
            targetContainer = group.transform;
            return true;
        }

        private List<Vector3> CollectOccupiedPositions()
        {
            List<Vector3> result = new List<Vector3>();
            if (targetSet == null || profile == null)
                return result;

            string profileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
            string profileKey = string.IsNullOrEmpty(profileGuid) ? profile.name : profileGuid;
            string rootName = RootPrefix + profile.name + " [" + profileKey + "]";
            HashSet<int> visitedScenes = new HashSet<int>();
            foreach (SurfacePaintTarget target in ResolveTargets())
            {
                Scene scene = target.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded || !visitedScenes.Add(scene.handle))
                    continue;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r] == null || roots[r].name != rootName)
                        continue;
                    Transform scatterRoot = roots[r].transform;
                    for (int g = 0; g < scatterRoot.childCount; g++)
                    {
                        Transform targetGroup = scatterRoot.GetChild(g);
                        for (int p = 0; p < targetGroup.childCount; p++)
                            result.Add(targetGroup.GetChild(p).position);
                    }
                }
            }
            return result;
        }


        private bool IsBlockedByExclusion(Vector3 position, MeshCollider sourceCollider)
        {
            if (profile == null || profile.exclusionLayers.value == 0)
                return false;

            Collider[] overlaps = Physics.OverlapSphere(
                position,
                profile.exclusionRadius,
                profile.exclusionLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider candidate = overlaps[i];
                if (candidate == null || candidate == sourceCollider)
                    continue;
                if (IsRegisteredTargetCollider(candidate))
                    continue;
                return true;
            }
            return false;
        }

        private bool IsRegisteredTargetCollider(Collider candidate)
        {
            foreach (SurfacePaintTarget target in ResolveTargets())
            {
                if (target != null && target.GetComponent<MeshCollider>() == candidate)
                    return true;
            }
            return false;
        }

        private SurfaceScatterPrefabEntry ChoosePrefab(System.Random random)
        {
            if (profile == null || profile.prefabs == null)
                return null;
            float total = 0f;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry item = profile.prefabs[i];
                if (item != null && item.prefab != null && item.weight > 0f)
                    total += item.weight;
            }
            if (total <= 0f)
                return null;

            float value = (float)random.NextDouble() * total;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry item = profile.prefabs[i];
                if (item == null || item.prefab == null || item.weight <= 0f)
                    continue;
                value -= item.weight;
                if (value <= 0f)
                    return item;
            }
            return null;
        }

        private static bool IsTooClose(Vector3 point, List<Vector3> occupied, float minimumDistance)
        {
            float distanceSquared = minimumDistance * minimumDistance;
            for (int i = 0; i < occupied.Count; i++)
            {
                if ((occupied[i] - point).sqrMagnitude < distanceSquared)
                    return true;
            }
            return false;
        }

        private static Vector2 RandomInsideCircle(System.Random random)
        {
            float angle = NextFloat(random, 0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static float NextFloat(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }
    }
}
