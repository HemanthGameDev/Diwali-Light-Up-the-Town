using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    /// <summary>AssetDatabase round-trip tests for SurfaceWeaver's persisted authoring data.</summary>
    public sealed class SurfaceAssetPersistenceTests
    {
        private const string TestRoot = "Assets/SurfaceWeaver/Editor/Tests";
        private string testFolder;
        private SurfacePaintProfile profile;
        private string profilePath;

        [SetUp]
        public void SetUp()
        {
            string folderName = "__SurfaceWeaver_v092_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(TestRoot, folderName);
            testFolder = TestRoot + "/" + folderName;
            profilePath = testFolder + "/TestProfile.asset";
            profile = ScriptableObject.CreateInstance<SurfacePaintProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile)))
                UnityEngine.Object.DestroyImmediate(profile);
            if (!string.IsNullOrEmpty(testFolder))
            {
                AssetDatabase.DeleteAsset(testFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void CreateBlackMaskPage_PersistsProfileReferenceAndBleedSafeImporterSettings()
        {
            Texture2D page = SurfaceMaskAssetUtility.CreateBlackPage(profile, 0, 128, out string error);
            Assert.IsNotNull(page, error);
            Assert.AreEqual(128, page.width);
            Assert.AreEqual(128, page.height);
            Assert.AreSame(page, profile.GetMaskPage(0));

            string pagePath = AssetDatabase.GetAssetPath(page);
            Assert.IsFalse(string.IsNullOrEmpty(pagePath));
            TextureImporter importer = AssetImporter.GetAtPath(pagePath) as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.IsFalse(importer.sRGBTexture);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceUpdate);
            SurfacePaintProfile reloadedProfile = AssetDatabase.LoadAssetAtPath<SurfacePaintProfile>(profilePath);
            Assert.IsNotNull(reloadedProfile);
            Texture2D reloadedPage = reloadedProfile.GetMaskPage(0);
            Assert.IsNotNull(reloadedPage);
            Assert.AreEqual(pagePath, AssetDatabase.GetAssetPath(reloadedPage));
        }

        [Test]
        public void SaveRenderTextureToAsset_PersistsPixelDataAndProfileReference()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                Assert.Ignore("Mask PNG GPU readback persistence test requires an ARGB32 graphics device.");

            // Mask textures store numeric data, not display color. Create the source explicitly
            // as Linear so the test is independent of the project's active color space.
            var source = new RenderTexture(
                16,
                16,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "SurfaceWeaver Persistence Test Source",
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false
            };

            RenderTexture previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            Texture2D decoded = null;
            try
            {
                Assert.IsTrue(source.Create(), "Could not create the test RenderTexture.");
                Assert.IsFalse(source.sRGB,
                    "Mask persistence test source must be linear (sRGB disabled).");

                // Do not encode the test values through an sRGB framebuffer conversion.
                GL.sRGBWrite = false;
                RenderTexture.active = source;
                GL.Clear(false, true, new Color(0.25f, 0.5f, 0.75f, 0.8f));
                RenderTexture.active = previous;

                bool saved = SurfaceMaskAssetUtility.SaveRenderTextureToAsset(
                    source, null, profile, 0, out Texture2D savedAsset, out string error);
                Assert.IsTrue(saved, error);
                Assert.IsNotNull(savedAsset);
                Assert.AreSame(savedAsset, profile.GetMaskPage(0));

                string assetPath = AssetDatabase.GetAssetPath(savedAsset);
                Assert.IsTrue(File.Exists(Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath))), "Mask PNG was not written to disk.");
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                Assert.IsTrue(decoded.LoadImage(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath)))));
                Color32 pixel = decoded.GetPixel(4, 4);
                Assert.That(pixel.r, Is.InRange(62, 66));
                Assert.That(pixel.g, Is.InRange(126, 130));
                Assert.That(pixel.b, Is.InRange(190, 194));
                Assert.That(pixel.a, Is.InRange(202, 206));

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceUpdate);
                SurfacePaintProfile reloaded = AssetDatabase.LoadAssetAtPath<SurfacePaintProfile>(profilePath);
                Assert.IsNotNull(reloaded.GetMaskPage(0));
                Assert.AreEqual(assetPath, AssetDatabase.GetAssetPath(reloaded.GetMaskPage(0)));
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
                source.Release();
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void PathAsset_SaveReload_RetainsAnchorsTangentsWidthPressureAndBrushSettings()
        {
            string path = testFolder + "/RoundTripPath.asset";
            var pathAsset = ScriptableObject.CreateInstance<SurfacePaintPathAsset>();
            AssetDatabase.CreateAsset(pathAsset, path);
            var points = new List<SurfacePaintPathPoint>
            {
                new SurfacePaintPathPoint(new Vector3(1f, 2f, 3f), Vector3.up,
                    Vector3.left * 0.5f, Vector3.right * 0.75f, true, 0.65f, 0.2f, true),
                new SurfacePaintPathPoint(new Vector3(4f, 5f, 6f), Vector3.forward,
                    Vector3.back * 0.25f, Vector3.forward * 0.5f, true, 1.8f, 0.9f, true)
            };

            pathAsset.Configure(
                "persisted-target-id", "Persistence Target", points, "persisted-layer-id", 0,
                SurfaceMaskChannel.Blue, 2.25f, 0.7f, 0.35f, 0.2f,
                false, false, null, null, 35f, 0.15f, true, true, 4321,
                false, true);
            EditorUtility.SetDirty(pathAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            SurfacePaintPathAsset loaded = AssetDatabase.LoadAssetAtPath<SurfacePaintPathAsset>(path);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("persisted-target-id", loaded.TargetGlobalObjectId);
            Assert.AreEqual("persisted-layer-id", loaded.LayerId);
            Assert.AreEqual(2, loaded.Points.Count);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), loaded.Points[0].LocalPosition);
            Assert.IsTrue(loaded.Points[0].HasCustomTangents);
            Assert.AreEqual(Vector3.left * 0.5f, loaded.Points[0].LocalInTangent);
            Assert.AreEqual(0.65f, loaded.Points[0].WidthScale, 0.0001f);
            Assert.AreEqual(0.2f, loaded.Points[0].Pressure, 0.0001f);
            Assert.AreEqual(1.8f, loaded.Points[1].WidthScale, 0.0001f);
            Assert.AreEqual(0.9f, loaded.Points[1].Pressure, 0.0001f);
            Assert.IsTrue(loaded.ClosedLoop);
            Assert.IsFalse(loaded.PressureAffectsWidth);
            Assert.IsTrue(loaded.PressureAffectsOpacity);
            Assert.AreEqual(4321, loaded.RandomSeed);
        }

        [Test]
        public void ScatterProfile_SaveReload_RetainsWeightedPrefabEntriesAndGlobalSettings()
        {
            string prefabPath = testFolder + "/GrassPrefab.prefab";
            GameObject prefabSource = new GameObject("GrassPrefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, prefabPath);
            UnityEngine.Object.DestroyImmediate(prefabSource);
            Assert.IsNotNull(prefab);

            string scatterPath = testFolder + "/ScatterProfile.asset";
            var scatter = ScriptableObject.CreateInstance<SurfaceScatterProfile>();
            scatter.prefabs.Add(new SurfaceScatterPrefabEntry
            {
                prefab = prefab,
                weight = 7.5f,
                minimumScale = 0.6f,
                maximumScale = 1.4f,
                rotationOffset = new Vector3(0f, 15f, 0f)
            });
            scatter.globalScaleMultiplier = new Vector3(1.1f, 1.35f, 0.9f);
            scatter.randomSeed = 98765;
            AssetDatabase.CreateAsset(scatter, scatterPath);
            EditorUtility.SetDirty(scatter);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(scatterPath, ImportAssetOptions.ForceUpdate);

            SurfaceScatterProfile loaded = AssetDatabase.LoadAssetAtPath<SurfaceScatterProfile>(scatterPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.prefabs.Count);
            Assert.AreEqual(prefabPath, AssetDatabase.GetAssetPath(loaded.prefabs[0].prefab));
            Assert.AreEqual(7.5f, loaded.prefabs[0].weight, 0.0001f);
            Assert.AreEqual(0.6f, loaded.prefabs[0].minimumScale, 0.0001f);
            Assert.AreEqual(1.4f, loaded.prefabs[0].maximumScale, 0.0001f);
            Assert.AreEqual(new Vector3(1.1f, 1.35f, 0.9f), loaded.globalScaleMultiplier);
            Assert.AreEqual(98765, loaded.randomSeed);
        }
    }
}
