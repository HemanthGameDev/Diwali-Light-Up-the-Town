# SurfaceWeaver v0.6.1 — Short Hotfix Test Plan

Use a duplicate scene, duplicate SurfacePaintProfile, and a duplicate mask page. Do not test on the only copy of a production mask.

## Test 1 — Compile and setup
- Replace only `Assets/SurfaceWeaver` with this package, preserving project backup/branch.
- Wait for import and compile.
- Open Window > SurfaceWeaver > Painter.
- Assign target, profile, non-convex MeshCollider using the same mesh, and load the duplicate mask page.
- Expected: no new Console errors.

## Test 2 — Spline works with Seam-Aware off
- Use a simple Plane with one UV island.
- Turn Seam-Aware Mesh Projection off.
- Enable Path Authoring, click 3 points, and apply.
- Expected: path paints using legacy UV mode. Status should warn that chart seams may show gaps.

## Test 3 — Stable square on the custom ground
- Turn Seam-Aware on.
- Use Soft Round or Hard Round.
- Turn `Randomize brush rotation / size per path stamp` off.
- Add 4 corner anchors in order. Enable `Close Path`.
- Apply Spline.
- Expected: one closed square/rounded rectangle is painted on the rendered mesh, not two separate physical copies.

## Test 4 — Projection depth control
- If another nearby/parallel surface receives the same mark, lower `Projection Depth (x Radius)` (try 0.25–0.4).
- If painting on a curved slope is clipped, raise it gradually (try 0.75–1.0).
- Expected: only the intended surface region is affected, with a reasonable balance between clipping and bleed.

## Test 5 — UV mask orientation
- Save the mask page.
- Inspect the rendered mesh first, then inspect the raw PNG.
- The raw UV PNG can look rotated relative to the world because UV charts may be rotated in UV space. This is not a bug if the rendered paint lands on the correct physical surface.
- Expected: saving/reloading keeps the same placement.

## Test 6 — Randomized brush (optional)
- Select Grunge or Scatter.
- First leave per-stamp randomization off; then turn it on and apply a second test path.
- Expected: off = consistent brush orientation/size; on = varied stamps by design.

## Test 7 — Persistence and history
- Undo the spline once; it should disappear as one operation. Redo; it should return.
- Save mask page, close/reopen Painter, reload mask.
- Save path asset, clear anchors, reload asset points.
- Expected: mask and path restore.
