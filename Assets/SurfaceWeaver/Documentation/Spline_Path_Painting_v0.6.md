# Spline / Path Painting — v0.6.0

## Workflow

1. Save the scene at least once.
2. Select a SurfacePaintTarget and load an enabled layer's mask page.
3. Enable Path Authoring.
4. Click the mesh in Scene View to add anchors. Add at least two points.
5. Review the Catmull-Rom preview. Press Backspace to remove the most recent anchor.
6. Save Path Asset to persist a reusable path recipe, or Apply Spline to paint.
7. Apply Spline creates one GPU Undo stroke. Save the mask page separately to persist the mask result.

## Path recipe data

The asset stores target-local anchor positions/normals, target GlobalObjectId string and display name, selected layer ID, page/channel, brush radius, opacity, hardness, spacing, paint/erase/smooth mode, brush preset or alpha override, rotation, scale jitter and random seed. Reapplying a saved path with the same settings/seed produces the same randomized brush stamp sequence, assuming the same SurfaceWeaver version and path sample settings.

The asset does not directly reference a scene component because a project asset cannot safely persist a scene-object reference. The Editor resolves its stored GlobalObjectId while the source scene is loaded. Unity's GlobalObjectId is tied to the scene identity: save the scene before creating the asset, keep the scene loaded when resolving, and be aware that moving the object to another scene changes its ID. If resolution fails, the Editor asks whether to use the selected target; only do so when its local mesh layout is compatible.

## Current limitations

- Catmull-Rom interpolation is used between fixed anchors.
- Anchors can be added and the most recent anchor removed; drag handles are not implemented.
- Open paths only; closed-loop authoring is pending.
- One constant brush radius/width per path; per-anchor width and pressure curves are pending.
- Applying the path modifies the active mask page, not a separate non-destructive layer. Use the stroke Undo or a duplicated page before experimenting.
