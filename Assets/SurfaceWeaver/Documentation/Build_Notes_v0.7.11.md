# SurfaceWeaver v0.7.11 — Mip-map Bleeding Prevention

## Purpose

Prevent the packed RGBA mask texture from sampling automatically generated lower mip levels that can average mask values across adjacent UV islands. This patch uses a conservative, deterministic mask-sampling policy:

1. Packed mask-page texture assets are imported without mipmaps.
2. The URP layered shader samples `_MaskPage0` explicitly at LOD 0 as a second safeguard if a user manually re-enables importer mipmaps.
3. Base and per-layer albedo textures continue to use their normal sampler and mip chains.

## Importer settings

Newly created and saved mask PNGs now receive the following import settings:

- Texture Type: Default.
- sRGB: Off (linear mask data).
- Alpha Is Transparency: Off (preserve packed Alpha values).
- Read/Write: Off.
- Mip Maps: Off.
- Wrap: Clamp.
- Filter: Bilinear.
- Compression: Uncompressed.

The Painter's **Mip-map Bleeding Prevention** section includes **Apply Bleed-Safe Import Settings to Current Mask Page** for migrating existing pages. It changes importer settings and reimports the asset; it does not intentionally edit PNG pixel data or its GUID.

Saving a mask page through SurfaceWeaver applies the same importer policy automatically.

## Important quality trade-off

Mask pages no longer receive minification mipmaps. This prevents mip-level blending between atlas regions, but very distant or strongly minified surfaces can show more mask-edge aliasing/shimmering than a mipmapped mask. The shader intentionally samples LOD 0, so simply re-enabling mipmaps in the importer will not restore mask minification filtering. Albedo maps are unaffected and retain normal mip sampling.

This is the safe baseline. If target-device tests show unacceptable distant aliasing, a future enhancement can introduce custom island-aware mip-chain generation and safe LOD selection. Do not silently switch the packed-mask texture back to standard automatic mipmaps: that reintroduces the bleed path this patch closes.

## Import and migration

1. Back up `Assets/SurfaceWeaver`.
2. Import this incremental patch into the Unity project root.
3. Wait for Unity compilation and resolve all errors.
4. Open **Window > SurfaceWeaver > Painter** and select the intended target/profile.
5. Load a mask page whose Texture Importer currently has Mip Maps enabled.
6. Expand **Mip-map Bleeding Prevention**.
7. Confirm the displayed page and current Mipmap Status.
8. Click **Apply Bleed-Safe Import Settings to Current Mask Page** and confirm.
9. Inspect the Texture Importer: Mip Maps Off, sRGB Off, Filter Bilinear, Wrap Clamp, Compression Uncompressed.
10. Save the mask page. Confirm the same settings remain after SaveAndReimport.

## Manual visual tests

### Test A — Adjacent islands with different mask channels

- Use a mesh with two non-overlapping UV islands placed close together.
- Paint Red near the edge of the first island and Green near the edge of the second.
- Assign visibly different overlay textures to the Red and Green channels.
- Save the mask and apply bleed-safe settings.
- Inspect the mesh from close, medium and distant camera positions.

Expected: the Red and Green mask regions do not blend into each other because of a generated mask mip chain. Some distant edge aliasing may be visible; record this separately rather than treating it as cross-island bleed.

### Test B — Existing mask page migration

- Start with a saved mask PNG whose Texture Importer has Mip Maps enabled.
- Use the Painter migration button.
- Confirm the asset pixel data/painting remains visually the same at close range and Mip Maps becomes disabled.
- Close/reopen the Painter and reload the page.

Expected: mask painting is preserved; importer setting remains disabled.

### Test C — Save path

- Paint or modify any mask channel.
- Click Save Mask PNG.
- Reopen the PNG's Texture Importer.

Expected: Mip Maps remains disabled after SurfaceWeaver saves/reimports the PNG.

### Test D — Shader LOD safeguard

Optional diagnostic only: temporarily re-enable Mip Maps manually on a duplicate mask texture asset and assign it to the material. The layered shader should still sample mip level 0. Restore the bleed-safe importer settings after this test. This test is not required for normal usage.

### Test E — Albedo mipmaps unaffected

Confirm Base Albedo and overlay albedo textures still use their normal mipmap settings and display as before.

## Performance and memory notes

Disabling mask mipmaps reduces the memory occupied by the mask texture compared with a full mip chain, but does not remove the base-level texture memory or GPU render-target/Undo history memory used while painting. This is not a complete mobile optimization benchmark; mobile shader profiling remains outstanding in v0.7.

## Scope and limitations

- Applies to packed RGBA mask pages only.
- Does not change albedo, normal or other texture importers.
- Does not modify UVs, painted mask pixels, gutter data or dilation data.
- Keeps bilinear filtering and Clamp wrapping.
- Does not provide custom per-mip island-aware filtering. Mask minification is intentionally fixed to LOD 0 in this release.
- Gutter padding remains useful for base-level bilinear filtering, even though mask mipmaps are disabled.

## Verification status

Source-level checks and archive validation only. The patch has not been compiled or run inside the consuming Unity project by the assistant. Verify compilation, existing-page migration, Save PNG reimport, shader behavior, camera-distance appearance and target-device performance in Unity.
