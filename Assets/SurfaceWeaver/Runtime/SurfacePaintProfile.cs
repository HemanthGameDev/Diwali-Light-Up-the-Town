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
        [SerializeField] private SurfaceTextureMappingMode textureMappingMode = SurfaceTextureMappingMode.MeshUV0;

        [Header("Painted Surface Layers")]
        [SerializeField] private List<SurfaceLayerDefinition> paintLayers =
            new List<SurfaceLayerDefinition>();

        [Header("Mask Storage")]
        [SerializeField] private SurfaceMaskStorageMode maskStorageMode =
            SurfaceMaskStorageMode.PackedRgbaPages;
        [SerializeField, Range(128, 4096)] private int maskResolution = 1024;
        [SerializeField] private List<Texture2D> maskPages = new List<Texture2D>();

        public Texture2D BaseAlbedo => baseAlbedo;
        public Color BaseTint => baseTint;
        public Vector2 BaseTiling => baseTiling;
        public SurfaceTextureMappingMode TextureMappingMode => textureMappingMode;
        public IReadOnlyList<SurfaceLayerDefinition> PaintLayers => paintLayers;
        public SurfaceMaskStorageMode MaskStorageMode => maskStorageMode;
        public int MaskResolution => maskResolution;
        public IReadOnlyList<Texture2D> MaskPages => maskPages;

        public void AddLayer(string layerName)
        {
            if (paintLayers == null)
                paintLayers = new List<SurfaceLayerDefinition>();

            HashSet<int> usedAddresses = new HashSet<int>();
            for (int i = 0; i < paintLayers.Count; i++)
            {
                SurfaceLayerDefinition existing = paintLayers[i];
                if (existing == null)
                    continue;
                int address = existing.MaskPageIndex * 4 + (int)existing.MaskChannel;
                usedAddresses.Add(address);
            }

            int nextAddress = 0;
            while (usedAddresses.Contains(nextAddress))
                nextAddress++;

            int pageIndex = nextAddress / 4;
            SurfaceMaskChannel channel = (SurfaceMaskChannel)(nextAddress % 4);
            SurfaceLayerDefinition layer = new SurfaceLayerDefinition(layerName);
            layer.SetMaskAddress(pageIndex, channel);
            paintLayers.Add(layer);
            EnsureMaskPageSlots(pageIndex + 1);
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

        /// <summary>Ensures the serialized page list has at least the requested number of slots.</summary>
        public void EnsureMaskPageSlots(int count)
        {
            if (maskPages == null)
                maskPages = new List<Texture2D>();

            while (maskPages.Count < Mathf.Max(0, count))
                maskPages.Add(null);
        }

        /// <summary>Assigns a persistent texture asset to a packed mask page.</summary>
        public void SetMaskPage(int index, Texture2D texture)
        {
            if (index < 0)
                return;

            EnsureMaskPageSlots(index + 1);
            maskPages[index] = texture;
        }

        private void OnValidate()
        {
            if (paintLayers != null)
            {
                for (int i = 0; i < paintLayers.Count; i++)
                {
                    if (paintLayers[i] != null)
                        paintLayers[i].EnsureId();
                }
            }

            // 4096 is the supported upper bound for the current two-buffer RGBA32 editor painter.
            // An 8192 page would require roughly 512 MiB for the two GPU buffers alone.
            maskResolution = Mathf.Clamp(maskResolution, 128, 4096);
        }
    }
}
