# SurfaceWeaver v0.7.5 — Preserve Layer Params X and W

## Confirmed v0.7.4 defect

The preserve path read the existing layer parameter vector but then:
- Clamped Params X with `Mathf.Clamp01`, so material values such as 1.2 and 2.5 were rewritten to 1.
- Rebuilt Params W as 1 for every active layer, so a custom value such as 0.6 was rewritten to 1.
- The live preview copy helper transferred Params X but not Params W.

## v0.7.5 correction

For active layers, the safe **Sync Profile Bindings** path now preserves Params X and Params W exactly as stored on the material. Profile-owned Params Y (channel) and Params Z (page) are still refreshed from the profile. Disabled/missing profile layers still have zero opacity and zero strength, and their texture binding is cleared as before.

The preview material tuning copy now transfers Params X and W, so live painting uses the same opacity/strength settings as the assigned material.

**Update Mask Only** remains unchanged and sets only `_MaskPage0`.

## Rendering note

The current URP shader computes the blend weight with `saturate(mask * ParamsX) * saturate(ParamsW)`. Therefore values above 1 are preserved in the material Inspector, but they do not produce a blend weight above 1 at render time. Params W is also checked as a positive enable gate (`> 0.5`) before applying a layer.

## Validation status

Source-level inspection and static text assertions only. This patch has not been compiled or run inside Unity. Unity 6 URP verification is required before calling this release-ready.
