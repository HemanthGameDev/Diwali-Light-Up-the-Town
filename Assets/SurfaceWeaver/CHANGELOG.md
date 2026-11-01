# SurfaceWeaver Changelog

## v0.7.6
- Fixed safe profile synchronization to preserve the complete per-layer Params vector (X opacity, Y channel, Z page, W enabled/strength).
- Safe sync now preserves material Texture Mapping mode, Base Tint/Tiling and Layer Tint/Tiling.
- Live preview copies the complete Params vector and mapping mode from the source material.
- Safe sync refreshes profile texture references, mask reference and overlay layer count only; destructive full profile reapplication remains a separate explicit action.

## v0.7.5
- Preserved material opacity and enabled/strength values exactly during profile synchronization.
