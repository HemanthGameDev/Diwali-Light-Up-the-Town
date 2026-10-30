using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Performs target-specific Scene View raycasts and returns UV0 hit data.</summary>
    internal static class SurfaceScenePicker
    {
        private const float MaxRayDistance = 100000f;

        public static bool TryPick(
            SurfacePaintTarget target,
            Vector2 guiPosition,
            out SurfaceSceneHit sceneHit,
            out string status)
        {
            sceneHit = default;
            status = string.Empty;

            if (target == null)
            {
                status = "Select a SurfacePaintTarget to preview the brush.";
                return false;
            }

            if (target.UvChannel != 0)
            {
                status = "Scene picking currently reads UV0. Set the target UV Channel to 0.";
                return false;
            }

            if (!target.TryGetMesh(out Mesh mesh))
            {
                status = "The target has no valid MeshFilter mesh.";
                return false;
            }

            if (!mesh.isReadable)
            {
                status = "The mesh is not readable. Enable Read/Write in its Model Import Settings.";
                return false;
            }

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                status = "Add or refresh a non-convex MeshCollider on the target.";
                return false;
            }

            if (meshCollider.convex)
            {
                status = "The target MeshCollider must have Convex disabled.";
                return false;
            }

            if (meshCollider.sharedMesh != mesh)
            {
                status = "The MeshCollider is using a different mesh. Refresh the collider.";
                return false;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            if (!meshCollider.Raycast(ray, out RaycastHit hit, MaxRayDistance))
            {
                status = "Move the pointer over the selected target mesh.";
                return false;
            }

            Vector2 uv = hit.textureCoord;
            if (!IsFinite(uv.x) || !IsFinite(uv.y))
            {
                status = "The hit returned non-finite UV coordinates. Recheck the target UV0 map.";
                return false;
            }

            sceneHit = new SurfaceSceneHit(target, meshCollider, hit);
            status = "Target hit successfully.";
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
