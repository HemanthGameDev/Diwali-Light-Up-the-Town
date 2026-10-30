using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Creates editable starter brush alpha textures and ScriptableObject presets.</summary>
    public static class SurfaceBrushPresetFactory
    {
        private const int TextureSize = 256;
        private const string RootFolder = "Assets/SurfaceWeaver/BrushLibrary";
        private const string StarterFolder = RootFolder + "/Starter";

        [MenuItem("Tools/SurfaceWeaver/Create Starter Brush Library", false, 20)]
        public static void CreateStarterBrushLibraryMenu()
        {
            SurfaceBrushPreset first = CreateStarterBrushLibrary();
            if (first != null)
                EditorGUIUtility.PingObject(first);
        }

        /// <summary>Creates Soft Round, Hard Round, Grunge and Scatter presets. Returns Soft Round.</summary>
        public static SurfaceBrushPreset CreateStarterBrushLibrary()
        {
            EnsureFolder("Assets", "SurfaceWeaver");
            EnsureFolder("Assets/SurfaceWeaver", "BrushLibrary");
            EnsureFolder(RootFolder, "Starter");

            SurfaceBrushPreset soft = CreatePreset(
                "Soft Round",
                "SoftRound",
                BuildSoftRound(),
                new SurfaceBrushSettings
                {
                    RadiusWorldUnits = 2f,
                    Opacity = 0.25f,
                    Hardness = 0.15f,
                    Spacing = 0.20f,
                    Jitter = 0f
                },
                false,
                0f);

            CreatePreset(
                "Hard Round",
                "HardRound",
                BuildHardRound(),
                new SurfaceBrushSettings
                {
                    RadiusWorldUnits = 1.5f,
                    Opacity = 0.35f,
                    Hardness = 0.95f,
                    Spacing = 0.20f,
                    Jitter = 0f
                },
                false,
                0f);

            CreatePreset(
                "Grunge",
                "Grunge",
                BuildGrunge(),
                new SurfaceBrushSettings
                {
                    RadiusWorldUnits = 2.5f,
                    Opacity = 0.35f,
                    Hardness = 0.05f,
                    Spacing = 0.18f,
                    Jitter = 0.1f
                },
                true,
                0.18f);

            CreatePreset(
                "Scatter",
                "Scatter",
                BuildScatter(),
                new SurfaceBrushSettings
                {
                    RadiusWorldUnits = 1.8f,
                    Opacity = 0.55f,
                    Hardness = 0.15f,
                    Spacing = 0.35f,
                    Jitter = 0.25f
                },
                true,
                0.30f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "SurfaceWeaver Brush Library",
                "Created four starter brush presets in " + StarterFolder + ".\n\n" +
                "Assign a preset in the Painter window or edit its alpha texture to make a custom brush.",
                "OK");
            return soft;
        }

        private static SurfaceBrushPreset CreatePreset(
            string displayName,
            string fileStem,
            byte[] alpha,
            SurfaceBrushSettings settings,
            bool randomRotation,
            float scaleJitter)
        {
            string texturePath = AssetDatabase.GenerateUniqueAssetPath(
                StarterFolder + "/" + fileStem + ".png");
            string relativeToAssets = texturePath.Substring("Assets/".Length)
                .Replace('/', Path.DirectorySeparatorChar);
            string absolutePath = Path.Combine(Application.dataPath, relativeToAssets);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, EncodeGrayscale(alpha));
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureBrushImporter(texturePath);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                throw new InvalidOperationException("Could not import generated brush texture: " + texturePath);

            SurfaceBrushPreset preset = ScriptableObject.CreateInstance<SurfaceBrushPreset>();
            preset.Configure(displayName, texture, settings, randomRotation, scaleJitter);
            string presetPath = AssetDatabase.GenerateUniqueAssetPath(
                StarterFolder + "/" + fileStem + ".asset");
            AssetDatabase.CreateAsset(preset, presetPath);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static byte[] EncodeGrayscale(byte[] values)
        {
            Texture2D texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false,
                true);
            Color32[] pixels = new Color32[values.Length];
            for (int i = 0; i < values.Length; i++)
                pixels[i] = new Color32(values[i], values[i], values[i], 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        private static void ConfigureBrushImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
        }

        private static byte[] BuildSoftRound()
        {
            byte[] result = new byte[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float radius = RadiusFromCenter(x, y);
                float value = 1f - SmoothStep(0.48f, 1f, radius);
                result[y * TextureSize + x] = ToByte(value);
            }
            return result;
        }

        private static byte[] BuildHardRound()
        {
            byte[] result = new byte[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float radius = RadiusFromCenter(x, y);
                float edge = 0.94f;
                float value = 1f - SmoothStep(edge - 0.025f, edge + 0.025f, radius);
                result[y * TextureSize + x] = ToByte(value);
            }
            return result;
        }

        private static byte[] BuildGrunge()
        {
            byte[] result = new byte[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float radius = RadiusFromCenter(x, y);
                float envelope = 1f - SmoothStep(0.72f, 1f, radius);
                float u = x / (float)TextureSize;
                float v = y / (float)TextureSize;
                float noise =
                    ValueNoise(u * 5f, v * 5f, 17) * 0.55f +
                    ValueNoise(u * 13f, v * 13f, 31) * 0.30f +
                    ValueNoise(u * 31f, v * 31f, 53) * 0.15f;
                float mottling = SmoothStep(0.18f, 0.72f, noise);
                float fine = 0.72f + 0.28f * Hash01(x, y, 91);
                result[y * TextureSize + x] = ToByte(envelope * mottling * fine);
            }
            return result;
        }

        private static byte[] BuildScatter()
        {
            byte[] result = new byte[TextureSize * TextureSize];
            const int dotCount = 42;
            Vector2[] centers = new Vector2[dotCount];
            float[] radii = new float[dotCount];
            System.Random random = new System.Random(2419);
            for (int i = 0; i < dotCount; i++)
            {
                centers[i] = new Vector2(
                    0.14f + (float)random.NextDouble() * 0.72f,
                    0.14f + (float)random.NextDouble() * 0.72f);
                radii[i] = 0.018f + (float)random.NextDouble() * 0.045f;
            }

            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 uv = new Vector2(
                    (x + 0.5f) / TextureSize,
                    (y + 0.5f) / TextureSize);
                float nearest = 0f;
                for (int i = 0; i < dotCount; i++)
                {
                    float distance = Vector2.Distance(uv, centers[i]);
                    float dot = 1f - SmoothStep(radii[i] * 0.65f, radii[i], distance);
                    nearest = Mathf.Max(nearest, dot);
                }
                float envelope = 1f - SmoothStep(0.52f, 0.72f, RadiusFromCenter(x, y));
                result[y * TextureSize + x] = ToByte(nearest * envelope);
            }
            return result;
        }

        private static float RadiusFromCenter(int x, int y)
        {
            float u = ((x + 0.5f) / TextureSize - 0.5f) * 2f;
            float v = ((y + 0.5f) / TextureSize - 0.5f) * 2f;
            return Mathf.Sqrt(u * u + v * v);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Mathf.Lerp(Hash01(x0, y0, seed), Hash01(x0 + 1, y0, seed), tx);
            float b = Mathf.Lerp(Hash01(x0, y0 + 1, seed), Hash01(x0 + 1, y0 + 1, seed), tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int hash = x * 374761393 + y * 668265263 + seed * 1442695041;
                hash = (hash ^ (hash >> 13)) * 1274126177;
                hash ^= hash >> 16;
                return (hash & 0x7fffffff) / 2147483647f;
            }
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
