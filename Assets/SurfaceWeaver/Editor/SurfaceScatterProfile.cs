using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Weighted prefab entry used by the editor-only scene scatter tool.</summary>
    [Serializable]
    public sealed class SurfaceScatterPrefabEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Min(0.01f)] public float minimumScale = 0.8f;
        [Min(0.01f)] public float maximumScale = 1.2f;
        public Vector3 rotationOffset;
    }

    /// <summary>
    /// Editor-only settings asset for deterministic, Undoable object and vegetation placement.
    /// It is not referenced by runtime assemblies or required by built players.
    /// </summary>
    [CreateAssetMenu(menuName = "SurfaceWeaver/Scatter Profile", fileName = "SurfaceScatterProfile")]
    public sealed class SurfaceScatterProfile : ScriptableObject
    {
        [Header("Prefab Library")]
        public List<SurfaceScatterPrefabEntry> prefabs = new List<SurfaceScatterPrefabEntry>();

        [Header("Brush")]
        [Min(0.05f)] public float brushRadius = 2f;
        [Min(1)] public int instancesPerStamp = 8;
        [Min(0.05f)] public float strokeSpacing = 1f;
        [Min(0.01f)] public float minimumInstanceDistance = 0.65f;

        [Header("Surface Rules")]
        public bool alignToSurfaceNormal = true;
        [Range(0f, 180f)] public float maximumSlopeAngle = 45f;
        [Min(0f)] public float surfaceOffset = 0.01f;
        [Min(0.1f)] public float projectionDistance = 5f;
        public LayerMask exclusionLayers;
        [Min(0.01f)] public float exclusionRadius = 0.4f;

        [Header("Randomization")]
        public bool randomYaw = true;
        [Range(0f, 360f)] public float yawRange = 360f;
        public bool randomPitchAndRoll;
        [Range(0f, 45f)] public float pitchRange = 10f;
        [Range(0f, 45f)] public float rollRange = 10f;
        public int randomSeed = 12345;

        private void OnValidate()
        {
            brushRadius = Mathf.Max(0.05f, brushRadius);
            instancesPerStamp = Mathf.Clamp(instancesPerStamp, 1, 128);
            strokeSpacing = Mathf.Max(0.05f, strokeSpacing);
            minimumInstanceDistance = Mathf.Max(0.01f, minimumInstanceDistance);
            projectionDistance = Mathf.Max(0.1f, projectionDistance);
            maximumSlopeAngle = Mathf.Clamp(maximumSlopeAngle, 0f, 180f);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            exclusionRadius = Mathf.Max(0.01f, exclusionRadius);
            yawRange = Mathf.Clamp(yawRange, 0f, 360f);
            pitchRange = Mathf.Clamp(pitchRange, 0f, 45f);
            rollRange = Mathf.Clamp(rollRange, 0f, 45f);

            if (prefabs == null)
                prefabs = new List<SurfaceScatterPrefabEntry>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                SurfaceScatterPrefabEntry entry = prefabs[i];
                if (entry == null)
                    continue;
                entry.weight = Mathf.Max(0f, entry.weight);
                entry.minimumScale = Mathf.Max(0.01f, entry.minimumScale);
                entry.maximumScale = Mathf.Max(entry.minimumScale, entry.maximumScale);
            }
        }
    }
}
