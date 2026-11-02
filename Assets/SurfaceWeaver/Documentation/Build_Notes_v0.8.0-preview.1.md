# SurfaceWeaver v0.8.0-preview.1 — Editable Spline Anchors and Tangent Handles

## Scope

This preview extends the existing v0.7.12 spline/path authoring workflow. It does not replace the path asset format or the mask painting backend.

## Included in source

- Scene View anchor selection and dragging.
- Anchor movement snaps to the target's non-convex MeshCollider using `Collider.ClosestPoint`.
- Selected anchors expose incoming and outgoing tangent handles.
- Cubic Bézier evaluation uses the stored target-local tangent offsets.
- Anchors without custom tangents use automatic Catmull-Rom-equivalent Bézier tangents, preserving the previous smooth path shape for existing assets.
- Custom tangent offsets are serialized inside `SurfacePaintPathPoint`, so reusable path assets retain edited handles.
- Reset Selected Handles and Reset All Handles restore automatic tangents.
- Backspace removes the selected anchor, or the final anchor when none is selected. Delete removes only the selected anchor.
- Closed paths remain supported by the existing Close Path toggle.
- Spline stamp spacing estimates the curve's sampled arc length rather than only the anchor-to-anchor chord.
- Existing seam-aware/legacy projection, one-stroke Undo, deterministic random seed, and mask persistence workflows are retained.

## Serialization compatibility

The new fields are additive. Existing path assets have no serialized tangent data, so `HasCustomTangents` is false and the Editor calculates automatic tangents. Existing saved paths do not require manual migration.

## Known limitations in this preview

- Tangent handles are independently editable; linked/mirrored tangent modes are not included yet.
- Anchor movement requires a non-convex MeshCollider on the target and uses the collider's closest point. Complex overlapping surfaces should be checked visually after moving an anchor.
- Tangent handles are stored in target-local space. Non-uniform/negative transform edge cases remain part of v0.9 validation.
- Variable path width, pressure/opacity curves, multi-mesh authoring, batch export, object scatter and topology-aware Flood Fill are not included in this preview.
- This source patch has not been compiled or exercised inside the consuming Unity project. Unity 6 Editor compilation and the manual test plan below are required before treating it as validated.
