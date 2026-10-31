# SurfaceWeaver v0.6.0 — Manual Test Plan

Run these tests in a duplicate project or a disposable test scene first. Save a copy of the v0.5.0 mask/profile assets. Check the Unity Console after each test.

## Test 1 — Import and compile

1. Import/replace `Assets/SurfaceWeaver` with the v0.6.0 package.
2. Wait for Unity to finish compiling.
3. Open **Window > SurfaceWeaver > Painter**.
4. Assign a target with MeshFilter, MeshRenderer, readable UV0, and a non-convex MeshCollider using the same mesh.
5. Load a duplicate packed RGBA mask page.

Expected: no new compile errors; the Painter opens; target and profile load; mask page initializes.

## Test 2 — Basic seam-free painting

1. Use a simple plane or ground mesh with UV0 inside 0–1 and no overlaps.
2. Enable Seam-Aware Mesh Projection.
3. Paint one short stroke with Soft Round; erase part of it.
4. Save the mask PNG and reload it.

Expected: brush appears in the correct world position; only the selected channel changes; save/reload retains the mask.

## Test 3 — Cross a real UV seam

Use a cylinder or sphere with a split UV seam. Place the seam in a visible, easy-to-find location. Validate UV0 first. Ensure the two sides of the seam are not overlapped in UV space.

1. Enable Seam-Aware Mesh Projection.
2. Select one mask channel.
3. Paint a short world-space stroke that crosses the physical seam.
4. Inspect the exported mask texture in the Texture Inspector or an image editor.

Expected: the matching surface region is painted in both UV chart locations, without a sudden break at the seam. The stroke remains clipped to valid UV triangles.

Repeat with a custom asymmetric/grunge alpha brush and a non-zero rotation to check stable orientation on both sides.

## Test 4 — Normal filtering

1. Use a cube or folded mesh with front/back and sharply angled surfaces.
2. Paint near one face and close to the adjacent/opposing face.
3. Inspect which charts were updated.

Expected: surfaces whose face normals are too different from the brush normal are rejected by the fixed v0.6 normal cutoff. Some adjacent surfaces may still receive paint if their normal dot product is above 0.15. Nearby parallel surfaces with matching normals can also be painted because a projection-depth limit is not implemented yet; record this as a known limitation.

## Test 5 — Transform correctness

1. Duplicate the test mesh.
2. Rotate it and apply a non-uniform Transform scale (for example X=2, Y=1, Z=0.5). Do not apply the transform to the mesh.
3. Refresh the MeshCollider.
4. Paint a circular soft brush with a known world radius.

Expected: the brush footprint remains approximately circular in world space on locally planar faces and does not stretch with object scale. Check tilted faces and uneven geometry separately. Negative scale is not guaranteed in v0.6.

## Test 6 — RGBA channel isolation

1. Use a test mask where R, G, B and A contain different visible patterns/values.
2. Select one profile layer assigned to G (or B/A).
3. Paint and erase in a separate area.
4. Read back/save the mask.

Expected: only the selected channel changes; the other three channels remain unchanged.

## Test 7 — Spline preview and application

1. Select a target and load its mask page.
2. Enable Path Authoring.
3. Click at least three anchors along a curved route on the mesh.
4. Check the Catmull-Rom preview.
5. Use Backspace to remove the last anchor, then add it again.
6. Apply Spline.
7. Undo once and Redo once.

Expected: preview follows the anchors; path samples project onto the target; Apply Spline creates one Undo step; Undo removes the entire path stroke and Redo restores it. The route uses a constant brush radius.

## Test 8 — Save and reload a path asset

1. Save the scene at least once.
2. Create a path with 3 or more anchors.
3. Choose a brush preset with random rotation and/or scale jitter.
4. Save Path Asset inside Assets.
5. Clear the current anchors.
6. Load Asset Points.
7. Confirm anchors, layer and brush settings are restored.
8. Save a duplicate mask page, apply the spline and capture the mask result. Undo/reset to the same starting mask and apply again.

Expected: the path target resolves while the source scene is loaded; path points are restored in target-local space; same path settings/seed produce the same stamp randomization. If the GlobalObjectId cannot resolve, the Painter should ask whether to load onto the currently selected compatible target.

## Test 9 — Legacy fallback

1. Disable Seam-Aware Mesh Projection.
2. Paint on a mesh with a continuous UV chart.
3. Paint a stroke that crosses a UV discontinuity.

Expected: legacy UV stamping still works on continuous charts; a seam discontinuity may break the stroke. The UI should explain that legacy mode is not seam-aware.

## Test 10 — Unsupported UV/layout conditions

1. Try a mesh with UV0 coordinates outside 0–1.
2. Try a mesh with degenerate UV triangles.
3. Try a mesh with overlapping/stacked UV charts.
4. Try a mesh with a non-triangle submesh.

Expected: out-of-range UV0 is rejected by the raster backend with an actionable message; degenerate/invalid triangles are skipped and reported; overlapping UV charts are reported by diagnostics and remain ambiguous; non-triangle submeshes are skipped and reported.

## Test 11 — Long path and mesh performance

1. Use the production ground mesh and a long path with several anchors.
2. Observe Editor responsiveness and GPU utilization.
3. Repeat at 1024 and 2048 mask resolutions.

Expected: operation completes without corrupting the mask. Record rough duration and editor responsiveness; each stamp draws the full UV raster mesh, so large meshes and dense paths may be slower.

## Capture for review

Please capture: (a) Console after import, (b) UV seam diagnostic report, (c) both UV islands after one cross-seam stroke, (d) RGBA page before/after channel test, (e) spline preview and applied result, and (f) path asset after reload.


## v0.6.1 hotfix note
Use `Hotfix_Test_Plan_v0.6.1.md` for the updated test sequence. Spline Apply is now supported with Seam-Aware mode off using legacy UV stamping. The default spline brush variation is fixed (randomization off), and projection depth can be adjusted to prevent nearby parallel surfaces from receiving duplicate marks.
