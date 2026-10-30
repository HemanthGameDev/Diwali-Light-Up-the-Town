using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Read-only result of picking a SurfaceWeaver target in the Scene View.</summary>
    internal readonly struct SurfaceSceneHit
    {
        public SurfacePaintTarget Target { get; }
        public MeshCollider Collider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector2 Uv0 { get; }
        public int TriangleIndex { get; }
        public float Distance { get; }

        public SurfaceSceneHit(SurfacePaintTarget target, MeshCollider collider, RaycastHit hit)
        {
            Target = target;
            Collider = collider;
            Point = hit.point;
            Normal = hit.normal;
            Uv0 = hit.textureCoord;
            TriangleIndex = hit.triangleIndex;
            Distance = hit.distance;
        }
    }
}
