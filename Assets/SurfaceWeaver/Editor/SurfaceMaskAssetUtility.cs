using System.IO;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Creates and persists linear PNG assets for packed RGBA mask pages.</summary>
    internal static class SurfaceMaskAssetUtility
    {
        public static Texture2D CreateBlackPage(
            SurfacePaintProfile profile,
            int pageIndex,
            int resolution,
            out string error)
        {
            error = string.Empty;
            if (profile == null)
            {
                error = "Assign a SurfacePaintProfile before creating a mask page.";
                return null;
            }

            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                error = "Save the SurfacePaintProfile inside the project's Assets folder before creating mask pages.";
                return null;
            }

            string folder = Path.GetDirectoryName(profilePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                error = "Could not resolve the profile asset folder.";
                return null;
            }

            resolution = Mathf.Clamp(resolution, 128, 4096);
            Texture2D blank = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = profile.name + "_MaskPage_" + pageIndex.ToString("00")
            };

            Color32[] pixels = new Color32[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            blank.SetPixels32(pixels);
            blank.Apply(false, false);

            string requestedPath = folder + "/" + blank.name + ".png";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            string absolutePath = AssetPathToAbsolutePath(assetPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, blank.EncodeToPNG());
            }
            catch (System.Exception exception)
            {
                error = "Could not create the mask page PNG: " + exception.Message;
                return null;
            }
            finally
            {
                Object.DestroyImmediate(blank);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(assetPath);

            Texture2D page = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (page == null)
            {
                error = "The mask PNG was written but Unity could not import it as a Texture2D.";
                return null;
            }

            Undo.RecordObject(profile, "Create SurfaceWeaver Mask Page");
            profile.SetMaskPage(pageIndex, page);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return page;
        }

        public static bool SaveRenderTextureToAsset(
            RenderTexture source,
            Texture2D existingAsset,
            SurfacePaintProfile profile,
            int pageIndex,
            out Texture2D savedAsset,
            out string error)
        {
            savedAsset = existingAsset;
            error = string.Empty;

            if (source == null || profile == null)
            {
                error = "A valid RenderTexture and SurfacePaintProfile are required.";
                return false;
            }

            string assetPath = existingAsset != null
                ? AssetDatabase.GetAssetPath(existingAsset)
                : string.Empty;

            if (string.IsNullOrEmpty(assetPath))
            {
                string profilePath = AssetDatabase.GetAssetPath(profile);
                if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    error = "Save the SurfacePaintProfile inside the project's Assets folder before saving a new mask page.";
                    return false;
                }

                string folder = Path.GetDirectoryName(profilePath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(folder))
                {
                    error = "Save the SurfacePaintProfile asset before saving a mask page.";
                    return false;
                }

                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + profile.name + "_MaskPage_" + pageIndex.ToString("00") + ".png");
            }

            Texture2D readback = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                true);

            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                readback.ReadPixels(
                    new Rect(0, 0, source.width, source.height),
                    0,
                    0,
                    false);
                readback.Apply(false, false);

                string absolutePath = AssetPathToAbsolutePath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, readback.EncodeToPNG());
            }
            catch (System.Exception exception)
            {
                error = "Could not save the GPU mask page: " + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(readback);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(assetPath);
            savedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (savedAsset == null)
            {
                error = "The mask PNG was written, but Unity could not reload it.";
                return false;
            }

            Undo.RecordObject(profile, "Save SurfaceWeaver Mask Page");
            profile.SetMaskPage(pageIndex, savedAsset);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static void ConfigureMaskImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.isReadable = false;
            importer.maxTextureSize = 8192;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
