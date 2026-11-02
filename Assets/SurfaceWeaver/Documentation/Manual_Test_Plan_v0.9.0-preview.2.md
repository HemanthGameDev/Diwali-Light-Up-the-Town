# SurfaceWeaver v0.9.0-preview.2 — Manual Integration Test Plan

Run after all preview.1 and preview.2 EditMode tests compile.

## 1. GPU stroke history in the Painter
1. Duplicate a profile and its mask PNG.
2. Paint a short stroke and finish it.
3. Undo once; confirm the entire stroke disappears.
4. Redo once; confirm the entire stroke returns.
5. Begin another stroke and press Escape; confirm the in-progress stroke is cancelled.
6. Confirm no half-stroke is left and Undo/Redo buttons return to the expected state.

Pass: each completed pointer drag is one logical history entry; cancelling restores the pre-stroke mask.

## 2. Flood Fill history
1. Use the duplicate mask page and select one channel.
2. Perform a connected-region Flood Fill.
3. Undo and compare the full mask to the pre-fill state.
4. Redo and confirm the filled region returns.
5. Verify the other three RGBA channels remain unchanged throughout.

Pass: Flood Fill behaves as one Undo step, and Redo restores exactly the committed result.

## 3. Multi-Mesh grouped history
1. Open a saved Multi-Mesh Target Set with at least two ready targets, each using a unique persisted mask page.
2. Arm Multi-Mesh Painter.
3. Start one continuous stroke on target A and continue onto target B.
4. Finish the stroke.
5. Undo once.
6. Inspect both mask previews: both targets must return to their pre-stroke contents.
7. Redo once and inspect both previews again.
8. Repeat and press Escape before releasing the stroke.

Pass: one cross-target drag is one logical Undo/Redo action; Escape restores all touched targets.

## 4. Live spline preview history
1. Open a duplicate path asset and duplicate profile/mask.
2. Apply Spline Preview.
3. Change an anchor width/pressure and move a point.
4. Commit Preview.
5. Undo once and compare the mask to the pre-preview state.
6. Redo and confirm the final preview returns.
7. Repeat and Cancel Preview; confirm the original mask is restored with no committed stroke.

Pass: repeated live-preview rebuilds do not accumulate; commit is one history entry; cancel restores baseline.

## 5. Scatter placement history
1. Use a duplicate scene and a test Scatter Profile with two weighted prefabs.
2. Place one stamp.
3. Undo once and verify all instances created by that stamp are removed.
4. Redo and verify the same prefab instances and transforms return.
5. Erase one placement group and test Undo/Redo.
6. Change supported global scale/rotation settings with Live Update Existing Instances enabled.
7. Undo the update and verify all affected transforms and `SurfaceScatterInstanceData` values return.

Pass: each stamp, erase group and live-update operation is reversible as one logical action.

## 6. Asset persistence and editor restart
1. Create a duplicate SurfacePaintProfile and a new mask page.
2. Paint or fill a known pattern, save the mask PNG, and save assets.
3. Save the path asset and Scatter Profile.
4. Save the scene and target set.
5. Close and reopen the Unity project (a domain reload alone is not a full editor restart).
6. Verify profile-to-mask references, mask PNG contents, target-set IDs, path anchors/handles/width/pressure, scatter prefab references/weights, and placed-instance metadata.

Pass: all references resolve and saved mask pixel contents are unchanged after reopening.

## 7. Reports
- Save the EditMode NUnit XML report.
- Save screenshots of Painter Undo/Redo, Multi-Mesh grouped Undo/Redo and Scatter Undo/Redo.
- Record any GPU tests skipped because the test host has no supported graphics device.
- Do not mark the milestone complete if any manual integration test is unverified.
