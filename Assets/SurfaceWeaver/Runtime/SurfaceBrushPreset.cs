using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>Reusable brush alpha and default stroke settings.</summary>
    [CreateAssetMenu(
        fileName = "SurfaceBrush",
        menuName = "SurfaceWeaver/Brush Preset",
        order = 21)]
    public sealed class SurfaceBrushPreset : ScriptableObject
    {
        [SerializeField] private string displayName = "Soft Round";
        [Tooltip("Optional grayscale alpha texture. White paints; black does not paint. Null uses a built-in round falloff.")]
        [SerializeField] private Texture2D alphaTexture;
        [SerializeField] private SurfaceBrushSettings defaults =
            SurfaceBrushSettings.Default;
        [SerializeField] private bool randomRotation;
        [Tooltip("Randomly varies each stamp radius by this fraction. 0.2 means +/-20%.")]
        [SerializeField, Range(0f, 0.75f)] private float scaleJitter;

        public string DisplayName => displayName;
        public Texture2D AlphaTexture => alphaTexture;
        public SurfaceBrushSettings Defaults => defaults;
        public bool RandomRotation => randomRotation;
        public float ScaleJitter => scaleJitter;

        /// <summary>Configures a preset. Intended for the Editor brush-library generator and custom tooling.</summary>
        public void Configure(
            string presetName,
            Texture2D brushAlpha,
            SurfaceBrushSettings brushDefaults,
            bool rotateRandomly,
            float randomizedScale)
        {
            displayName = string.IsNullOrWhiteSpace(presetName) ? "Custom Brush" : presetName;
            alphaTexture = brushAlpha;
            defaults = brushDefaults;
            randomRotation = rotateRandomly;
            scaleJitter = Mathf.Clamp(randomizedScale, 0f, 0.75f);
        }
    }
}
