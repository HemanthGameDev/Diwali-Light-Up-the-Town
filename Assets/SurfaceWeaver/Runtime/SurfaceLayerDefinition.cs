using System;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Data for a single surface material layer. The base layer does not use a mask;
    /// overlay layers reference a mask page and one of its RGBA channels.
    /// </summary>
    [Serializable]
    public sealed class SurfaceLayerDefinition
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private string displayName = "New Surface";
        [SerializeField] private Texture2D albedoTexture;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Vector2 tiling = Vector2.one;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField, Min(0)] private int maskPageIndex;
        [SerializeField] private SurfaceMaskChannel maskChannel = SurfaceMaskChannel.Red;
        [SerializeField] private bool enabled = true;

        public string Id => id;
        public string DisplayName => displayName;
        public Texture2D AlbedoTexture => albedoTexture;
        public Color Tint => tint;
        public Vector2 Tiling => tiling;
        public float Opacity => opacity;
        public int MaskPageIndex => maskPageIndex;
        public SurfaceMaskChannel MaskChannel => maskChannel;
        public bool Enabled => enabled;

        public SurfaceLayerDefinition(string name = "New Surface")
        {
            id = Guid.NewGuid().ToString("N");
            displayName = name;
            tint = Color.white;
            tiling = Vector2.one;
            opacity = 1f;
            maskPageIndex = 0;
            maskChannel = SurfaceMaskChannel.Red;
            enabled = true;
        }

        public void EnsureId()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = Guid.NewGuid().ToString("N");
        }

        public void SetDisplayName(string value) => displayName = value;
    }
}
