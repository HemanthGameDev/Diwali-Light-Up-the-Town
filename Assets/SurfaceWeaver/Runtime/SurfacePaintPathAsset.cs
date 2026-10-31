using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>A waypoint stored in the local space of its SurfacePaintTarget.</summary>
    [Serializable]
    public struct SurfacePaintPathPoint
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localNormal;

        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalNormal => localNormal;

        public SurfacePaintPathPoint(Vector3 position, Vector3 normal)
        {
            localPosition = position;
            localNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
        }
    }

    /// <summary>
    /// Reusable saved path geometry and brush settings for SurfaceWeaver's Editor spline painter.
    /// Points are stored in target-local space so moving or rotating the target preserves the path.
    /// The target is resolved in the Editor by its GlobalObjectId; the Runtime assembly has no Editor dependency.
    /// Applying a path writes into the active mask page; use the session Undo command to revert it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SurfacePaintPath",
        menuName = "SurfaceWeaver/Surface Paint Path",
        order = 22)]
    public sealed class SurfacePaintPathAsset : ScriptableObject
    {
        [SerializeField] private string targetGlobalObjectId;
        [SerializeField] private string targetDisplayName;
        [SerializeField] private List<SurfacePaintPathPoint> points = new List<SurfacePaintPathPoint>();
        [SerializeField] private string layerId;
        [SerializeField, Min(0)] private int maskPageIndex;
        [SerializeField] private SurfaceMaskChannel maskChannel;
        [SerializeField, Min(0.01f)] private float brushRadius = 2f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hardness = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float spacing = 0.25f;
        [SerializeField] private bool erase;
        [SerializeField] private SurfaceBrushPreset brushPreset;
        [SerializeField] private Texture2D brushAlphaOverride;
        [SerializeField, Range(0f, 360f)] private float brushRotationDegrees;
        [SerializeField, Range(0f, 0.75f)] private float scaleJitter;
        [SerializeField] private bool smooth;
        [SerializeField] private bool closedLoop;
        [SerializeField] private bool randomizePerStamp;
        [SerializeField] private int randomSeed = 1;

        public string TargetGlobalObjectId => targetGlobalObjectId;
        public string TargetDisplayName => targetDisplayName;
        public IReadOnlyList<SurfacePaintPathPoint> Points => points;
        public string LayerId => layerId;
        public int MaskPageIndex => maskPageIndex;
        public SurfaceMaskChannel MaskChannel => maskChannel;
        public float BrushRadius => brushRadius;
        public float Opacity => opacity;
        public float Hardness => hardness;
        public float Spacing => spacing;
        public bool Erase => erase;
        public SurfaceBrushPreset BrushPreset => brushPreset;
        public Texture2D BrushAlphaOverride => brushAlphaOverride;
        public float BrushRotationDegrees => brushRotationDegrees;
        public float ScaleJitter => scaleJitter;
        public bool Smooth => smooth;
        public bool ClosedLoop => closedLoop;
        public bool RandomizePerStamp => randomizePerStamp;
        public int RandomSeed => randomSeed;

        public void Configure(
            string globalObjectId,
            string displayName,
            IList<SurfacePaintPathPoint> pathPoints,
            string selectedLayerId,
            int pageIndex,
            SurfaceMaskChannel channel,
            float radius,
            float paintOpacity,
            float brushHardness,
            float brushSpacing,
            bool eraseOperation,
            bool smoothOperation,
            SurfaceBrushPreset preset,
            Texture2D alphaOverride,
            float rotationDegrees,
            float randomizedScale,
            bool closePath,
            bool randomizeStamps,
            int seed)
        {
            targetGlobalObjectId = globalObjectId ?? string.Empty;
            targetDisplayName = displayName ?? string.Empty;
            points = pathPoints != null
                ? new List<SurfacePaintPathPoint>(pathPoints)
                : new List<SurfacePaintPathPoint>();
            layerId = selectedLayerId ?? string.Empty;
            maskPageIndex = Mathf.Max(0, pageIndex);
            maskChannel = channel;
            brushRadius = Mathf.Max(0.01f, radius);
            opacity = Mathf.Clamp01(paintOpacity);
            hardness = Mathf.Clamp01(brushHardness);
            spacing = Mathf.Clamp(brushSpacing, 0.05f, 1f);
            erase = eraseOperation;
            smooth = smoothOperation;
            brushPreset = preset;
            brushAlphaOverride = alphaOverride;
            brushRotationDegrees = Mathf.Repeat(rotationDegrees, 360f);
            scaleJitter = Mathf.Clamp(randomizedScale, 0f, 0.75f);
            closedLoop = closePath;
            randomizePerStamp = randomizeStamps;
            randomSeed = seed;
        }

        private void OnValidate()
        {
            if (points == null)
                points = new List<SurfacePaintPathPoint>();
            maskPageIndex = Mathf.Max(0, maskPageIndex);
            brushRadius = Mathf.Max(0.01f, brushRadius);
            opacity = Mathf.Clamp01(opacity);
            hardness = Mathf.Clamp01(hardness);
            spacing = Mathf.Clamp(spacing, 0.05f, 1f);
            brushRotationDegrees = Mathf.Repeat(brushRotationDegrees, 360f);
            scaleJitter = Mathf.Clamp(scaleJitter, 0f, 0.75f);
        }
    }
}
