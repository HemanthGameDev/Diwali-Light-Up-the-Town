using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Runtime
{
    /// <summary>
    /// Applies a SurfacePaintProfile to a dedicated shared Material asset.
    /// Profile binding updates texture references, mask layout and overlay count.
    /// Existing material tuning (mapping mode, tint, tiling and all layer Params
    /// components) is preserved unless the caller explicitly reapplies profile values.
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
        /// Updates profile texture/mask bindings and overlay count while preserving
        /// material mapping mode, tint, tiling and the complete Params vector
        /// (X opacity, Y channel, Z page, W enabled/strength) for every layer.
        /// This is the safe default for an existing material.
        /// </summary>
        public bool ApplyProfile(out string report)
        {
            return ApplyProfileInternal(true, out report);
        }

        /// <summary>
        /// Explicitly reapplies every value from the profile, including mapping mode,
        /// tint, tiling and all layer Params components. Use only when the user requests
        /// a full material reset from the profile.
        /// </summary>
        public bool ApplyProfileSettings(out string report)
        {
            return ApplyProfileInternal(false, out report);
        }

        /// <summary>
        /// Refreshes only the saved packed RGBA mask page on the bound material.
        /// No textures, mapping mode, layer layout, tint, tiling, opacity, or other
        /// material properties are changed. Save the current mask from the Painter
        /// before using this method if there are unsaved strokes.
        /// </summary>
        public bool ApplyMaskPageOnly(out string report)
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
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                report = "Material index " + materialIndex + " is missing or outside the renderer's material slots.";
                return false;
            }

            Material material = materials[materialIndex];
            if (material.shader == null || material.shader.name != ShaderName || !material.HasProperty(MaskPage0Id))
            {
                report = "The selected material must use shader: " + ShaderName;
                return false;
            }

            SurfacePaintProfile profile = target.Profile;
            if (profile == null)
            {
                report = "No SurfacePaintProfile is assigned.";
                return false;
            }

            Texture2D savedMaskPage = profile.GetMaskPage(0);
            if (savedMaskPage == null)
            {
                report = "The profile has no saved mask page 0. Save the mask from the Painter first. The material was not changed.";
                return false;
            }

            material.SetTexture(MaskPage0Id, savedMaskPage);
            report = "Updated only Mask Page 0 on material '" + material.name + "'. All other material properties were left unchanged.";
            return true;
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
        /// is true, existing mapping mode, tint, tiling and every layer Params component
        /// are retained. maskOverride lets the editor Painter supply its live GPU mask
        /// without modifying the profile asset.
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

            // The safe sync path updates profile-owned texture/mask bindings and the
            // structural layer count, but it must not rewrite artist-edited material
            // controls. This includes mapping mode and the complete RGBA-like Params
            // vector (X=opacity, Y=mask channel, Z=page, W=enabled/strength).
            if (profile.BaseAlbedo != null)
                material.SetTexture(BaseMapId, profile.BaseAlbedo);
            else if (!preserveMaterialTuning)
                material.SetTexture(BaseMapId, Texture2D.whiteTexture);

            if (!preserveMaterialTuning)
            {
                material.SetColor(BaseTintId, profile.BaseTint);
                material.SetVector(BaseTilingId,
                    new Vector4(profile.BaseTiling.x, profile.BaseTiling.y, 0f, 0f));
                material.SetFloat(MappingModeId, (float)profile.TextureMappingMode);
            }

            Texture page0 = maskOverride != null ? maskOverride : profile.GetMaskPage(0);
            material.SetTexture(MaskPage0Id, page0 != null ? page0 : Texture2D.blackTexture);

            int layerCount = Mathf.Min(profile.PaintLayers.Count, MaxOverlayLayers);
            material.SetFloat(LayerCountId, layerCount);

            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                SurfaceLayerDefinition layer = i < layerCount ? profile.PaintLayers[i] : null;
                bool hasProfileTexture = layer != null && layer.AlbedoTexture != null;
                bool active = hasProfileTexture && layer.Enabled;

                if (preserveMaterialTuning)
                {
                    // Bind a supplied profile texture, but do not clear a material slot
                    // merely because the profile layer is disabled or has no texture.
                    if (hasProfileTexture)
                        material.SetTexture(LayerMapIds[i], layer.AlbedoTexture);

                    // Preserve every artist-controlled material value exactly. In
                    // particular, do not re-import channel/page from the profile here.
                    // Users may intentionally edit Params Y (channel) and Z (page)
                    // directly on the material, just like opacity and enabled state.
                    continue;
                }

                material.SetTexture(LayerMapIds[i],
                    active ? layer.AlbedoTexture : Texture2D.whiteTexture);
                material.SetColor(LayerTintIds[i], active ? layer.Tint : Color.white);
                material.SetVector(LayerTilingIds[i], active
                    ? new Vector4(layer.Tiling.x, layer.Tiling.y, 0f, 0f)
                    : new Vector4(1f, 1f, 0f, 0f));

                float opacity = active ? Mathf.Clamp01(layer.Opacity) : 0f;
                float channel = active ? (float)layer.MaskChannel : 0f;
                float page = active ? layer.MaskPageIndex : 0f;
                float enabledStrength = active ? 1f : 0f;
                material.SetVector(LayerParamsIds[i],
                    new Vector4(opacity, channel, page, enabledStrength));
            }

            report = "Applied profile '" + profile.name + "' to material '" + material.name +
                     "'. Bound " + layerCount + " overlay layer(s) using one packed RGBA mask page. " +
                     (preserveMaterialTuning
                         ? "Preserved material tint, tiling, mapping mode and every layer Params component (X opacity, Y channel, Z page, W enabled/strength). Profile texture bindings, mask reference and overlay count were refreshed."
                         : "All material settings, including tint, tiling, mapping mode and layer Params, were copied from the profile.");
            if (page0 == null && layerCount > 0)
                report += " Warning: mask page 0 is not assigned, so overlay masks currently read as black.";
            return true;
        }

        /// <summary>
        /// Copies user-facing material tuning, including mapping mode and the full Params vector,
        /// from one material to another.
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
            destination.SetFloat(MappingModeId, source.GetFloat(MappingModeId));

            for (int i = 0; i < MaxOverlayLayers; i++)
            {
                destination.SetColor(LayerTintIds[i], source.GetColor(LayerTintIds[i]));
                destination.SetVector(LayerTilingIds[i], source.GetVector(LayerTilingIds[i]));
                // Copy all Params components so the live preview matches the source
                // material's custom opacity, channel, page and enabled state.
                destination.SetVector(LayerParamsIds[i], source.GetVector(LayerParamsIds[i]));
            }
        }

        /// <summary>Reapplies profile bindings while preserving material tuning.</summary>
        public bool Refresh(out string report) => ApplyProfile(out report);
    }
}
