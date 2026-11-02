# SurfaceWeaver Changelog

## v0.8.0-preview.1 — Editable Spline Anchors and Tangent Handles
- Added Scene View anchor selection and surface-snapped anchor movement.
- Added independently editable incoming/outgoing Bézier tangent handles.
- Added automatic tangents for legacy paths and reset controls.
- Persisted custom tangent offsets in reusable path assets.
- Preserved closed paths and existing mask application/Undo workflow.
- Improved stamp spacing estimation for curved path segments.
- Added v0.8 preview build notes and a manual regression plan.
- Recorded the first OnePlus CPH2467 Android performance baseline; overlay-count and thermal comparisons remain pending.

## v0.7.11 — Mip-map Bleeding Prevention
- Disabled mipmap generation for packed RGBA mask-page PNGs on create and save.
- Added a Painter migration action to apply bleed-safe importer settings to existing mask pages.
- Layered URP shader samples `_MaskPage0` at LOD 0 as a second safeguard.
- Albedo textures retain standard mipmap sampling.
- Documented the distant-minification aliasing trade-off.

# SurfaceWeaver Changelog

## v0.7.6
- Fixed safe profile synchronization to preserve the complete per-layer Params vector (X opacity, Y channel, Z page, W enabled/strength).
- Safe sync now preserves material Texture Mapping mode, Base Tint/Tiling and Layer Tint/Tiling.
- Live preview copies the complete Params vector and mapping mode from the source material.
- Safe sync refreshes profile texture references, mask reference and overlay layer count only; destructive full profile reapplication remains a separate explicit action.

## v0.7.5
- Preserved material opacity and enabled/strength values exactly during profile synchronization.
