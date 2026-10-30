using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Marks a static MeshFilter + MeshRenderer object as a SurfaceWeaver authoring target.
    /// The component stores references only; editor painting data is saved in profile assets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SurfacePaintTarget : MonoBehaviour
    {
        [SerializeField] private SurfacePaintProfile profile;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private int uvChannel;

        public SurfacePaintProfile Profile => profile;
        public MeshFilter TargetMeshFilter => meshFilter;
        public MeshRenderer TargetRenderer => meshRenderer;
        public int UvChannel => uvChannel;

        public bool TryGetMesh(out Mesh mesh)
        {
            mesh = null;

            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshFilter == null)
                return false;

            mesh = meshFilter.sharedMesh;
            return mesh != null;
        }

        public bool TryGetRenderer(out MeshRenderer renderer)
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            renderer = meshRenderer;
            return renderer != null;
        }

        public void SetProfile(SurfacePaintProfile value)
        {
            profile = value;
        }

        public void SetUvChannel(int value)
        {
            uvChannel = Mathf.Clamp(value, 0, 7);
        }

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnValidate()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            uvChannel = Mathf.Clamp(uvChannel, 0, 7);
        }
    }
}
