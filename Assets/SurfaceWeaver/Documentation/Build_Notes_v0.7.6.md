# SurfaceWeaver v0.7.6 Build Notes

## Safe profile synchronization
The safe **Sync Profile Bindings** path now preserves material-side settings: Base Tint, Base Tiling, Texture Mapping Mode, every Layer Tint and Layer Tiling, and the complete `_LayerNParams` vector (X=opacity, Y=mask channel, Z=mask page, W=enabled/strength). This fixes channel values reverting to the profile value during sync.

The safe sync still refreshes profile texture references, the packed RGBA mask reference, and overlay layer count. **Update Mask Only** remains the strict no-other-properties update: it changes only `_MaskPage0`. **Reset Material from Profile** remains the explicit destructive operation that overwrites material settings from the profile.

The live preview copy helper now copies the full Params vector and mapping mode so painting preview matches the current material settings.

## Verification status
Source-level checks only. This patch has not been compiled or executed in Unity. Run the included manual test plan before using it in a release build.
