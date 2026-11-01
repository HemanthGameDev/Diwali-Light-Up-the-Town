using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Applies a SurfacePaintProfile to a dedicated shared Material asset.
    /// Profile binding updates textures and mask layout. Existing material tuning
    /// (tint, tiling and opacity) is preserved unless the caller explicitly asks
    /// to reapply the profile's tuning defaults.
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
            // into the dedicated .mat asset.
        }

        public void Configure(SurfacePaintTarget paintTarget, int slot)
        {
            target = paintTarget;
            materialIndex = Mathf.Max(0, slot);
        }

        /// <summary>
        /// Updates profile bindings but preserves the material's existing tint,
        /// tiling and opacity values. This is the safe default for an existing material.
        /// </summary>
        public bool ApplyProfile(out string report)
        {
            return ApplyProfileInternal(true, out report);
        }

        /// <summary>
        /// Explicitly reapplies every value from the profile, including tint,
        /// tiling and opacity. Use only when the user requests a tuning reset.
        /// </summary>
        public bool ApplyProfileSettings(out string report)
        {
            return ApplyProfileInternal(false, out report);
        }

        private bool ApplyProfileInternal(bool preserveMaterialTuning, out string report)
        {
            report = string.Empty;

            if (target == null)
            {
                report = "No SurfacePaintTarget is assigned to the binder.";
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
            return ApplyProfileToMaterial(
                target != null ? target.Profile : null,
                material,
                null,
                preserveMaterialTuning,
                out report);
        }

        /// <summary>
        /// Applies profile texture/mask bindings to a material. When preserveMaterialTuning
        /// is true, existing material tint, tiling and opacity are retained. maskOverride
        /// lets the editor Painter supply its live GPU mask without modifying the profile asset.
        /// </summary>
        public static bool ApplyProfileToMaterial(
            SurfacePaintProfile profile,
            Material material,
            Texture maskOverride,
            bool preserveMaterialTuning,
            out string report)
        {
            report = string.Empty;

            if (profile == null)
            {
                report = "No SurfacePaintProfile is assigned.";
                return false;
            }

            if (material == null || material.shader == null || material.shader.name != ShaderName)
            {
                report = "The material must use shader: " + ShaderName;
                return false;
            }

            if (profile.MaskStorageMode != SurfaceMaskStorageMode.PackedRgbaPages)
            {
                report = "v0.7 supports Packed RGBA Pages. SeparateTextures storage is not supported by this shader yet.";
                return false;
            }

            if (profile.PaintLayers.Count > MaxOverlayLayers)
            {
                report = "This v0.7 mobile-conscious shader supports up to " + MaxOverlayLayers +
                         " overlay layers in one RGBA mask page. The profile currently has " +
                         profile.PaintLayers.Count + ". Reduce the layer count for this target.";
                return false;
            }

            for (int i = 0; i < profile.PaintLayers.Count; i++)
            {
                SurfaceLayerDefinition layer = profile.PaintLayers[i];
                if (layer != null && layer.MaskPageIndex >= MaxMaskPages)
                {
                    report = "Layer '" + layer.DisplayName + "' uses mask page " + layer.MaskPageIndex +
                             ". v0.7 supports one packed RGBA mask page (four overlay layers).";
                    return false;
                }
            }

            // Base texture and mapping remain profile-driven. Tint and tiling are
            // material tuning values and are preserved by the safe/default apply path.
            material.SetTexture(BaseMapId,
                profile.BaseAlbedo != null ? profile.BaseAlbedo : Texture2D.whiteTexture);

            Color baseTint = preserveMaterialTuning
                ? material.GetColor(BaseTintId)
                : profile.BaseTint;
            Vector4 baseTiling = preserveMaterialTuning
                ? material.GetVector(BaseTilingId)
                : new Vector4(profile.BaseTiling.x, profile.BaseTiling.y, 0f, 0f);

            material.SetColor(BaseTintId, baseTint);
            material.SetVector(BaseTilingId, baseTiling);
            material.SetFloat(MappingModeId, (float)profile.TextureMappingMode);

            Texture page0 = maskOverride != null ? maskOverride : profile.GetMaskPage(0);
            material.SetTexture(MaskPage0Id, page0 != null ? page0 : Texture2D.blackTexture);

            int layerCount = Mathf.Min(profile.PaintLayers.Count, MaxOverlayLayers);
            material.SetFloat(LayerCountId, layerCount);

            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                SurfaceLayerDefinition layer = i < layerCount ? profile.PaintLayers[i] : null;
                bool active = layer != null && layer.Enabled && layer.AlbedoTexture != null;

                material.SetTexture(LayerMapIds[i],
                    active ? layer.AlbedoTexture : Texture2D.whiteTexture);

                Color layerTint = preserveMaterialTuning
                    ? material.GetColor(LayerTintIds[i])
                    : (active ? layer.Tint : Color.white);
                Vector4 layerTiling = preserveMaterialTuning
                    ? material.GetVector(LayerTilingIds[i])
                    : (active
                        ? new Vector4(layer.Tiling.x, layer.Tiling.y, 0f, 0f)
                        : new Vector4(1f, 1f, 0f, 0f));
                Vector4 previousParams = material.GetVector(LayerParamsIds[i]);
                float opacity = preserveMaterialTuning
                    ? Mathf.Clamp01(previousParams.x)
                    : (active ? Mathf.Clamp01(layer.Opacity) : 0f);

                material.SetColor(LayerTintIds[i], layerTint);
                material.SetVector(LayerTilingIds[i], layerTiling);

                float channel = active ? (float)layer.MaskChannel : 0f;
                float page = active ? layer.MaskPageIndex : 0f;
                float enabled = active ? 1f : 0f;
                material.SetVector(LayerParamsIds[i],
                    new Vector4(opacity, channel, page, enabled));
            }

            report = "Applied profile '" + profile.name + "' to material '" + material.name +
                     "'. Bound " + layerCount + " overlay layer(s) using one packed RGBA mask page. " +
                     (preserveMaterialTuning
                         ? "Existing material tint, tiling and opacity were preserved."
                         : "Tint, tiling and opacity were copied from the profile.");
            if (page0 == null && layerCount > 0)
                report += " Warning: mask page 0 is not assigned, so overlay masks currently read as black.";
            return true;
        }

        /// <summary>
        /// Copies only user-facing material tuning from one material to another.
        /// Used by the Painter's temporary preview instance so inspector edits to the
        /// saved material are reflected without changing the source .mat asset.
        /// </summary>
        public static void CopyMaterialTuning(Material source, Material destination)
        {
            if (source == null || destination == null ||
                source.shader == null || destination.shader == null ||
                source.shader.name != ShaderName || destination.shader.name != ShaderName)
                return;

            destination.SetColor(BaseTintId, source.GetColor(BaseTintId));
            destination.SetVector(BaseTilingId, source.GetVector(BaseTilingId));

            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                destination.SetColor(LayerTintIds[i], source.GetColor(LayerTintIds[i]));
                destination.SetVector(LayerTilingIds[i], source.GetVector(LayerTilingIds[i]));

                Vector4 destinationParams = destination.GetVector(LayerParamsIds[i]);
                Vector4 sourceParams = source.GetVector(LayerParamsIds[i]);
                destinationParams.x = sourceParams.x;
                destination.SetVector(LayerParamsIds[i], destinationParams);
            }
        }

        /// <summary>Reapplies profile bindings while preserving material tuning.</summary>
        public bool Refresh(out string report) => ApplyProfile(out report);
    }
}
