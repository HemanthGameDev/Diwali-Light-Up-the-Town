# SurfaceWeaver v0.7.4 — Explicit Mask-Only Update

## Why this update exists

v0.7.3's preserve workflow retains base/layer tint, tiling and opacity, but it still refreshes profile-owned properties (base/layer textures, mapping mode, mask page, layer count, channel/page addresses and enabled states). Those changes can surprise users who have manually edited those fields directly on the material.

## Changes

- Added **Update Mask Only (Preserve All Material Settings)**. It updates only `_MaskPage0` from the saved profile mask page. It does not change base/overlay textures, mapping mode, layer count, channel/page/enabled state, tint, tiling, opacity or any other material property.
- Renamed and clarified **Sync Profile Bindings (Preserve Material Tuning)**. It intentionally refreshes profile-owned textures and layer layout while preserving tint, tiling and opacity.
- Renamed the destructive action to **Reset Material from Profile (Overwrite All Settings)** and made its overwrite scope explicit in the confirmation dialog.
- Live RGBA/Alpha preview from v0.7.3 is retained.

## Important usage distinction

- Use **Update Mask Only** when you want to update painted mask data and preserve the entire existing material setup. Save the mask in the Painter first; this button uses the saved mask page in the profile.
- Use **Sync Profile Bindings** after intentionally changing profile textures, mapping mode, layer count, channel/page mapping or enabled state.
- Use **Reset Material from Profile** only when intentionally replacing material tuning with profile defaults.

## Validation status

Source-level inspection only. This patch has not been compiled or run in Unity. Verify the three actions in Unity 6 URP, including saved/reopened project persistence, before using it as a release build.
