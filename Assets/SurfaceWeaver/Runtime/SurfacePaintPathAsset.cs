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
        // Tangents are offsets from LocalPosition, stored in target-local space.
        // Old path assets deserialize these fields as zero with hasCustomTangents=false,
        // so they continue using automatic Catmull-Rom-equivalent tangents.
        [SerializeField] private Vector3 localInTangent;
        [SerializeField] private Vector3 localOutTangent;
        [SerializeField] private bool hasCustomTangents;
        [SerializeField, Range(0.05f, 4f)] private float widthScale;
        [SerializeField, Range(0f, 1f)] private float pressure;
        // Older path assets do not contain width/pressure fields. This flag lets their
        // missing zero values retain neutral defaults (1, 1) after deserialization.
        [SerializeField] private bool hasCustomWidthPressure;

        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalNormal => localNormal;
        public Vector3 LocalInTangent => localInTangent;
        public Vector3 LocalOutTangent => localOutTangent;
        public bool HasCustomTangents => hasCustomTangents;
        public float WidthScale => hasCustomWidthPressure ? Mathf.Clamp(widthScale, 0.05f, 4f) : 1f;
        public float Pressure => hasCustomWidthPressure ? Mathf.Clamp01(pressure) : 1f;
        public bool HasCustomWidthPressure => hasCustomWidthPressure;

        public SurfacePaintPathPoint(Vector3 position, Vector3 normal)
            : this(position, normal, Vector3.zero, Vector3.zero, false, 1f, 1f, false)
        {
        }

        public SurfacePaintPathPoint(
            Vector3 position,
            Vector3 normal,
            Vector3 inTangent,
            Vector3 outTangent,
            bool useCustomTangents)
            : this(position, normal, inTangent, outTangent, useCustomTangents, 1f, 1f, false)
        {
        }

        public SurfacePaintPathPoint(
            Vector3 position,
            Vector3 normal,
            Vector3 inTangent,
            Vector3 outTangent,
            bool useCustomTangents,
            float anchorWidthScale,
            float anchorPressure,
            bool useCustomWidthPressure)
        {
            localPosition = position;
            localNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            localInTangent = inTangent;
            localOutTangent = outTangent;
            hasCustomTangents = useCustomTangents;
            widthScale = Mathf.Clamp(anchorWidthScale, 0.05f, 4f);
            pressure = Mathf.Clamp01(anchorPressure);
            hasCustomWidthPressure = useCustomWidthPressure;
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
        [SerializeField] private bool pressureAffectsWidth = true;
        [SerializeField] private bool pressureAffectsOpacity = true;
        [SerializeField] private bool hasSavedPressureBehaviorSettings;

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
        // Older path assets have no saved pressure behavior; retain neutral legacy behavior.
        public bool PressureAffectsWidth => hasSavedPressureBehaviorSettings ? pressureAffectsWidth : true;
        public bool PressureAffectsOpacity => hasSavedPressureBehaviorSettings ? pressureAffectsOpacity : true;

        // Backward-compatible overload for integrations compiled against preview.2.
        // Existing callers have no pressure profile settings, so neutral (1, 1) point
        // values keep their previous visual behavior.
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
            Configure(
                globalObjectId, displayName, pathPoints, selectedLayerId, pageIndex,
                channel, radius, paintOpacity, brushHardness, brushSpacing,
                eraseOperation, smoothOperation, preset, alphaOverride,
                rotationDegrees, randomizedScale, closePath, randomizeStamps, seed,
                true, true);
        }

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
            int seed,
            bool pressureControlsWidth,
            bool pressureControlsOpacity)
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
            pressureAffectsWidth = pressureControlsWidth;
            pressureAffectsOpacity = pressureControlsOpacity;
            hasSavedPressureBehaviorSettings = true;
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
