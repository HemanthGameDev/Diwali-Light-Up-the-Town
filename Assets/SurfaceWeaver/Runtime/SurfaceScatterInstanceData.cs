using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>Serialized authoring metadata for editor-managed scatter instances. Contains no runtime behavior.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class SurfaceScatterInstanceData : MonoBehaviour
    {
        [HideInInspector] public string profileGuid;
        [HideInInspector] public string prefabEntryId;
        [HideInInspector] public GameObject sourcePrefab;
        [HideInInspector] public Vector3 surfacePoint;
        [HideInInspector] public Vector3 surfaceNormal = Vector3.up;
        [HideInInspector] public Vector3 prefabBaseScale = Vector3.one;
        [HideInInspector, Range(0f, 1f)] public float normalizedScale = 0.5f;
        [HideInInspector, Range(-1f, 1f)] public float normalizedYaw;
        [HideInInspector, Range(-1f, 1f)] public float normalizedPitch;
        [HideInInspector, Range(-1f, 1f)] public float normalizedRoll;

        [HideInInspector] public bool legacyAdopted;
        [HideInInspector] public Vector3 legacyBaseLocalScale = Vector3.one;
        [HideInInspector] public Quaternion legacyBaseWorldRotation = Quaternion.identity;
    }
}
