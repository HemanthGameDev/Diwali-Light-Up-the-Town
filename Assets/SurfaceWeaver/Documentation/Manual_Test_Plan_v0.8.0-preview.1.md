# SurfaceWeaver v0.8.0-preview.1 — Manual Test Plan

Use a duplicate scene, duplicate `SurfacePaintProfile`, duplicate mask page, and duplicate path asset where applicable. Preserve the current production scene and material.

## 1. Import and compile

1. Import the patch into the existing SurfaceWeaver package folder.
2. Wait for Unity domain reload and shader/import completion.
3. Confirm there are no C# compiler errors.
4. Open Window > SurfaceWeaver > Painter and select the test target.

Pass: Painter opens and existing painting/UV diagnostic workflows remain available.

## 2. Automatic tangent compatibility

1. Create an open path with at least three anchors.
2. Inspect the preview before editing any handles.
3. Compare the curve shape with the previous v0.7.12 path preview on the same anchor positions.

Pass: automatic tangents create a smooth Catmull-Rom-equivalent path; old path assets still load with automatic tangents.

## 3. Move anchors

1. Select an anchor in Scene View.
2. Drag its center to another location on the target mesh.
3. Drag it outside the target surface.

Pass: valid drags snap to the target MeshCollider. A drag too far from the target is rejected with a status message; no anchor becomes detached from the mesh.

## 4. Edit incoming/outgoing handles

1. Select a middle anchor.
2. Drag the incoming tangent handle.
3. Drag the outgoing tangent handle independently.
4. Confirm the preview curve updates immediately.
5. Use Reset Selected Handles.

Pass: each handle reshapes the appropriate adjacent Bézier segment; reset restores automatic tangents.

## 5. Open and closed paths

1. Test a two-anchor open path.
2. Test a three-or-more-anchor open path.
3. Enable Close Path and inspect the final-to-first segment.
4. Disable Close Path again.

Pass: open endpoints remain well behaved and a closed path connects continuously from the final anchor back to the first.

## 6. Delete and selection behavior

1. Select a middle anchor and press Backspace.
2. Select another anchor and press Delete.
3. With no selection, press Backspace.
4. Click the mesh away from handles to add a new anchor.

Pass: only the intended anchor is removed; mesh clicks still add anchors; dragging handles does not add anchors accidentally.

## 7. Save/load path assets

1. Edit at least one anchor and both tangent handles.
2. Save a new path asset.
3. Clear the in-window path and load the saved asset.
4. Confirm custom tangent shape and closed-loop setting are restored.
5. Load a path asset created before this patch.

Pass: new custom tangents persist; older path assets remain compatible and use automatic tangents.

## 8. Apply, Undo and mask persistence

1. Apply the edited path using seam-aware projection.
2. Confirm the mask is painted as one Undo step.
3. Undo and confirm the paint is reverted.
4. Reapply and save the mask PNG.
5. Reopen the scene and confirm the mask asset persists.

Pass: existing painting workflow, projection and one-stroke Undo remain unchanged.

## 9. Regression/performance checks

1. Test short and long paths, sharp curves, and closed paths on a representative mesh.
2. Compare stamp spacing on curved segments with straight segments.
3. Check no excessive preview allocations occur while idle; curve preview should run only during Scene View repaint.
4. Repeat the runtime Android benchmark only after the Editor feature tests pass; spline editing itself is Editor-only and should not add player runtime work.

Pass: no missing references, exceptions, unexpected mask changes, or runtime dependencies are introduced.
