# Manual Test Plan — v0.8.0-preview.5.1

## Setup
- Use two or more saved-scene `SurfacePaintTarget` objects.
- Ensure each has a distinct profile/mask-page asset, readable UV0 mesh, MeshRenderer and matching non-convex MeshCollider on the same GameObject.
- Select a layer on page 0 for each target.
- Assign the targets to a reusable Target Set.

## Tests
1. Open Window > SurfaceWeaver > Multi-Mesh Painter and assign the Target Set.
2. Confirm every supported target reports Ready; confirm unsupported targets explain why they are blocked.
3. Arm Scene Painting and paint a stroke on target A. Confirm only A changes.
4. Paint one continuous drag from target A onto target B. Confirm both previews update.
5. Release the mouse. Confirm one grouped stroke is committed.
6. Click Undo Stroke. Confirm both A and B return to their pre-stroke masks.
7. Click Redo Stroke. Confirm both masks are restored.
8. Start another cross-target drag and press Escape before releasing. Confirm all touched targets return to their pre-stroke state and no dirty state is introduced by the cancelled stroke.
9. Test Erase and Smooth across two targets.
10. Save All Masks. Confirm each target's own mask-page asset is updated and remains distinct.
11. Close and reopen the window. Confirm saved masks persist.
12. Open the single-target Painter for a target, make an unsaved change, then open Multi-Mesh Painter. Confirm the single-target session saves/releases before multi-mesh painting.
13. Return to the single-target Painter. Confirm it reloads the newer saved mask, not an old GPU buffer.
14. Attempt to assign the same mask-page asset to two targets. Confirm validation blocks both rows.
15. Try target-set switching/refresh with a simulated save failure or read-only mask asset. Confirm the session is not silently disposed.
16. Verify single-target brush painting, spline placement, anchor editing, live spline preview, Commit/Cancel and Undo still work after closing Multi-Mesh Painter.

## Acceptance
All supported targets paint correctly; cross-target strokes update every touched mask; grouped Undo/Redo and Escape cancellation are consistent; saved assets persist; single-target Painter/spline behavior remains unchanged.
