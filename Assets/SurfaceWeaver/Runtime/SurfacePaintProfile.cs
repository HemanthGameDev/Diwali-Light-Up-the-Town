using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Reusable surface recipe: a base texture, ordered overlay layers, and mask pages.
    /// This asset contains authoring data only; it does not modify source meshes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SurfacePaintProfile",
        menuName = "SurfaceWeaver/Surface Paint Profile",
        order = 20)]
    public sealed class SurfacePaintProfile : ScriptableObject
    {
        [Header("Base Surface")]
        [SerializeField] private Texture2D baseAlbedo;
        [SerializeField] private Color baseTint = Color.white;
        [SerializeField] private Vector2 baseTiling = Vector2.one;

        [Header("Painted Surface Layers")]
        [SerializeField] private List<SurfaceLayerDefinition> paintLayers =
            new List<SurfaceLayerDefinition>();

        [Header("Mask Storage")]
        [SerializeField] private SurfaceMaskStorageMode maskStorageMode =
            SurfaceMaskStorageMode.PackedRgbaPages;
        [SerializeField, Range(128, 8192)] private int maskResolution = 1024;
        [SerializeField] private List<Texture2D> maskPages = new List<Texture2D>();

        public Texture2D BaseAlbedo => baseAlbedo;
        public Color BaseTint => baseTint;
        public Vector2 BaseTiling => baseTiling;
        public IReadOnlyList<SurfaceLayerDefinition> PaintLayers => paintLayers;
        public SurfaceMaskStorageMode MaskStorageMode => maskStorageMode;
        public int MaskResolution => maskResolution;
        public IReadOnlyList<Texture2D> MaskPages => maskPages;

        public void AddLayer(string layerName)
        {
            if (paintLayers == null)
                paintLayers = new List<SurfaceLayerDefinition>();

            paintLayers.Add(new SurfaceLayerDefinition(layerName));
        }

        public bool RemoveLayerAt(int index)
        {
            if (paintLayers == null || index < 0 || index >= paintLayers.Count)
                return false;

            paintLayers.RemoveAt(index);
            return true;
        }

        public Texture2D GetMaskPage(int index)
        {
            if (maskPages == null || index < 0 || index >= maskPages.Count)
                return null;

            return maskPages[index];
        }

        private void OnValidate()
        {
            if (paintLayers == null)
                return;

            for (int i = 0; i < paintLayers.Count; i++)
            {
                if (paintLayers[i] != null)
                    paintLayers[i].EnsureId();
            }

            maskResolution = Mathf.Clamp(maskResolution, 128, 8192);
        }
    }
}
