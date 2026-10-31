using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Applies a SurfacePaintProfile directly to a dedicated shared Material asset.
    /// Use one generated material asset per distinct profile to keep URP SRP Batcher compatibility.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SurfaceWeaver/Surface Paint Material Binder")]
    public sealed class SurfacePaintMaterialBinder : MonoBehaviour
    {
        public const string ShaderName = "HemanthGameDev/SurfaceWeaver/URP Layered Surface";
        public const int MaxOverlayLayers = 4;
        public const int MaxMaskPages = 1;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseTintId = Shader.PropertyToID("_BaseTint");
        private static readonly int BaseTilingId = Shader.PropertyToID("_BaseTiling");
        private static readonly int MappingModeId = Shader.PropertyToID("_MappingMode");
        private static readonly int MaskPage0Id = Shader.PropertyToID("_MaskPage0");
        private static readonly int LayerCountId = Shader.PropertyToID("_LayerCount");

        private static readonly int[] LayerMapIds = new int[MaxOverlayLayers];
        private static readonly int[] LayerTintIds = new int[MaxOverlayLayers];
        private static readonly int[] LayerTilingIds = new int[MaxOverlayLayers];
        private static readonly int[] LayerParamsIds = new int[MaxOverlayLayers];

        static SurfacePaintMaterialBinder()
        {
            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                LayerMapIds[i] = Shader.PropertyToID("_Layer" + i + "Map");
                LayerTintIds[i] = Shader.PropertyToID("_Layer" + i + "Tint");
                LayerTilingIds[i] = Shader.PropertyToID("_Layer" + i + "Tiling");
                LayerParamsIds[i] = Shader.PropertyToID("_Layer" + i + "Params");
            }
        }

        [SerializeField] private SurfacePaintTarget target;
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField] private bool applyOnEnable = true;

        public SurfacePaintTarget Target => target;
        public int MaterialIndex => materialIndex;

        private void OnEnable()
        {
            if (applyOnEnable && Application.isPlaying)
                ApplyProfile(out _);
        }

        private void Reset()
        {
            if (target == null)
                target = GetComponent<SurfacePaintTarget>();
        }

        private void OnValidate()
        {
            materialIndex = Mathf.Max(0, materialIndex);
            if (target == null)
                target = GetComponent<SurfacePaintTarget>();
            // In Edit Mode, use the target Inspector's Apply button to serialize changes
            // into the dedicated .mat asset. Runtime application occurs in OnEnable.
        }

        public void Configure(SurfacePaintTarget paintTarget, int slot)
        {
            target = paintTarget;
            materialIndex = Mathf.Max(0, slot);
        }

        /// <summary>Applies the target profile to the renderer's selected material slot.</summary>
        public bool ApplyProfile(out string report)
        {
            report = string.Empty;

            if (target == null)
            {
                report = "No SurfacePaintTarget is assigned to the binder.";
                return false;
            }

            SurfacePaintProfile profile = target.Profile;
            if (profile == null)
            {
                report = "The SurfacePaintTarget has no SurfacePaintProfile.";
                return false;
            }

            if (!target.TryGetRenderer(out MeshRenderer targetRenderer) || targetRenderer == null)
            {
                report = "The target MeshRenderer is missing.";
                return false;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
            {
                report = "Material index " + materialIndex + " is outside the renderer's material slots.";
                return false;
            }

            Material material = materials[materialIndex];
            if (material == null || material.shader == null || material.shader.name != ShaderName)
            {
                report = "The selected material slot must use shader: " + ShaderName;
                return false;
            }

            if (profile.MaskStorageMode != SurfaceMaskStorageMode.PackedRgbaPages)
            {
                report = "v0.7 supports Packed RGBA Pages. SeparateTextures storage is not supported by this shader yet.";
                return false;
            }

            if (profile.PaintLayers.Count > MaxOverlayLayers)
            {
                report = "This v0.7 mobile-conscious shader supports up to " + MaxOverlayLayers + " overlay layers in one RGBA mask page. The profile currently has " + profile.PaintLayers.Count + ". Reduce the layer count for this target.";
                return false;
            }

            for (int i = 0; i < profile.PaintLayers.Count; i++)
            {
                SurfaceLayerDefinition layer = profile.PaintLayers[i];
                if (layer != null && layer.MaskPageIndex >= MaxMaskPages)
                {
                    report = "Layer '" + layer.DisplayName + "' uses mask page " + layer.MaskPageIndex + ". v0.7 supports one packed RGBA mask page (four overlay layers).";
                    return false;
                }
            }

            // A dedicated material asset is generated by the Editor workflow for this target/profile.
            // Write to sharedMaterial rather than Renderer.material or MaterialPropertyBlock:
            // this avoids runtime material instantiation and preserves SRP Batcher eligibility.
            material.SetTexture(BaseMapId, profile.BaseAlbedo != null ? profile.BaseAlbedo : Texture2D.whiteTexture);
            material.SetColor(BaseTintId, profile.BaseTint);
            material.SetVector(BaseTilingId, new Vector4(profile.BaseTiling.x, profile.BaseTiling.y, 0f, 0f));
            material.SetFloat(MappingModeId, (float)profile.TextureMappingMode);

            Texture2D page0 = profile.GetMaskPage(0);
            material.SetTexture(MaskPage0Id, page0 != null ? page0 : Texture2D.blackTexture);

            int layerCount = Mathf.Min(profile.PaintLayers.Count, MaxOverlayLayers);
            material.SetFloat(LayerCountId, layerCount);

            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                SurfaceLayerDefinition layer = i < layerCount ? profile.PaintLayers[i] : null;
                bool active = layer != null && layer.Enabled && layer.AlbedoTexture != null;

                material.SetTexture(LayerMapIds[i], active ? layer.AlbedoTexture : Texture2D.whiteTexture);
                material.SetColor(LayerTintIds[i], active ? layer.Tint : Color.white);
                Vector2 tiling = active ? layer.Tiling : Vector2.one;
                material.SetVector(LayerTilingIds[i], new Vector4(tiling.x, tiling.y, 0f, 0f));

                float opacity = active ? Mathf.Clamp01(layer.Opacity) : 0f;
                float channel = active ? (float)layer.MaskChannel : 0f;
                material.SetVector(LayerParamsIds[i], new Vector4(opacity, channel, 0f, active ? 1f : 0f));
            }

            report = "Applied profile '" + profile.name + "' to dedicated material '" + material.name + "' on " + targetRenderer.name + " slot " + materialIndex + ". Bound " + layerCount + " overlay layer(s) using one packed RGBA mask page.";
            if (page0 == null && layerCount > 0)
                report += " Warning: mask page 0 is not assigned, so overlay masks currently read as black.";
            return true;
        }

        /// <summary>Reapplies the current profile to the dedicated material asset.</summary>
        public bool Refresh(out string report) => ApplyProfile(out report);
    }
}
