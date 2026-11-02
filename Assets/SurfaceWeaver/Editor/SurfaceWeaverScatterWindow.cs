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
        [SerializeField] private bool liveUpdateExistingInstances = true;
        [SerializeField] private Vector2 scroll;

        private SerializedObject serializedProfile;
        private readonly List<SurfacePaintTarget> cachedTargets = new List<SurfacePaintTarget>();
        private readonly List<GameObject> previewInstances = new List<GameObject>();
        private readonly List<GameObject> previewPrefabReferences = new List<GameObject>();
        private GameObject previewRoot;
        private bool previewDirty = true;
        private bool hasPreviewHit;
        private TargetHit lastPreviewHit;
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
            ClearPreviewInstances();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("SURFACEWEAVER — OBJECT & VEGETATION SCATTER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Place enabled prefab entries on registered mesh targets.", EditorStyles.wordWrappedMiniLabel);
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
                    previewDirty = true;
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
            DrawPrefabEntries();
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
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("globalScaleMultiplier"),
                    new GUIContent("Global Scale (X Width / Y Height / Z Depth)"));
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
            {
                previewDirty = true;
                if (liveUpdateExistingInstances)
                    UpdateExistingInstancesFromProfile(false);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Existing Scatter Instances", EditorStyles.boldLabel);
                liveUpdateExistingInstances = EditorGUILayout.ToggleLeft(
                    "Live Update Existing Instances", liveUpdateExistingInstances);
                EditorGUILayout.HelpBox(
                    "When enabled, edits to per-prefab scale, global X/Y/Z scale, rotation randomization, surface alignment and surface offset update already-placed instances from their stored placement data. Weight affects future prefab selection only. Brush radius, density, spacing and instances-per-stamp affect future stamps only.",
                    MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Adopt Existing Instances")) AdoptExistingInstances();
                    if (GUILayout.Button("Update Existing Now")) UpdateExistingInstancesFromProfile(true);
                }
            }
        }

        private void DrawPrefabEntries()
        {
            SerializedProperty entries = serializedProfile.FindProperty("prefabs");
            if (entries == null) return;

            EditorGUILayout.PropertyField(entries, new GUIContent("Prefab Entries"), false);
            if (!entries.isExpanded) return;

            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty prefab = entry.FindPropertyRelative("prefab");
                SerializedProperty weight = entry.FindPropertyRelative("weight");
                SerializedProperty minScale = entry.FindPropertyRelative("minimumScale");
                SerializedProperty maxScale = entry.FindPropertyRelative("maximumScale");
                SerializedProperty rotation = entry.FindPropertyRelative("rotationOffset");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Prefab " + (i + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button("−", GUILayout.Width(24f))) removeIndex = i;
                    }
                    EditorGUILayout.PropertyField(prefab, new GUIContent("Prefab"));
                    EditorGUILayout.PropertyField(weight, new GUIContent("Selection Weight"));
                    EditorGUILayout.PropertyField(minScale, new GUIContent("Minimum Scale"));
                    EditorGUILayout.PropertyField(maxScale, new GUIContent("Maximum Scale"));
                    EditorGUILayout.PropertyField(rotation, new GUIContent("Rotation Offset"));
                    EditorGUILayout.LabelField("Weight 0 excludes this prefab; higher weights make it more likely to be selected.", EditorStyles.miniLabel);
                }
            }
            if (removeIndex >= 0) entries.DeleteArrayElementAtIndex(removeIndex);
            if (GUILayout.Button("+ Add Prefab Entry"))
            {
                int index = entries.arraySize++;
                SerializedProperty added = entries.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("weight").floatValue = 1f;
                added.FindPropertyRelative("minimumScale").floatValue = 0.8f;
                added.FindPropertyRelative("maximumScale").floatValue = 1.2f;
                added.FindPropertyRelative("entryId").stringValue = Guid.NewGuid().ToString("N");
            }
            EditorGUI.indentLevel--;
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
                        if (!scenePlacementEnabled) ClearPreviewInstances();
                        previewDirty = true;
                        status = scenePlacementEnabled
                            ? "Scene placement enabled. Drag in a Scene View over a registered target. Hold Alt to navigate."
                            : "Scene placement disabled.";
                        SceneView.RepaintAll();
                    }
                }
                if (scenePlacementEnabled)
                    EditorGUILayout.HelpBox("Move the pointer over a registered target to see live prefab ghosts. Adjust the profile to update the preview; click/drag to commit stamps. Hold Alt to navigate. Each stamp is Undoable.", MessageType.Info);
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
            status = "Created a Scatter Profile. Add prefab entries and adjust their selection weights. A weight of zero excludes an entry.";
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
            {
                DrawBrushPreview(hit);
                if (mode == ScatterMode.Paint) DrawPrefabPreview(hit);
                else ClearPreviewInstances();
            }
            else if (evt.type == EventType.Repaint && !hasHit)
            {
                ClearPreviewInstances();
            }

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

        private sealed class ScatterPlacement
        {
            public SurfaceScatterPrefabEntry Entry;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public Vector3 Normal;
            public Vector3 SurfacePoint;
            public float NormalizedScale;
            public float NormalizedYaw;
            public float NormalizedPitch;
            public float NormalizedRoll;
        }

        private List<ScatterPlacement> GeneratePlacements(TargetHit hit, int seed)
        {
            List<ScatterPlacement> placements = new List<ScatterPlacement>();
            if (profile == null || hit.Collider == null) return placements;

            List<Vector3> occupied = CollectOccupiedPositions();
            System.Random random = new System.Random(unchecked(seed));
            int attempts = Mathf.Max(profile.instancesPerStamp * 24, 24);
            Vector3 normal = hit.Normal.sqrMagnitude > 0.0001f ? hit.Normal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(normal, Vector3.forward);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            for (int i = 0; i < attempts && placements.Count < profile.instancesPerStamp; i++)
            {
                Vector2 sample = RandomInsideCircle(random) * profile.brushRadius;
                Vector3 origin = hit.Point + tangent * sample.x + bitangent * sample.y + normal * profile.projectionDistance;
                Ray ray = new Ray(origin, -normal);
                if (!hit.Collider.Raycast(ray, out RaycastHit candidate, profile.projectionDistance * 2f)) continue;
                if (Vector3.Angle(Vector3.up, candidate.normal) > profile.maximumSlopeAngle) continue;
                if (IsBlockedByExclusion(candidate.point + candidate.normal * profile.surfaceOffset, hit.Collider)) continue;
                if (IsTooClose(candidate.point, occupied, profile.minimumInstanceDistance)) continue;

                SurfaceScatterPrefabEntry entry = ChoosePrefab(random);
                if (entry == null || entry.prefab == null) break;

                float scaleMin = Mathf.Max(0.01f, Mathf.Min(entry.minimumScale, entry.maximumScale));
                float scaleMax = Mathf.Max(scaleMin, Mathf.Max(entry.minimumScale, entry.maximumScale));
                float normalizedScale = (float)random.NextDouble();
                float normalizedYaw = NextFloat(random, -1f, 1f);
                float normalizedPitch = NextFloat(random, -1f, 1f);
                float normalizedRoll = NextFloat(random, -1f, 1f);
                float scale = Mathf.Lerp(scaleMin, scaleMax, normalizedScale);
                float yaw = profile.randomYaw ? normalizedYaw * profile.yawRange * 0.5f : 0f;
                float pitch = profile.randomPitchAndRoll ? normalizedPitch * profile.pitchRange : 0f;
                float roll = profile.randomPitchAndRoll ? normalizedRoll * profile.rollRange : 0f;
                Quaternion align = profile.alignToSurfaceNormal ? Quaternion.FromToRotation(Vector3.up, candidate.normal) : Quaternion.identity;
                Quaternion rotation = align * Quaternion.Euler(entry.rotationOffset) * Quaternion.Euler(pitch, yaw, roll);
                placements.Add(new ScatterPlacement
                {
                    Entry = entry,
                    Position = candidate.point + candidate.normal * profile.surfaceOffset,
                    Rotation = rotation,
                    Scale = scale,
                    Normal = candidate.normal,
                    SurfacePoint = candidate.point,
                    NormalizedScale = normalizedScale,
                    NormalizedYaw = normalizedYaw,
                    NormalizedPitch = normalizedPitch,
                    NormalizedRoll = normalizedRoll
                });
                occupied.Add(candidate.point);
            }
            return placements;
        }

        private void PlaceStamp(TargetHit hit)
        {
            if (!TryBuildTargetContainer(hit.Target, out Transform targetContainer))
            {
                status = "Could not create or find a scene container for this target.";
                Repaint();
                return;
            }

            List<ScatterPlacement> placements = GeneratePlacements(hit, unchecked(profile.randomSeed + stampIndex++));
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("SurfaceWeaver Scatter Objects");
            int placed = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                ScatterPlacement placement = placements[i];
                GameObject instance = PrefabUtility.InstantiatePrefab(placement.Entry.prefab, hit.Target.gameObject.scene) as GameObject;
                if (instance == null) continue;
                Undo.RegisterCreatedObjectUndo(instance, "Place SurfaceWeaver Scatter Instance");
                Undo.SetTransformParent(instance.transform, targetContainer, "Parent SurfaceWeaver Scatter Instance");
                Undo.RecordObject(instance.transform, "Configure SurfaceWeaver Scatter Instance");
                instance.transform.SetPositionAndRotation(placement.Position, placement.Rotation);
                instance.transform.localScale = Vector3.Scale(
                    placement.Entry.prefab.transform.localScale * placement.Scale,
                    profile.globalScaleMultiplier);
                SurfaceScatterInstanceData data = Undo.AddComponent<SurfaceScatterInstanceData>(instance);
                data.profileGuid = GetProfileGuid();
                data.prefabEntryId = placement.Entry.entryId;
                data.sourcePrefab = placement.Entry.prefab;
                data.surfacePoint = placement.SurfacePoint;
                data.surfaceNormal = placement.Normal;
                data.prefabBaseScale = placement.Entry.prefab.transform.localScale;
                data.normalizedScale = placement.NormalizedScale;
                data.normalizedYaw = placement.NormalizedYaw;
                data.normalizedPitch = placement.NormalizedPitch;
                data.normalizedRoll = placement.NormalizedRoll;
                data.legacyAdopted = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                EditorUtility.SetDirty(data);
                placed++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            status = "Placed " + placed + " instance(s) on " + hit.Target.name + ". Undo removes the latest stamp.";
            Repaint();
            previewDirty = true;
            if (placed > 0) EditorSceneManager.MarkSceneDirty(hit.Target.gameObject.scene);
        }

        private void DrawPrefabPreview(TargetHit hit)
        {
            if (profile == null || mode != ScatterMode.Paint) return;
            bool changed = previewDirty || !hasPreviewHit || lastPreviewHit.Target != hit.Target ||
                           (lastPreviewHit.Point - hit.Point).sqrMagnitude > 0.0004f ||
                           Vector3.Angle(lastPreviewHit.Normal, hit.Normal) > 1f;
            if (!changed) return;

            previewDirty = false;
            hasPreviewHit = true;
            lastPreviewHit = hit;
            List<ScatterPlacement> placements = GeneratePlacements(hit, unchecked(profile.randomSeed + stampIndex));
            EnsurePreviewRoot();

            for (int i = 0; i < placements.Count; i++)
            {
                ScatterPlacement placement = placements[i];
                GameObject prefab = placement.Entry.prefab;
                if (i >= previewInstances.Count || previewPrefabReferences[i] != prefab)
                {
                    while (previewInstances.Count <= i)
                    {
                        previewInstances.Add(null);
                        previewPrefabReferences.Add(null);
                    }
                    DestroyPreviewAt(i);
                    GameObject ghost = Instantiate(prefab, previewRoot.transform);
                    ghost.name = "[Preview] " + prefab.name;
                    SetPreviewFlagsAndDisableBehaviours(ghost);
                    previewInstances[i] = ghost;
                    previewPrefabReferences[i] = prefab;
                }

                GameObject preview = previewInstances[i];
                if (preview == null) continue;
                preview.SetActive(true);
                preview.transform.SetPositionAndRotation(placement.Position, placement.Rotation);
                preview.transform.localScale = Vector3.Scale(
                    prefab.transform.localScale * placement.Scale, profile.globalScaleMultiplier);
                Bounds bounds = GetPreviewBounds(preview);
                Handles.color = new Color(0.2f, 1f, 0.45f, 0.75f);
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            for (int i = placements.Count; i < previewInstances.Count; i++)
                if (previewInstances[i] != null) previewInstances[i].SetActive(false);

            Handles.color = Color.white;
            Handles.Label(hit.Point + hit.Normal * (profile.surfaceOffset + 0.12f),
                placements.Count + " prefab preview(s) — click to place");
        }

        private void EnsurePreviewRoot()
        {
            if (previewRoot != null) return;
            previewRoot = new GameObject("SurfaceWeaver Scatter Preview (Temporary)")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void SetPreviewFlagsAndDisableBehaviours(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject go = transforms[i].gameObject;
                go.hideFlags = HideFlags.HideAndDontSave;
                go.layer = 2; // Ignore Raycast.
                Collider[] colliders = go.GetComponents<Collider>();
                for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;
                MonoBehaviour[] scripts = go.GetComponents<MonoBehaviour>();
                for (int b = 0; b < scripts.Length; b++) if (scripts[b] != null) scripts[b].enabled = false;
                AudioSource[] audio = go.GetComponents<AudioSource>();
                for (int a = 0; a < audio.Length; a++) audio[a].enabled = false;
                ParticleSystem[] particles = go.GetComponents<ParticleSystem>();
                for (int q = 0; q < particles.Length; q++) particles[q].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static Bounds GetPreviewBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.25f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void DestroyPreviewAt(int index)
        {
            if (index < 0 || index >= previewInstances.Count) return;
            if (previewInstances[index] != null) DestroyImmediate(previewInstances[index]);
            previewInstances[index] = null;
            previewPrefabReferences[index] = null;
        }

        private void ClearPreviewInstances()
        {
            for (int i = 0; i < previewInstances.Count; i++)
                if (previewInstances[i] != null) DestroyImmediate(previewInstances[i]);
            previewInstances.Clear();
            previewPrefabReferences.Clear();
            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewRoot = null;
            hasPreviewHit = false;
            previewDirty = true;
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
            if (profile == null || profile.prefabs == null) return null;
            double totalWeight = 0.0;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry item = profile.prefabs[i];
                if (item != null && item.prefab != null && item.weight > 0f)
                    totalWeight += item.weight;
            }
            if (totalWeight <= 0.0) return null;

            double roll = random.NextDouble() * totalWeight;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry item = profile.prefabs[i];
                if (item == null || item.prefab == null || item.weight <= 0f) continue;
                roll -= item.weight;
                if (roll <= 0.0) return item;
            }
            for (int i = profile.prefabs.Count - 1; i >= 0; i--)
            {
                SurfaceScatterPrefabEntry item = profile.prefabs[i];
                if (item != null && item.prefab != null && item.weight > 0f) return item;
            }
            return null;
        }


        private string GetProfileGuid()
        {
            string path = profile != null ? AssetDatabase.GetAssetPath(profile) : string.Empty;
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? (profile != null ? profile.name : string.Empty) : guid;
        }

        private SurfaceScatterPrefabEntry FindEntry(string entryId, GameObject sourcePrefab)
        {
            if (profile == null || profile.prefabs == null) return null;
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry entry = profile.prefabs[i];
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(entryId) && entry.entryId == entryId) return entry;
            }
            for (int i = 0; i < profile.prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry entry = profile.prefabs[i];
                if (entry != null && entry.prefab == sourcePrefab) return entry;
            }
            return null;
        }

        private void UpdateExistingInstancesFromProfile(bool showStatus)
        {
            if (profile == null) return;
            SurfaceScatterInstanceData[] all = Resources.FindObjectsOfTypeAll<SurfaceScatterInstanceData>();
            int updated = 0;
            int skipped = 0;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update Existing SurfaceWeaver Scatter Instances");
            string guid = GetProfileGuid();
            for (int i = 0; i < all.Length; i++)
            {
                SurfaceScatterInstanceData data = all[i];
                if (data == null || data.gameObject == null) continue;
                Scene scene = data.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded || data.profileGuid != guid) continue;
                SurfaceScatterPrefabEntry entry = FindEntry(data.prefabEntryId, data.sourcePrefab);
                if (entry == null || entry.prefab == null) { skipped++; continue; }

                Transform instanceTransform = data.transform;
                Undo.RecordObject(instanceTransform, "Update Scatter Instance Transform");
                Undo.RecordObject(data, "Update Scatter Instance Metadata");
                Vector3 normal = data.surfaceNormal.sqrMagnitude > 0.0001f ? data.surfaceNormal.normalized : Vector3.up;
                Vector3 position = data.surfacePoint + normal * profile.surfaceOffset;
                Quaternion rotation;
                Vector3 scale;
                if (data.legacyAdopted)
                {
                    rotation = data.legacyBaseWorldRotation;
                    scale = Vector3.Scale(data.legacyBaseLocalScale, profile.globalScaleMultiplier);
                }
                else
                {
                    float minimum = Mathf.Max(0.01f, Mathf.Min(entry.minimumScale, entry.maximumScale));
                    float maximum = Mathf.Max(minimum, Mathf.Max(entry.minimumScale, entry.maximumScale));
                    float randomScale = Mathf.Lerp(minimum, maximum, Mathf.Clamp01(data.normalizedScale));
                    float yaw = profile.randomYaw ? data.normalizedYaw * profile.yawRange * 0.5f : 0f;
                    float pitch = profile.randomPitchAndRoll ? data.normalizedPitch * profile.pitchRange : 0f;
                    float roll = profile.randomPitchAndRoll ? data.normalizedRoll * profile.rollRange : 0f;
                    Quaternion align = profile.alignToSurfaceNormal ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
                    rotation = align * Quaternion.Euler(entry.rotationOffset) * Quaternion.Euler(pitch, yaw, roll);
                    scale = Vector3.Scale(data.prefabBaseScale * randomScale, profile.globalScaleMultiplier);
                }
                instanceTransform.SetPositionAndRotation(position, rotation);
                instanceTransform.localScale = scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instanceTransform);
                EditorUtility.SetDirty(data);
                EditorSceneManager.MarkSceneDirty(scene);
                updated++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            if (showStatus || updated > 0)
                status = "Updated " + updated + " existing scatter instance(s)." +
                         (skipped > 0 ? " Skipped " + skipped + " instance(s) whose prefab entry is missing." : string.Empty);
            Repaint();
            SceneView.RepaintAll();
        }

        private void AdoptExistingInstances()
        {
            if (profile == null || targetSet == null) return;
            int adopted = 0;
            string guid = GetProfileGuid();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Adopt Existing SurfaceWeaver Scatter Instances");
            foreach (SurfacePaintTarget target in ResolveTargets())
            {
                if (target == null) continue;
                if (!TryFindExistingTargetContainer(target, out Transform container)) continue;
                for (int i = 0; i < container.childCount; i++)
                {
                    Transform child = container.GetChild(i);
                    if (child == null || child.GetComponent<SurfaceScatterInstanceData>() != null) continue;
                    GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) as GameObject;
                    if (sourcePrefab == null) continue;
                    SurfaceScatterPrefabEntry entry = FindEntry(null, sourcePrefab);
                    if (entry == null) continue;
                    SurfaceScatterInstanceData data = Undo.AddComponent<SurfaceScatterInstanceData>(child.gameObject);
                    data.profileGuid = guid;
                    data.prefabEntryId = entry.entryId;
                    data.sourcePrefab = sourcePrefab;
                    data.surfaceNormal = Vector3.up;
                    data.surfacePoint = child.position - Vector3.up * profile.surfaceOffset;
                    MeshCollider targetCollider = target.GetComponent<MeshCollider>();
                    if (targetCollider != null)
                    {
                        Ray adoptionRay = new Ray(child.position + Vector3.up * profile.projectionDistance, Vector3.down);
                        if (targetCollider.Raycast(adoptionRay, out RaycastHit adoptionHit, profile.projectionDistance * 2f))
                        {
                            data.surfaceNormal = adoptionHit.normal;
                            data.surfacePoint = adoptionHit.point;
                        }
                    }

                    // Recover the previous uniform random scale from the prefab's source scale.
                    data.prefabBaseScale = sourcePrefab.transform.localScale;
                    float scaleRatio = EstimateUniformScale(child.localScale, data.prefabBaseScale);
                    float scaleMin = Mathf.Max(0.01f, Mathf.Min(entry.minimumScale, entry.maximumScale));
                    float scaleMax = Mathf.Max(scaleMin, Mathf.Max(entry.minimumScale, entry.maximumScale));
                    data.normalizedScale = scaleMax > scaleMin
                        ? Mathf.Clamp01(Mathf.InverseLerp(scaleMin, scaleMax, scaleRatio))
                        : 0.5f;

                    // Preserve the existing random rotation as normalized values so later profile edits can re-evaluate it.
                    Quaternion alignedBase = profile.alignToSurfaceNormal
                        ? Quaternion.FromToRotation(Vector3.up, data.surfaceNormal.normalized)
                        : Quaternion.identity;
                    Quaternion authoredBase = alignedBase * Quaternion.Euler(entry.rotationOffset);
                    Quaternion randomDelta = Quaternion.Inverse(authoredBase) * child.rotation;
                    Vector3 randomEuler = randomDelta.eulerAngles;
                    float signedYaw = Mathf.DeltaAngle(0f, randomEuler.y);
                    float signedPitch = Mathf.DeltaAngle(0f, randomEuler.x);
                    float signedRoll = Mathf.DeltaAngle(0f, randomEuler.z);
                    data.normalizedYaw = profile.randomYaw && profile.yawRange > 0.001f
                        ? Mathf.Clamp(signedYaw / (profile.yawRange * 0.5f), -1f, 1f) : 0f;
                    data.normalizedPitch = profile.randomPitchAndRoll && profile.pitchRange > 0.001f
                        ? Mathf.Clamp(signedPitch / profile.pitchRange, -1f, 1f) : 0f;
                    data.normalizedRoll = profile.randomPitchAndRoll && profile.rollRange > 0.001f
                        ? Mathf.Clamp(signedRoll / profile.rollRange, -1f, 1f) : 0f;
                    data.legacyAdopted = false;
                    EditorUtility.SetDirty(data);
                    adopted++;
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
            status = "Adopted " + adopted + " existing prefab instance(s). Their current scale and rotation are preserved as the baseline; global scale and surface offset can now update them.";
            Repaint();
        }

        private static float EstimateUniformScale(Vector3 instanceScale, Vector3 prefabScale)
        {
            float sum = 0f;
            int count = 0;
            if (Mathf.Abs(prefabScale.x) > 0.0001f) { sum += Mathf.Abs(instanceScale.x / prefabScale.x); count++; }
            if (Mathf.Abs(prefabScale.y) > 0.0001f) { sum += Mathf.Abs(instanceScale.y / prefabScale.y); count++; }
            if (Mathf.Abs(prefabScale.z) > 0.0001f) { sum += Mathf.Abs(instanceScale.z / prefabScale.z); count++; }
            return count > 0 ? sum / count : 1f;
        }

        private bool TryFindExistingTargetContainer(SurfacePaintTarget target, out Transform targetContainer)
        {
            targetContainer = null;
            if (target == null || profile == null) return false;
            string profileKey = GetProfileGuid();
            string rootName = RootPrefix + profile.name + " [" + profileKey + "]";
            GameObject[] roots = target.gameObject.scene.GetRootGameObjects();
            GlobalObjectId targetId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            string targetKey = "Target - " + target.name + " [" + targetId + "]";
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null || roots[r].name != rootName) continue;
                for (int c = 0; c < roots[r].transform.childCount; c++)
                {
                    Transform child = roots[r].transform.GetChild(c);
                    if (child != null && child.name == targetKey)
                    {
                        targetContainer = child;
                        return true;
                    }
                }
            }
            return false;
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
