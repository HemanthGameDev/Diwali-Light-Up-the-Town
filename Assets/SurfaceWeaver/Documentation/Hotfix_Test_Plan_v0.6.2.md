# SurfaceWeaver v0.6.2 — Short hotfix test plan

## Before testing
1. Commit the working v0.6.1 state.
2. Duplicate the test profile and create a brand-new black mask page.
3. Keep the original village mask untouched.
4. Confirm the target has a readable mesh, UV0, and a non-convex MeshCollider that uses the same mesh as the MeshFilter.

## Test A — compile and preview
1. Import v0.6.2 into the project, replacing only `Assets/SurfaceWeaver`.
2. Wait for Unity compilation.
3. Open `Window > SurfaceWeaver > Painter`.
4. Load the newly-created black mask page.
5. Keep `Preview selected mask channel only (grayscale)` enabled.
6. Verify a newly-created empty page is black.

Expected: no magenta region on a clean black page. If combined RGBA mode shows magenta but selected-channel grayscale is black, another packed channel contains data; that is not a selected-channel paint error.

## Test B — simple surface sanity
1. Use a plane with a single full 0–1 UV island.
2. Paint a short line in seam-aware mode.
3. Verify it appears once in the selected-channel preview and on the material preview.
4. Save, reload, and check orientation.

Expected: no rotated/mirrored patch, no rectangular block, and one correctly oriented stroke.

## Test C — cylinder seam
1. Use a cylinder with a longitudinal UV seam and two separate, non-overlapping UV charts.
2. Run UV diagnostics. Confirm seam edges are reported and overlap pairs are zero.
3. Create/reload a fresh black mask page. Select one layer/channel.
4. Set brush radius to about 0.15–0.25 of the cylinder radius; projection depth to 0.5 x radius; hardness 0.7; brush alpha override none.
5. Paint a short stroke whose center crosses the longitudinal seam.
6. Inspect the selected-channel grayscale mask and the cylinder surface.

Expected: a physically continuous mark across the seam, represented in both corresponding UV chart locations. The exact raw UV pattern will appear on separate islands.

## Test D — spline sanity
1. On a fresh mask page, enable path authoring and add three anchors on one continuous surface region.
2. Apply a short open spline.
3. Inspect selected-channel grayscale preview.
4. Undo and Redo once.
5. Save and reload.

Expected: one path, no unexplained rectangular/mirrored area, Undo removes the stroke, Redo restores it.

## If a test fails
Capture: Unity version, graphics API (`Project Settings > Player > Other Settings > Graphics APIs`), Console errors, screenshot of selected-channel preview, screenshot of the actual mesh, and a screenshot of the UV layout. Do not save the corrupted page over the original asset.
