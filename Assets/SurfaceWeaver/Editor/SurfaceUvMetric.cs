using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Calculates the local world-space distance represented by one UV unit on the hit triangle.
    /// This lets a brush remain circular in world space even when UV scale is non-uniform.
    /// </summary>
    internal static class SurfaceUvMetric
    {
        private static Mesh cachedMesh;
        private static Vector3[] cachedVertices;
        private static Vector2[] cachedUvs;
        private static int[] cachedTriangles;

        public static bool TryGetWorldPerUv(
            SurfaceSceneHit hit,
            out Vector3 worldPerUvU,
            out Vector3 worldPerUvV,
            out string error)
        {
            worldPerUvU = Vector3.zero;
            worldPerUvV = Vector3.zero;
            error = string.Empty;

            if (hit.Target == null || !hit.Target.TryGetMesh(out Mesh mesh))
            {
                error = "The picked target no longer has a valid mesh.";
                return false;
            }

            if (hit.Target.UvChannel != 0)
            {
                error = "GPU painting in this build requires UV0.";
                return false;
            }

            EnsureCache(mesh);
            if (cachedVertices == null || cachedUvs == null || cachedTriangles == null)
            {
                error = "Could not read the target mesh vertices, triangles or UV0.";
                return false;
            }

            int triangleOffset = hit.TriangleIndex * 3;
            if (triangleOffset < 0 || triangleOffset + 2 >= cachedTriangles.Length)
            {
                error = "The raycast triangle index is outside the mesh triangle array.";
                return false;
            }

            int i0 = cachedTriangles[triangleOffset];
            int i1 = cachedTriangles[triangleOffset + 1];
            int i2 = cachedTriangles[triangleOffset + 2];

            if (!IsValidIndex(i0) || !IsValidIndex(i1) || !IsValidIndex(i2))
            {
                error = "The picked triangle contains an invalid vertex index.";
                return false;
            }

            Vector3 p0 = hit.Target.transform.TransformPoint(cachedVertices[i0]);
            Vector3 p1 = hit.Target.transform.TransformPoint(cachedVertices[i1]);
            Vector3 p2 = hit.Target.transform.TransformPoint(cachedVertices[i2]);

            Vector2 uv0 = cachedUvs[i0];
            Vector2 uv1 = cachedUvs[i1];
            Vector2 uv2 = cachedUvs[i2];

            Vector3 edge1 = p1 - p0;
            Vector3 edge2 = p2 - p0;
            float du1 = uv1.x - uv0.x;
            float dv1 = uv1.y - uv0.y;
            float du2 = uv2.x - uv0.x;
            float dv2 = uv2.y - uv0.y;
            float determinant = du1 * dv2 - du2 * dv1;

            if (Mathf.Abs(determinant) < 0.0000001f)
            {
                error = "The picked triangle has degenerate UV0 coordinates.";
                return false;
            }

            float inverseDeterminant = 1f / determinant;
            worldPerUvU = (edge1 * dv2 - edge2 * dv1) * inverseDeterminant;
            worldPerUvV = (edge2 * du1 - edge1 * du2) * inverseDeterminant;

            if (!IsFinite(worldPerUvU) || !IsFinite(worldPerUvV) ||
                worldPerUvU.sqrMagnitude < 0.0000001f ||
                worldPerUvV.sqrMagnitude < 0.0000001f)
            {
                error = "Could not calculate a stable world-space UV basis for this triangle.";
                return false;
            }

            return true;
        }

        public static void ClearCache()
        {
            cachedMesh = null;
            cachedVertices = null;
            cachedUvs = null;
            cachedTriangles = null;
        }

        private static void EnsureCache(Mesh mesh)
        {
            if (cachedMesh == mesh && cachedVertices != null &&
                cachedUvs != null && cachedTriangles != null)
                return;

            cachedMesh = mesh;
            cachedVertices = mesh.vertices;
            cachedUvs = mesh.uv;
            cachedTriangles = mesh.triangles;

            if (cachedUvs == null || cachedUvs.Length != cachedVertices.Length)
                cachedUvs = null;
        }

        private static bool IsValidIndex(int index)
        {
            return index >= 0 && cachedVertices != null &&
                   cachedUvs != null && index < cachedVertices.Length &&
                   index < cachedUvs.Length;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
