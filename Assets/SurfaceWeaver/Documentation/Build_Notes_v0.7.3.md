# SurfaceWeaver v0.7.3 — Live Layer Preview and Material Tuning Preservation

## Goals

- Show the complete RGBA layered material on the target while painting, including the Alpha layer.
- Prevent routine profile updates from overwriting hand-tuned material tint, tiling, and opacity.
- Let users intentionally overwrite material tuning from the profile through a separate, confirmed action.

## Changes

### Live preview

- The temporary Painter material instance now receives the current profile's base/overlay texture bindings, mapping mode, layer count, per-layer channel/page/enabled settings, and the live GPU mask texture.
- This refresh occurs during paint updates and when profile data is edited in the Painter.
- The preview copies tint, tiling, and opacity from the original material before refreshing profile bindings. The source `.mat` asset is not modified by the live preview.
- Applying the profile from the target Inspector suspends the temporary preview first and restores it after the source material has been updated, so the button no longer edits a temporary clone.

### Material tuning preservation

- `ApplyProfile()` now preserves the existing material's base tint/tiling and per-layer tint/tiling/opacity.
- It still updates profile-driven textures, mask page, layer count, channel mapping, page mapping, and enabled state.
- `ApplyProfileSettings()` explicitly copies all tuning values from the profile.
- The target Inspector exposes separate actions:
  - **Update Profile Bindings (Preserve Material Tuning)** — safe/default update.
  - **Apply Profile Tuning (Overwrite Material)** — confirmed overwrite of material tint, tiling and opacity.
- Creating a new SurfaceWeaver material initializes its values from the profile. Reusing an existing SurfaceWeaver material preserves its tuning.

## Install

1. Close Unity or save the project and make a backup of `Assets/SurfaceWeaver`.
2. Extract the patch ZIP into the Unity project root, preserving the `Assets/SurfaceWeaver/...` paths and overwriting the three `.cs` files included.
3. Keep the existing `.meta` files. The patch includes the matching meta files for the updated scripts.
4. Reopen Unity and wait for compilation to finish.

## Manual verification in Unity 6 URP

1. Open the SurfaceWeaver Painter and load the existing RGBA mask page.
2. Select the Alpha layer and paint a small new stroke. Verify the fourth texture appears on the mesh during the stroke, without closing the Painter or applying the profile.
3. Adjust Base Tiling and at least one overlay's Tiling/Opacity on the generated `.mat` asset. Record the values.
4. With the Painter still open, click **Update Profile Bindings (Preserve Material Tuning)** on the target Inspector. Verify the material values remain unchanged and the live mask preview remains visible.
5. Close and reopen the Painter. Verify the Alpha layer and the material tuning remain.
6. Optionally click **Apply Profile Tuning (Overwrite Material)** and confirm that the material values are intentionally copied from the profile.
7. Save the project, restart Unity, and verify the material `.mat` still contains the chosen tuning values.

## Validation status

The patch has received source-level structural review only. It has not been compiled or run inside Unity in this environment. Unity compilation and the manual test sequence above are required before treating v0.7.3 as verified.
