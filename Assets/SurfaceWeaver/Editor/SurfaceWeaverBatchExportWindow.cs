using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Exports persisted mask-page source files for multiple selected SurfacePaintTargets.
    /// This is deliberately separate from the Painter's single-active-target editing workflow.
    /// </summary>
    public sealed class SurfaceWeaverBatchExportWindow : EditorWindow
    {
        private sealed class TargetEntry
        {
            public SurfacePaintTarget Target;
            public SurfacePaintProfile Profile;
            public readonly List<PageEntry> Pages = new List<PageEntry>();
            public string Status;
        }

        private sealed class PageEntry
        {
            public int PageIndex;
            public Texture2D Texture;
            public string AssetPath;
        }

        private readonly List<TargetEntry> targets = new List<TargetEntry>();
        private Vector2 scroll;
        private string outputFolder;
        private bool createTargetSubfolders = true;
        private bool writeManifest = true;
        private bool showOnlyValidTargets;
        private string lastReport = "Select multiple SurfacePaintTarget GameObjects in the Hierarchy, then refresh the selection.";

        [MenuItem("Tools/SurfaceWeaver/Batch Export Masks", false, 40)]
        public static void Open()
        {
            SurfaceWeaverBatchExportWindow window = GetWindow<SurfaceWeaverBatchExportWindow>();
            window.titleContent = new GUIContent("SW Batch Export");
            window.minSize = new Vector2(440f, 420f);
            window.RefreshFromSelection();
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(outputFolder))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                outputFolder = Path.Combine(projectRoot, "SurfaceWeaver_Exports");
            }
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SURFACEWEAVER — BATCH MASK EXPORT", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Export saved mask-page files from multiple targets without changing the Painter's active target.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "Select multiple GameObjects that already have SurfacePaintTarget components in the Hierarchy. " +
                "This utility exports persisted mask-page source files only; it does not merge masks or perform multi-target painting.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh From Selection", GUILayout.Height(26f)))
                    RefreshFromSelection();
                if (GUILayout.Button("Find All Loaded Scene Targets", GUILayout.Height(26f)))
                    FindAllLoadedSceneTargets();
            }

            showOnlyValidTargets = EditorGUILayout.ToggleLeft("Show only targets with exportable mask pages", showOnlyValidTargets);
            EditorGUILayout.Space(4f);

            int pageCount = CountPages();
            int validTargetCount = targets.Count(t => t.Pages.Count > 0);
            EditorGUILayout.LabelField(
                string.Format("Targets: {0}   Exportable targets: {1}   Mask pages: {2}", targets.Count, validTargetCount, pageCount),
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(150f));
            for (int i = 0; i < targets.Count; i++)
            {
                TargetEntry entry = targets[i];
                if (showOnlyValidTargets && entry.Pages.Count == 0)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(entry.Target != null ? entry.Target.name : "Missing target", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (entry.Target != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                            EditorGUIUtility.PingObject(entry.Target.gameObject);
                    }

                    EditorGUILayout.LabelField("Profile", entry.Profile != null ? entry.Profile.name : "Not assigned");
                    if (!string.IsNullOrEmpty(entry.Status))
                        EditorGUILayout.HelpBox(entry.Status, entry.Pages.Count > 0 ? MessageType.None : MessageType.Warning);

                    for (int p = 0; p < entry.Pages.Count; p++)
                    {
                        PageEntry page = entry.Pages[p];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                string.Format("Page {0:00}", page.PageIndex),
                                page.Texture != null ? page.Texture.name : "Missing texture",
                                GUILayout.MinWidth(170f));
                            if (page.Texture != null)
                                EditorGUILayout.LabelField(page.Texture.width + " × " + page.Texture.height, GUILayout.Width(100f));
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Export Destination", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField(outputFolder ?? string.Empty);
                if (GUILayout.Button("Browse…", GUILayout.Width(76f)))
                {
                    string chosen = EditorUtility.OpenFolderPanel(
                        "Choose SurfaceWeaver Export Folder",
                        Directory.Exists(outputFolder) ? outputFolder : Application.dataPath,
                        string.Empty);
                    if (!string.IsNullOrEmpty(chosen))
                        outputFolder = chosen;
                }
            }

            createTargetSubfolders = EditorGUILayout.ToggleLeft("Create a separate folder for each target", createTargetSubfolders);
            writeManifest = EditorGUILayout.ToggleLeft("Write CSV export manifest", writeManifest);

            EditorGUILayout.HelpBox(
                "Before exporting, commit any live spline preview and save the active mask page in the Painter. " +
                "The exporter copies saved source files as-is; it will not include unsaved GPU preview contents.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(pageCount == 0 || string.IsNullOrWhiteSpace(outputFolder)))
            {
                if (GUILayout.Button("Export " + pageCount + " Mask Page(s)", GUILayout.Height(32f)))
                    ExportMaskPages();
            }

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(lastReport, MessageType.Info);
            }
        }

        private void RefreshFromSelection()
        {
            SurfacePaintTarget[] selected = Selection.GetFiltered<SurfacePaintTarget>(
                SelectionMode.Editable | SelectionMode.Deep);
            BuildEntries(selected);
        }

        private void FindAllLoadedSceneTargets()
        {
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

            BuildEntries(sceneTargets.ToArray());
        }

        private void BuildEntries(SurfacePaintTarget[] source)
        {
            targets.Clear();
            HashSet<int> seen = new HashSet<int>();

            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    SurfacePaintTarget paintTarget = source[i];
                    if (paintTarget == null || !seen.Add(paintTarget.GetInstanceID()))
                        continue;

                    TargetEntry entry = new TargetEntry { Target = paintTarget };
                    entry.Profile = paintTarget.Profile;

                    if (entry.Profile == null)
                    {
                        entry.Status = "No SurfacePaintProfile is assigned to this target.";
                    }
                    else
                    {
                        IReadOnlyList<Texture2D> maskPages = entry.Profile.MaskPages;
                        if (maskPages == null || maskPages.Count == 0)
                        {
                            entry.Status = "The assigned profile has no mask-page slots.";
                        }
                        else
                        {
                            for (int pageIndex = 0; pageIndex < maskPages.Count; pageIndex++)
                            {
                                Texture2D texture = maskPages[pageIndex];
                                if (texture == null)
                                    continue;

                                string assetPath = AssetDatabase.GetAssetPath(texture);
                                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                                bool supportedExtension = extension == ".png" || extension == ".tga" ||
                                                           extension == ".exr" || extension == ".tif" ||
                                                           extension == ".tiff";
                                string absolutePath = string.IsNullOrEmpty(assetPath)
                                    ? string.Empty
                                    : GetAbsoluteAssetPath(assetPath);

                                PageEntry page = new PageEntry
                                {
                                    PageIndex = pageIndex,
                                    Texture = texture,
                                    AssetPath = assetPath
                                };

                                if (string.IsNullOrEmpty(assetPath) || !File.Exists(absolutePath))
                                {
                                    AppendStatus(entry, "Page " + pageIndex.ToString("00") +
                                        " skipped: texture is not a file-backed project asset.");
                                }
                                else if (!supportedExtension)
                                {
                                    AppendStatus(entry, "Page " + pageIndex.ToString("00") +
                                        " skipped: unsupported lossless source format '" + extension + "'.");
                                }
                                else
                                {
                                    entry.Pages.Add(page);
                                }
                            }

                            if (entry.Pages.Count == 0 && string.IsNullOrEmpty(entry.Status))
                                entry.Status = "No file-backed PNG/TGA/EXR/TIF mask pages were found.";
                        }
                    }

                    targets.Add(entry);
                }
            }

            targets.Sort((a, b) => string.Compare(
                a.Target != null ? a.Target.name : string.Empty,
                b.Target != null ? b.Target.name : string.Empty,
                StringComparison.OrdinalIgnoreCase));

            lastReport = targets.Count == 0
                ? "No SurfacePaintTarget components found. Select one or more targets in the Hierarchy."
                : "Selection refreshed. Save active Painter mask changes before exporting.";
            Repaint();
        }


        private static void AppendStatus(TargetEntry entry, string message)
        {
            if (entry == null || string.IsNullOrEmpty(message))
                return;

            entry.Status = string.IsNullOrEmpty(entry.Status)
                ? message
                : entry.Status + "\n" + message;
        }

        private int CountPages()
        {
            int count = 0;
            for (int i = 0; i < targets.Count; i++)
                count += targets[i].Pages.Count;
            return count;
        }

        private void ExportMaskPages()
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                return;

            int total = CountPages();
            if (total == 0)
                return;

            int exported = 0;
            int failed = 0;
            string exportRoot = Path.GetFullPath(outputFolder);
            List<string> exportedAssetPaths = new List<string>();
            StringBuilder manifest = new StringBuilder();
            if (writeManifest)
                manifest.AppendLine("Target,Profile,PageIndex,SourceAsset,ExportedFile,Width,Height");

            try
            {
                Directory.CreateDirectory(exportRoot);
                int processed = 0;

                for (int i = 0; i < targets.Count; i++)
                {
                    TargetEntry targetEntry = targets[i];
                    if (targetEntry.Target == null || targetEntry.Profile == null)
                        continue;

                    string targetFolder = createTargetSubfolders
                        ? Path.Combine(exportRoot, SanitizeFileName(targetEntry.Target.name))
                        : exportRoot;
                    Directory.CreateDirectory(targetFolder);

                    for (int p = 0; p < targetEntry.Pages.Count; p++)
                    {
                        PageEntry page = targetEntry.Pages[p];
                        processed++;
                        EditorUtility.DisplayProgressBar(
                            "SurfaceWeaver Batch Export",
                            targetEntry.Target.name + " — mask page " + page.PageIndex,
                            processed / (float)total);

                        try
                        {
                            string sourceAbsolute = GetAbsoluteAssetPath(page.AssetPath);
                            string extension = Path.GetExtension(page.AssetPath);
                            string fileName = SanitizeFileName(targetEntry.Target.name) + "_" +
                                              SanitizeFileName(targetEntry.Profile.name) + "_MaskPage_" +
                                              page.PageIndex.ToString("00") + extension;
                            string destination = GetUniqueFilePath(Path.Combine(targetFolder, fileName));

                            if (string.Equals(
                                Path.GetFullPath(sourceAbsolute),
                                Path.GetFullPath(destination),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                failed++;
                                continue;
                            }

                            File.Copy(sourceAbsolute, destination, false);
                            exported++;
                            if (IsInsideAssets(destination))
                                exportedAssetPaths.Add(GetAssetRelativePath(destination));

                            if (writeManifest)
                            {
                                string relativeOrAbsolute = destination;
                                manifest.Append(Csv(targetEntry.Target.name)).Append(',')
                                    .Append(Csv(targetEntry.Profile.name)).Append(',')
                                    .Append(page.PageIndex).Append(',')
                                    .Append(Csv(page.AssetPath)).Append(',')
                                    .Append(Csv(relativeOrAbsolute)).Append(',')
                                    .Append(page.Texture.width).Append(',')
                                    .Append(page.Texture.height).AppendLine();
                            }
                        }
                        catch (Exception exception)
                        {
                            failed++;
                            Debug.LogError("[SurfaceWeaver] Failed to export mask page " + page.PageIndex +
                                           " for target '" + targetEntry.Target.name + "': " + exception.Message);
                        }
                    }
                }

                if (writeManifest)
                {
                    string manifestPath = GetUniqueFilePath(Path.Combine(
                        exportRoot,
                        "SurfaceWeaver_BatchExport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"));
                    File.WriteAllText(manifestPath, manifest.ToString(), new UTF8Encoding(true));
                }

                if (IsInsideAssets(exportRoot))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                    for (int i = 0; i < exportedAssetPaths.Count; i++)
                        SurfaceMaskAssetUtility.ConfigureMaskImporter(exportedAssetPaths[i]);
                }

                int discovered = CountPages();
                lastReport = string.Format("Export complete. Exported: {0} of {1} discovered pages. Failed: {2}. Destination: {3}",
                    exported, discovered, failed, exportRoot);
                Debug.Log("[SurfaceWeaver] " + lastReport);
                EditorUtility.DisplayDialog("SurfaceWeaver Batch Export", lastReport, "OK");
            }
            catch (Exception exception)
            {
                lastReport = "Batch export failed: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("SurfaceWeaver Batch Export", lastReport, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string GetUniqueFilePath(string requestedPath)
        {
            if (!File.Exists(requestedPath) && !Directory.Exists(requestedPath))
                return requestedPath;

            string directory = Path.GetDirectoryName(requestedPath);
            string fileName = Path.GetFileNameWithoutExtension(requestedPath);
            string extension = Path.GetExtension(requestedPath);
            int suffix = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, fileName + "_" + suffix.ToString("000") + extension);
                suffix++;
            }
            while (File.Exists(candidate) || Directory.Exists(candidate));

            return candidate;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "SurfaceWeaver";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
                builder.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "SurfaceWeaver" : result;
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string GetAssetRelativePath(string absolutePath)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath);
            string relative = fullPath.Substring(assetsRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            return "Assets/" + relative;
        }

        private static bool IsInsideAssets(string folder)
        {
            string assets = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullFolder = Path.GetFullPath(folder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullFolder.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fullFolder.TrimEnd(Path.DirectorySeparatorChar),
                       assets.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
    }
}
