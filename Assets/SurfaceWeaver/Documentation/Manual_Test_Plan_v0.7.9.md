# Manual Test Plan — SurfaceWeaver v0.7.9

Run these tests in a duplicate scene and duplicate `SurfacePaintProfile`. Do not experiment on the only copy of a production mask page.

## Test 0 — Import and compilation

1. Back up `Assets/SurfaceWeaver`.
2. Import the v0.7.9 patch into the Unity project root.
3. Wait for script compilation and shader import.
4. Open the Console and confirm there are no new compile errors.
5. Open **Window > SurfaceWeaver > Painter**.

Expected: Painter opens normally; existing paint, live RGBA preview, Alpha preview, profile binding, UV diagnostics, and spline tools remain available.

## Test 1 — Safe padding on separated UV islands

### Mesh setup

Create or use a mesh with two or more separate UV islands inside the 0–1 range. Leave a visible gap between islands. At 1024 resolution, leave at least 32–40 pixels between island boundaries for an 8-pixel gutter test. Do not overlap the islands.

### Mask setup

1. Create or duplicate a packed RGBA mask page at 1024 x 1024.
2. Paint a clear Red (R) region near the edge of the first island.
3. Paint a clear Alpha (A) region near the edge of the second island.
4. Save the page, then reload it to ensure the test begins from persisted mask data.
5. Load the same page in the Painter.

### Apply

1. Click **Validate UV Channel**.
2. Confirm UVs are in 0–1, there are no degenerate triangles, and `Overlapping island pairs` is 0.
3. Expand **UV Gutter Padding**.
4. Set **Gutter Width (Pixels)** to 8.
5. Click **Apply UV Gutter Padding to Active Mask**.

Expected:

- Operation succeeds and reports a positive padded-texel count.
- All four channels are copied together from each island's boundary pixels.
- The mask values covered by the mesh UV triangles remain unchanged.
- On-mesh appearance should not change because only unused atlas texels are modified.
- `Session Status` becomes unsaved/dirty.

Inspect the saved mask page at high zoom after saving. The edge colors should extend outward from the island silhouettes into empty atlas space.

## Test 2 — Undo, Redo, and persistence

1. After Test 1, click **Undo** once.
2. Inspect the mask preview; the gutter extension should revert.
3. Click **Redo** once.
4. Inspect the mask preview; the gutter extension should return.
5. Click **Save Mask PNG**.
6. Close and reopen the Painter, then reload the mask page.

Expected: The saved page retains the gutter extension after reload. Undo/Redo works before saving within the active Painter session.

## Test 3 — Separate-island overlap protection

Use a test mesh with two distinct UV islands that overlap or one island partially/fully covers another.

1. Validate UVs and confirm the report shows at least one overlapping island pair.
2. Load the relevant mask page and try the gutter operation.

Expected: The operation is refused with a clear warning that separate-island overlap must be resolved first. The active mask must remain unchanged, and no new Undo state should be created.

## Test 4 — Padding-front collision protection

1. Use two separate, non-overlapping islands with a very narrow gap.
2. Choose a gutter width large enough that the two expansion fronts would meet.
3. Click Apply.

Expected: The operation is refused and reports that gutter regions collided. No partial padding is committed. Reduce the width or repack the islands with more spacing, then retry.

## Test 5 — Mirrored overlap inside one connected island

Use the mirrored-half plane used for the v0.7.8 regression test. It should report mirrored triangle overlaps within the same connected island, but no overlapping pair between separate islands.

1. Load a mask page and apply a small gutter width (start at 4 pixels).

Expected: The operation is allowed if all other UV safety checks pass. Mirrored faces intentionally share the same UV mask region; they are not treated as independent island gutters.

## Test 6 — Existing workflow regression

1. Paint Red, Green, Blue and Alpha patches.
2. Confirm live preview updates while painting.
3. Confirm material texture mapping, tint, tiling, and all four Params values remain unchanged.
4. Confirm Save Mask PNG updates only the packed mask texture.
5. Use Undo/Redo across both a paint stroke and the gutter operation.

Expected: No regression to v0.7.6 material preservation or v0.7.8 UV diagnostics.

## Test 7 — Resolution smoke test

Repeat Test 1 at 512, then 1024, and only then 2048. Record operation duration and editor memory behavior. Do not begin 4096 profiling until 2048 is stable.

Expected: Correct padding at all tested resolutions with no editor freeze or memory failure. Record actual timings; do not infer performance from the source code alone.
