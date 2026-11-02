# SurfaceWeaver v0.8.0-preview.5.1 — Continuous Cross-Target Painting

## Scope
Adds a separate Multi-Mesh Painter window. The existing single-target Painter and spline authoring workflow remain unchanged.

## Workflow
1. Create or assign a `SurfacePaintTargetSet` in Window > SurfaceWeaver > Multi-Mesh Workspace.
2. Add existing `SurfacePaintTarget` components from saved, loaded scenes.
3. Open Multi-Mesh Painter.
4. Review each target's selected paint layer and validation status.
5. Arm Scene Painting and drag through the Scene View. The nearest registered, ready target receives each sample.
6. Use grouped Undo/Redo for the entire cross-target drag, Save All Masks to persist, or Escape while painting to cancel the active drag.

## Per-target requirements
- Active `SurfacePaintTarget` with a `SurfacePaintProfile`.
- Packed RGBA mask pages, with the selected layer on page 0.
- A unique saved mask-page asset for each target/layer session; shared texture assets are rejected to avoid conflicting GPU buffers.
- UV0 and a readable mesh.
- A `MeshRenderer` and matching non-convex `MeshCollider` on the same target GameObject.
- The collider's `sharedMesh` must match the mesh returned by the target.
- A compatible material property (`_MaskPage0` for the SurfaceWeaver layered URP shader or `_PaintMask` for legacy preview).

## Stroke behavior
- One pointer drag can cross multiple registered meshes.
- Each touched target lazily loads its own GPU mask session.
- One logical stroke is grouped across all touched target sessions.
- Undo/Redo is grouped across the touched targets. Escape cancels the in-progress stroke on every touched target.
- Paint, Erase and Smooth operations are supported.
- Save All Masks writes each dirty mask page back to its existing asset.

## Safety and compatibility
- The Multi-Mesh Painter is separate from the single-target Painter and spline editor.
- Before taking a target from an open single-target Painter, the latter commits pending spline preview, saves, and releases its GPU session. Returning reloads the saved mask to avoid stale GPU data overwriting newer work.
- Switching target sets and refreshing the list are blocked if dirty mask sessions cannot be saved.
- Do not use the same mask-page texture asset for multiple targets.
- This preview does not provide cross-mesh spline authoring, shared profile editing, child-collider discovery, non-UV0 painting, multi-page painting, or seamless geometry-aware interpolation across a mesh boundary. A cross-target drag resumes stamping on the newly hit mesh; it does not project stamps through the gap between separate meshes.

## Validation status
Source structure and lexical checks passed. Unity compilation, Scene View interaction, material preview, persistence and grouped Undo/Redo must be verified in the consuming project.
