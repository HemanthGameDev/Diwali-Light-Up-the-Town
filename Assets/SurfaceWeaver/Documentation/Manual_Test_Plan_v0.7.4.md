# SurfaceWeaver v0.7.4 — Material Update and Regression Test Plan

Use a duplicate scene, target, profile and material. Back up the project before importing.

## A. Live RGBA painting
1. Assign the v0.7 layered URP material to the test target.
2. Open SurfaceWeaver Painter and load the saved RGBA mask.
3. Paint a short stroke on Red, Green, Blue and Alpha in separate areas.
4. Keep the Painter open throughout.

Pass: Each layer's texture appears on the mesh as its channel is painted. Alpha must be visible during painting, without closing the Painter or applying profile bindings.

## B. Mask-only update must preserve every other material property
1. Save the mask from the Painter.
2. Record the material's Base Map, Base Tint, Base Tiling, Mapping Mode, Layer Count, all four Layer Maps, Tints, Tilings and Layer Params.
3. Close the Painter or leave it open after saving the mask.
4. Click **Update Mask Only (Preserve All Material Settings)**.
5. Compare the material values with the recorded values.

Pass: Only `_MaskPage0` changes. Every other listed property remains identical. The material still renders correctly.

## C. Profile binding sync
1. Change one profile-owned value (for example, one overlay albedo or channel assignment).
2. Click **Sync Profile Bindings (Preserve Material Tuning)**.
3. Verify the base/layer textures, mapping mode, mask, layer count, channel/page addresses and enabled states reflect the profile.
4. Verify base/layer tint, tiling and opacity remain unchanged.

Pass: Profile-owned bindings update; material tuning remains.

## D. Explicit full reset
1. Record current tuning values.
2. Click **Reset Material from Profile (Overwrite All Settings)** and confirm.

Pass: Profile textures/layout and all profile tuning defaults are applied. Cancel must leave material unchanged.

## E. Persistence and regression
1. Save the project.
2. Close and reopen Unity.
3. Confirm material references, all tuning values, RGBA mask data and live painting still work.
4. Re-test v0.6.6 seam-aware painting on a plane and a UV-seamed cylinder.
5. Re-test a short spline stroke and Undo/Redo.

Pass: No missing references, unexpected material resets, C# Console errors or shader errors.

## Release gate
Do not mark this package release-ready until all tests above pass in the target Unity 6 LTS + URP version. The package still requires a separate device/performance pass and a third-party/license audit before a public Asset Store release.
