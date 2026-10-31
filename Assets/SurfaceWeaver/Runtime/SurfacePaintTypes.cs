using System;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>RGBA channel used to store one painted surface layer.</summary>
    public enum SurfaceMaskChannel
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3
    }

    /// <summary>Editor painting operation. Runtime consumers do not need this enum.</summary>
    public enum SurfacePaintOperation
    {
        Paint,
        Erase,
        Smooth,
        Fill
    }


    /// <summary>Coordinate source used to sample base and overlay albedo textures.</summary>
    public enum SurfaceTextureMappingMode
    {
        /// <summary>Samples textures using the mesh UV0 coordinates.</summary>
        MeshUV0,

        /// <summary>Samples textures using world XZ coordinates. Best suited to mostly horizontal environments.</summary>
        WorldPlanarXZ
    }

    /// <summary>Texture mask storage layout.</summary>
    public enum SurfaceMaskStorageMode
    {
        /// <summary>Each RGBA mask texture can store four independent layer masks.</summary>
        PackedRgbaPages,

        /// <summary>Each painted layer owns a dedicated grayscale mask texture.</summary>
        SeparateTextures
    }

    [Serializable]
    public struct SurfaceBrushSettings
    {
        public float RadiusWorldUnits;
        [Range(0f, 1f)] public float Opacity;
        [Range(0f, 1f)] public float Hardness;
        [Min(0.01f)] public float Spacing;
        [Range(0f, 1f)] public float Jitter;

        public static SurfaceBrushSettings Default
        {
            get
            {
                return new SurfaceBrushSettings
                {
                    RadiusWorldUnits = 2f,
                    Opacity = 0.25f,
                    Hardness = 0.2f,
                    Spacing = 0.25f,
                    Jitter = 0f
                };
            }
        }
    }
}
