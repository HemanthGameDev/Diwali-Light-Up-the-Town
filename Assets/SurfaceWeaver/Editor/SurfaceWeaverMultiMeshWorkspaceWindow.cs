using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Manages reusable sets of SurfacePaintTargets and opens each target in the existing
    /// single-target Painter. It deliberately does not alter Painter target selection,
    /// spline authoring, colliders, profiles, materials or mesh transforms.
    /// </summary>
    public sealed class SurfaceWeaverMultiMeshWorkspaceWindow : EditorWindow
    {
        private sealed class TargetRow
        {
            public string GlobalId;
            public SurfacePaintTarget Target;
            public string Status;
            public MessageType StatusType;
        }

        [SerializeField] private SurfacePaintTargetSet targetSet;
        [SerializeField] private Vector2 scroll;
        private readonly List<TargetRow> rows = new List<TargetRow>();
        private string report = "Create or assign a target set, then add SurfacePaintTargets from the Hierarchy.";

        [MenuItem("Window/SurfaceWeaver/Multi-Mesh Workspace", false, 2110)]
        public static void Open()
        {
            SurfaceWeaverMultiMeshWorkspaceWindow window = GetWindow<SurfaceWeaverMultiMeshWorkspaceWindow>();
            window.titleContent = new GUIContent("SW Multi-Mesh");
            window.minSize = new Vector2(440f, 420f);
            window.RefreshRows();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshRows();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SURFACEWEAVER — MULTI-MESH WORKSPACE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Organize multiple scene targets and edit each one with the existing Painter.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "This workspace is separate from the Painter. It does not add or remove target components, " +
                "register extra colliders, move meshes, or change spline behavior. The Painter remains " +
                "single-active-target; use Open Painter on one row at a time. Save the current path asset " +
                "and mask before switching to another target. Continuous brush strokes across mesh boundaries " +
                "are not part of this preview.",
                MessageType.Info);

            DrawTargetSetField();
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawSummary();
            DrawRows();

            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(report, MessageType.Info);
            }
        }

        private void DrawTargetSetField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                SurfacePaintTargetSet next = (SurfacePaintTargetSet)EditorGUILayout.ObjectField(
                    "Target Set", targetSet, typeof(SurfacePaintTargetSet), false);
                if (EditorGUI.EndChangeCheck())
                {
                    targetSet = next;
                    RefreshRows();
                }

                if (GUILayout.Button("New…", GUILayout.Width(58f)))
                    CreateTargetSet();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUI.DisabledScope(targetSet == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Selected Targets", GUILayout.Height(25f)))
                        AddSelectedTargets();
                    if (GUILayout.Button("Find All Loaded", GUILayout.Height(25f)))
                        AddAllLoadedTargets();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All in Set"))
                        SelectAllTargets();
                    if (GUILayout.Button("Clear Set"))
                        ClearTargetSet();
                }
            }

            using (new EditorGUI.DisabledScope(targetSet == null || targetSet.Count == 0))
            {
                if (GUILayout.Button("Open Batch Mask Export", GUILayout.Height(24f)))
                    OpenBatchExporter();
            }
        }

        private void DrawSummary()
        {
            int valid = rows.Count(row => row.Target != null && row.StatusType == MessageType.None);
            EditorGUILayout.LabelField(
                string.Format("Targets in set: {0}    Resolved: {1}    Ready: {2}",
                    rows.Count, rows.Count(row => row.Target != null), valid),
                EditorStyles.boldLabel);
        }

        private void DrawRows()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180f));
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No targets in this set yet. Select GameObjects with existing SurfacePaintTarget components and click Add Selected Targets.", MessageType.Info);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow row = rows[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string title = row.Target != null ? row.Target.name : "Missing scene target";
                        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (row.Target != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                            EditorGUIUtility.PingObject(row.Target.gameObject);
                        if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                        {
                            RemoveAt(i);
                            break;
                        }
                    }

                    if (row.Target != null)
                    {
                        SurfacePaintTarget paintTarget = row.Target;
                        EditorGUILayout.LabelField("Scene", paintTarget.gameObject.scene.name);
                        EditorGUILayout.LabelField("Profile", paintTarget.Profile != null ? paintTarget.Profile.name : "Not assigned");
                        if (paintTarget.TryGetMesh(out Mesh mesh) && mesh != null)
                            EditorGUILayout.LabelField("Mesh", mesh.name + "  •  " + mesh.vertexCount + " vertices");
                    }

                    if (!string.IsNullOrEmpty(row.Status))
                        EditorGUILayout.HelpBox(row.Status, row.StatusType);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(row.Target == null))
                        {
                            if (GUILayout.Button("Select in Hierarchy"))
                            {
                                Selection.activeGameObject = row.Target.gameObject;
                                EditorGUIUtility.PingObject(row.Target.gameObject);
                            }

                            if (GUILayout.Button("Open Painter", GUILayout.Height(24f)))
                                OpenPainterFor(row.Target);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateTargetSet()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create SurfaceWeaver Target Set",
                "SurfaceWeaverTargetSet",
                "asset",
                "Choose a folder inside Assets for the reusable target-set asset.");
            if (string.IsNullOrEmpty(path))
                return;

            SurfacePaintTargetSet created = CreateInstance<SurfacePaintTargetSet>();
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            targetSet = created;
            EditorGUIUtility.PingObject(created);
            report = "Created a reusable target set. Add saved-scene SurfacePaintTargets to it.";
            RefreshRows();
        }

        private void AddSelectedTargets()
        {
            if (!EnsureTargetSet())
                return;

            List<SurfacePaintTarget> selected = new List<SurfacePaintTarget>();
            UnityEngine.Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                GameObject selectedObject = selection[i] as GameObject;
                if (selectedObject == null && selection[i] is Component component)
                    selectedObject = component.gameObject;

                if (selectedObject == null)
                    continue;

                SurfacePaintTarget selectedTarget = selectedObject.GetComponent<SurfacePaintTarget>();
                if (selectedTarget != null)
                    selected.Add(selectedTarget);
            }
            int added = AddTargets(selected);
            report = added > 0
                ? "Added " + added + " target(s) to the workspace."
                : "No new targets added. Select GameObjects with existing SurfacePaintTarget components. Save their scenes before adding them to a reusable asset.";
            RefreshRows();
        }

        private void AddAllLoadedTargets()
        {
            if (!EnsureTargetSet())
                return;

            SurfacePaintTarget[] all = Resources.FindObjectsOfTypeAll<SurfacePaintTarget>();
            List<SurfacePaintTarget> sceneTargets = new List<SurfacePaintTarget>();
            for (int i = 0; i < all.Length; i++)
            {
                SurfacePaintTarget candidate = all[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                Scene scene = candidate.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                sceneTargets.Add(candidate);
            }

            int added = AddTargets(sceneTargets);
            report = "Found " + sceneTargets.Count + " loaded-scene target(s); added " + added + " new target(s).";
            RefreshRows();
        }

        private int AddTargets(IEnumerable<SurfacePaintTarget> candidates)
        {
            int added = 0;
            int skippedUnsaved = 0;
            foreach (SurfacePaintTarget candidate in candidates ?? Enumerable.Empty<SurfacePaintTarget>())
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.gameObject.scene.path))
                {
                    skippedUnsaved++;
                    continue;
                }

                GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(candidate);
                string id = globalId.ToString();
                if (!string.IsNullOrEmpty(id) && targetSet.Add(id))
                {
                    added++;
                    EditorUtility.SetDirty(targetSet);
                }
            }

            if (skippedUnsaved > 0)
                report = skippedUnsaved + " target(s) skipped because their scene has not been saved. Save the scene, then add them again.";
            AssetDatabase.SaveAssets();
            return added;
        }

        private void SelectAllTargets()
        {
            List<GameObject> objects = rows
                .Where(row => row.Target != null)
                .Select(row => row.Target.gameObject)
                .Distinct()
                .ToList();
            Selection.objects = objects.Cast<UnityEngine.Object>().ToArray();
            if (objects.Count > 0)
                EditorGUIUtility.PingObject(objects[0]);
        }

        private void ClearTargetSet()
        {
            if (targetSet == null || targetSet.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Clear SurfaceWeaver Target Set",
                    "Remove all entries from this workspace asset? Scene objects, profiles, masks and materials will not be changed.",
                    "Clear Set", "Cancel"))
                return;

            targetSet.Clear();
            EditorUtility.SetDirty(targetSet);
            AssetDatabase.SaveAssets();
            report = "Target set cleared. No scene objects or assets were modified.";
            RefreshRows();
        }

        private void RemoveAt(int index)
        {
            if (targetSet == null || index < 0 || index >= rows.Count)
                return;

            targetSet.RemoveAt(index);
            EditorUtility.SetDirty(targetSet);
            AssetDatabase.SaveAssets();
            report = "Removed the target from this workspace only. The scene object and its SurfacePaintTarget component were not changed.";
            RefreshRows();
        }

        private void OpenPainterFor(SurfacePaintTarget paintTarget)
        {
            if (paintTarget == null)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Open Single-Target Painter",
                "The Painter edits one target at a time. Before switching targets, save the current mask and save the current spline as a Path Asset. This workspace will not change the Painter's spline or target workflow.",
                "Open Painter", "Cancel");
            if (!confirmed)
                return;

            Selection.activeGameObject = paintTarget.gameObject;
            SurfaceWeaverWindow.OpenFor(paintTarget);
        }

        private void OpenBatchExporter()
        {
            // The existing batch exporter refreshes from Hierarchy selection. Select the set first,
            // then invoke its established menu command to avoid changing its implementation.
            SelectAllTargets();
            EditorApplication.delayCall += () =>
                EditorApplication.ExecuteMenuItem("Tools/SurfaceWeaver/Batch Export Masks");
        }

        private bool EnsureTargetSet()
        {
            if (targetSet != null)
                return true;

            EditorUtility.DisplayDialog(
                "No Target Set Assigned",
                "Create a new target-set asset or assign an existing one before adding scene targets.",
                "OK");
            return false;
        }

        private void RefreshRows()
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
                SurfacePaintTarget resolved = Resolve(id);
                TargetRow row = new TargetRow
                {
                    GlobalId = id,
                    Target = resolved,
                    StatusType = MessageType.None
                };
                ValidateTarget(row);
                rows.Add(row);
            }

            // Shared profiles intentionally share their mask pages. Surface this clearly so
            // users do not mistake shared-profile behavior for accidental cross-target painting.
            for (int i = 0; i < rows.Count; i++)
            {
                TargetRow row = rows[i];
                if (row.Target == null || row.Target.Profile == null)
                    continue;

                List<string> sharedWith = rows
                    .Where(other => other != row && other.Target != null &&
                                    other.Target.Profile == row.Target.Profile)
                    .Select(other => other.Target.name)
                    .Distinct()
                    .ToList();
                if (sharedWith.Count > 0)
                {
                    string warning = "Shares profile '" + row.Target.Profile.name + "' with " +
                                     string.Join(", ", sharedWith) +
                                     ". These targets share the same profile and mask-page assets.";
                    row.Status = string.IsNullOrEmpty(row.Status) ? warning : row.Status + "\n" + warning;
                    row.StatusType = MessageType.Warning;
                }
            }

            Repaint();
        }

        private static SurfacePaintTarget Resolve(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId globalId))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as SurfacePaintTarget;
        }

        private static void ValidateTarget(TargetRow row)
        {
            SurfacePaintTarget target = row.Target;
            if (target == null)
            {
                row.Status = "This scene target is not currently resolved. Open its saved scene; if the object was deleted, remove this entry manually. Unloaded scenes can also make scene references temporarily unresolved.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (!target.gameObject.activeInHierarchy)
            {
                row.Status = "Target GameObject is inactive. Activate it before editing.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (target.Profile == null)
            {
                row.Status = "No SurfacePaintProfile is assigned.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (!target.TryGetMesh(out Mesh mesh) || mesh == null)
            {
                row.Status = "A valid MeshFilter/sharedMesh is required.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (!mesh.isReadable)
            {
                row.Status = "Mesh is not readable. Enable Read/Write in its import settings.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (target.UvChannel != 0)
            {
                row.Status = "Current painter/shader workflow expects UV0. Set the target UV channel to 0.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (!target.TryGetRenderer(out MeshRenderer renderer) || renderer == null)
            {
                row.Status = "A MeshRenderer is required.";
                row.StatusType = MessageType.Warning;
                return;
            }

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null)
            {
                row.Status = "Ready except for Scene View painting: add a non-convex MeshCollider on this target.";
                row.StatusType = MessageType.Warning;
                return;
            }

            if (collider.convex || collider.sharedMesh != mesh)
            {
                row.Status = "MeshCollider must be non-convex and reference the same mesh as the MeshFilter.";
                row.StatusType = MessageType.Warning;
                return;
            }

            row.Status = "Ready for the existing single-target Painter.";
            row.StatusType = MessageType.None;
        }
    }
}
