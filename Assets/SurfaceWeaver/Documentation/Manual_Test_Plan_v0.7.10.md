# Manual Test Plan — SurfaceWeaver v0.7.10 Mask Dilation

Run these tests in a duplicate scene and duplicate `SurfacePaintProfile`. Keep a copy of the mask PNG before testing. Mask dilation changes painted surface values, so do not experiment on the only copy of a production mask page.

## Test 0 — Import and compile

1. Back up `Assets/SurfaceWeaver`.
2. Import the v0.7.10 patch into the Unity project root, merging files.
3. Wait for script compilation.
4. Confirm there are no new Console compile errors.
5. Open **Window > SurfaceWeaver > Painter**.

Expected: the Painter opens and existing painting, live RGBA preview, UV diagnostics, gutter padding, profile binding and spline controls remain available.

## Test 1 — Basic selected-channel dilation

### Mesh and mask setup

1. Use a readable mesh with one or more separate, non-overlapping UV islands fully inside 0–1.
2. Use a 1024 x 1024 packed RGBA mask page.
3. Select a layer mapped to Red (R).
4. Paint a small, clearly visible Red spot well inside a sufficiently large UV island. Leave empty mesh-covered texels around the spot.
5. Save the mask PNG, then reload the page so the test begins from persisted data.
6. Load the page in the Painter and ensure the Red layer is selected.

### Apply dilation

1. Expand **UV-Constrained Mask Dilation**.
2. Confirm the Target Channel reads **Red**.
3. Set **Dilation Radius (Pixels)** to 4.
4. Click **Apply Mask Dilation to Selected Channel** and confirm the dialog.

Expected:

- The operation succeeds and reports the changed texel count, seed texel count and UV island count.
- The Red mask region grows by up to four pixels in the square/Chebyshev neighborhood, constrained to texels covered by that UV island.
- The visible painted region on the mesh grows slightly.
- Pixels outside the UV island remain unchanged.
- Green, Blue and Alpha values remain byte-for-byte unchanged.
- Session Status becomes unsaved/dirty.

The exact changed-pixel count depends on brush shape, island geometry and mask values; do not expect a fixed count.

## Test 2 — Channel isolation

1. Save a copy of the mask PNG before dilation.
2. Paint a separate Green patch and an Alpha patch elsewhere on the same page.
3. Select the Red layer and repeat dilation at 2 pixels.
4. Compare the packed RGBA data before and after, preferably by exporting both PNGs and checking channels separately.

Expected: only Red changes. Green, Blue and Alpha remain unchanged. The dilation must not modify the other channels even where their own mask values are non-zero.

## Test 3 — Undo and Redo

1. Apply a dilation operation.
2. Click **Undo** once.
3. Confirm the expanded region returns to its exact pre-dilation shape.
4. Click **Redo** once.
5. Confirm the expanded region returns.

Expected: the entire dilation is one history step. No partial intermediate state should remain.

## Test 4 — Save and reload

1. After Redo, click **Save Mask PNG**.
2. Confirm Session Status becomes saved/unchanged.
3. Close and reopen the Painter.
4. Reload the mask page.

Expected: the dilated values persist after reload.

## Test 5 — Empty selected channel

1. Select a layer/channel with no non-zero mask values inside the UV islands.
2. Load its mask page and click Apply Mask Dilation.

Expected: the operation is refused with a clear message that the selected channel contains no painted values. No mask values change and no Undo state is created.

## Test 6 — Separate-island overlap protection

Use a mesh with two distinct UV islands that partially or fully overlap in UV space.

Expected: dilation is refused with a clear overlap warning. The active mask remains unchanged and no Undo state is created.

## Test 7 — Mirrored overlaps within one connected island

Use the mirrored-half plane from the v0.7.8 regression test. The validator should report mirrored triangle overlaps within one connected island, but no separate overlapping-island pair.

Expected: dilation is allowed if the remaining UV checks pass. Shared mirrored faces use the same UV mask region by design.

## Test 8 — UV boundaries and channel controls

1. Paint a spot close to, but not crossing, the boundary of an island.
2. Apply a 4-pixel dilation.
3. Inspect the mask page at high zoom.

Expected: dilation stops at the rasterized island boundary. It must not expand into empty atlas texels; that is the separate purpose of UV Gutter Padding. Confirm the gutter padding values outside the UV island remain unchanged.

## Test 9 — Resolution smoke test

Repeat Test 1 at 512, then 1024, and only then 2048. Record operation duration and Editor memory behavior. Do not begin 4096 profiling until 2048 is stable.

Expected: correct results at each tested resolution, with no Editor freeze, incomplete operation or memory failure. Record actual timings rather than inferring performance from source code.

## Limitations

- Dilation changes the selected channel on the mesh surface; it is not an automatic seam repair or a substitute for painting.
- It uses grayscale maximum filtering, so the strongest nearby value spreads outward and existing values are never reduced.
- The operation is limited to UV0 and UV coordinates inside 0–1, matching the current painter and shader workflow.
- Separate overlapping UV islands are blocked because they cannot be safely distinguished in a shared mask page.
- Mirrored overlap inside one connected island is allowed because it represents shared UV ownership.
- Mip-map bleeding prevention and mip-chain-aware gutter generation are not included in this patch.

## Verification status

The source structure and offline processor tests should be completed before importing. Unity Editor compilation, active GPU readback/upload, Undo/Redo, saved PNG behavior and mesh-visible results must be verified in the consuming Unity project. This patch has not been run inside the Unity Editor by the assistant.
