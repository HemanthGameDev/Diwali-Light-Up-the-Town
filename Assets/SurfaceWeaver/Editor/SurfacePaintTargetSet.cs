using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Reusable collection of scene SurfacePaintTargets. Scene references are stored as
    /// GlobalObjectId strings because an asset must not hold direct references to scene objects.
    /// </summary>
    [CreateAssetMenu(menuName = "SurfaceWeaver/Multi-Mesh Target Set", fileName = "SurfaceWeaverTargetSet")]
    public sealed class SurfacePaintTargetSet : ScriptableObject
    {
        [SerializeField] private List<string> targetGlobalObjectIds = new List<string>();

        public IReadOnlyList<string> TargetGlobalObjectIds => targetGlobalObjectIds;
        public int Count => targetGlobalObjectIds != null ? targetGlobalObjectIds.Count : 0;

        public bool Add(string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
                return false;

            if (targetGlobalObjectIds == null)
                targetGlobalObjectIds = new List<string>();

            if (targetGlobalObjectIds.Contains(globalObjectId))
                return false;

            targetGlobalObjectIds.Add(globalObjectId);
            return true;
        }

        public bool Remove(string globalObjectId)
        {
            return targetGlobalObjectIds != null && targetGlobalObjectIds.Remove(globalObjectId);
        }

        public void RemoveAt(int index)
        {
            if (targetGlobalObjectIds == null || index < 0 || index >= targetGlobalObjectIds.Count)
                return;

            targetGlobalObjectIds.RemoveAt(index);
        }

        public void Clear()
        {
            targetGlobalObjectIds?.Clear();
        }
    }
}
