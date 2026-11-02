# SurfaceWeaver v0.8.0-preview.3.1 — Manual Test Plan

Requires v0.8.0-preview.3 (single active SurfacePaintTarget workflow). Back up the project and mask/path assets first.

## 1. Compile and preserve existing behavior
1. Import the incremental patch and wait for Unity compilation.
2. Confirm there are no C# Console errors.
3. Load an existing path asset with three or more anchors.
4. Confirm anchor editing, Bézier handles, closed paths and width rails still work.

## 2. Start live preview
1. Load the mask page for the intended paint layer.
2. Create a path with at least two anchors.
3. Click **Apply Spline Preview**.
4. Confirm the mask result appears on the target material and in the GPU buffer preview.
5. Confirm Path Status reports that live preview is active and that Commit/Cancel controls are visible.

Pass: the preview is visible immediately; the active GPU stroke remains pending rather than adding a committed Undo entry.

## 3. Adjust width and pressure without clearing
1. While live preview is active, change P1 Width Scale.
2. Confirm the mask preview updates automatically.
3. Change P2 Pressure and confirm the result updates again.
4. Toggle Pressure Affects Width and Pressure Affects Opacity separately.
5. Change several values repeatedly and confirm opacity does not accumulate from previous preview rebuilds.
6. Move an anchor and edit a tangent; confirm the live mask preview follows the updated path.
7. Change Close Path and relevant brush settings; confirm the preview is rebuilt.

Pass: every rebuild starts from the same pre-preview mask snapshot; only the latest path/profile configuration is visible.

## 4. Commit and Undo
1. Click **Commit Preview**.
2. Confirm the final result remains visible.
3. Confirm Undo count increases by exactly one for the spline operation.
4. Click Undo and confirm the mask returns to its pre-preview state.
5. Redo and confirm the final committed spline returns.

Pass: multiple preview edits create one Undo operation, not one entry per slider movement.

## 5. Cancel behavior
1. Start another live spline preview.
2. Change width and pressure several times.
3. Click **Cancel Preview**.
4. Confirm the exact mask from before Apply Spline Preview is restored.
5. Repeat using **Cancel Preview (Undo)**.

Pass: cancellation restores the original mask and does not create an Undo item.

## 6. Save and session transitions
1. Start a live preview and adjust its profile.
2. Click **Save Mask PNG**. Confirm the current preview is committed before the PNG is saved.
3. Start another preview and turn Path Authoring off. Confirm the preview commits.
4. Start another preview and switch target/profile or load another page. Confirm there is no pending-stroke error and the status is clear.
5. Close the Painter while a preview is active. Confirm it commits before the GPU session is disposed.

## 7. Projection and regression checks
1. Run the live preview with seam-aware projection enabled.
2. Repeat with legacy UV projection enabled.
3. Test open and closed paths, low and high pressure, and minimum/maximum width.
4. Confirm no paint accumulates after repeated rebuilds.
5. Confirm standard hand-painted strokes and channel Fill/Clear still work after committing or cancelling a live preview.

Pass criteria: profile edits update the actual material/mask preview immediately, the preview is non-accumulative, Commit creates one Undo step, Cancel restores the baseline, and existing Painter workflows remain intact.
