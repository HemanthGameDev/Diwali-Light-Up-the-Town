# Manual Test Plan — v0.5.0

Run these tests in the target Unity 6 URP project after importing the package and allowing script/shader compilation.

## A. UV diagnostics

1. Assign a readable custom mesh with a single normalized UV island; validate. Expect no degenerate triangles, no positive-area overlaps, and no seam edges for a welded continuous grid.
2. Create two UV triangles that share only an edge. Expect no overlap pair.
3. Stack two triangles in identical UV coordinates but on different mesh regions. Expect at least one overlapping pair.
4. Split a geometric edge into duplicate vertices with different UV endpoints. Expect a UV seam edge warning.
5. Move one UV beyond 0–1. Expect an out-of-range warning.
6. Set a triangle's UVs collinear. Expect a degenerate UV warning.
7. Validate a very large/highly overlapping mesh. If the candidate-pair safety limit is reached, expect the analysis status to say Partial.

## B. Smooth operation

1. Create/load an RGBA mask page with a visible value boundary in the selected channel.
2. Paint using Paint mode; verify the selected channel moves towards white.
3. Change to Smooth; paint across the boundary at low opacity. Expect the selected channel to soften locally.
4. Inspect the other three channels. They must remain unchanged.
5. Undo and redo the smooth stroke. Expect the mask to restore and reapply exactly at the GPU snapshot level.
6. Verify brush alpha controls the smooth footprint.

## C. Fill / Clear

1. Populate at least two RGBA channels with different patterns.
2. Click Fill Selected Layer and confirm. Expect only the selected channel to become white.
3. Undo. Expect the original selected-channel data to return.
4. Click Clear Selected Layer Channel and confirm. Expect only the selected channel to become black.
5. Undo and save/reload. Confirm channel contents persist in the PNG.

## D. Regression

1. Paint and erase with Soft Round, Hard Round, Grunge and Scatter presets.
2. Confirm Scene View Alt navigation still works.
3. Confirm Escape cancels an active stroke.
4. Confirm Ctrl/Cmd+Z and Ctrl/Cmd+Y or Shift+Z work when focus is in Scene View.
5. Save, close and reopen the Painter window; reload the same mask page and confirm data persists.
6. Check Console and Editor.log for errors and warnings.

## Not covered by this milestone

- Automatic seam-crossing paint propagation.
- Island padding/dilation and mip bleed tests.
- Spline/path painting.
- Scatter placement of mesh prefabs.
- Multiple target batch painting.
- Full profile-driven multi-layer URP rendering.
