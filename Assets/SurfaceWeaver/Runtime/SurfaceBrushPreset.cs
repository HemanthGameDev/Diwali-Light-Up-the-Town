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

        public string DisplayName => displayName;
        public Texture2D AlphaTexture => alphaTexture;
        public SurfaceBrushSettings Defaults => defaults;
        public bool RandomRotation => randomRotation;
    }
}
