# SurfaceWeaver v0.7.5 Manual Test Plan

## Preparation
1. Back up the current `Assets/SurfaceWeaver` folder and the test scene/material assets.
2. Import this patch into the Unity project root and wait for script compilation.
3. Select the generated SurfaceWeaver material and record the four `_LayerNParams` vectors.

## Preservation regression
Use a material with these representative values on active layers:
- Layer 0 Params: `(1.2, 0, 0, 1)`
- Layer 1 Params: `(2.5, 1, 0, 0.6)`
- Layer 2 Params: `(0.6, 2, 0, 1)`
- Layer 3 Params: `(0.38, 3, 0, 1)`

1. Click **Sync Profile Bindings (Preserve Material Tuning)**.
2. Verify Params X and W remain exactly the same for each active layer.
3. Verify Params Y and Z still match the profile's channel and page assignments.
4. Verify Base Tiling, layer tiling, base/layer tint remain unchanged.
5. Save the project, close/reopen Unity, and verify the values persist.

## Mask-only regression
1. Record all material properties or duplicate the material for comparison.
2. Save a changed RGBA mask from the Painter.
3. Click **Update Mask Only (Preserve All Material Settings)**.
4. Verify only `_MaskPage0` changes. All other shader properties must remain identical.

## Live preview regression
1. Open the Painter with the material above assigned.
2. Paint each RGBA channel, including Alpha.
3. Verify the correct layer updates live on the mesh.
4. Verify the preview retains the source material's Params X and W values while painting.

## Reset regression
1. Use **Reset Material from Profile (Overwrite All Settings)** only on a disposable test material.
2. Confirm the dialog appears.
3. Verify material tuning is replaced by profile defaults as expected.

## Pass criteria
- No Console compile errors.
- Active layer Params X and W are preserved exactly by Sync Profile Bindings.
- Update Mask Only changes only `_MaskPage0`.
- Live RGBA/Alpha preview still works.
- Values survive save/reopen.
