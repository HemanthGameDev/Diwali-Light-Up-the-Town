# Build Notes — v0.8.0-preview.2-hotfix.3

## Scope
Restore the reliable single-active-SurfacePaintTarget spline workflow and keep all existing spline anchors independently editable. The experimental multi-surface target list and per-point surface binding are removed.

## User workflow
1. Select the intended `SurfacePaintTarget` in the Painter.
2. Enable Path Authoring.
3. Click that target mesh in Scene View to add anchors.
4. Select anchors by clicking their Scene View spheres or choosing P1, P2, P3, etc. from **Edit Anchor**.
5. Drag any anchor sphere to move it on the active target.
6. With an anchor selected, edit its incoming/outgoing tangent handles.
7. Save or apply the path using the existing controls.

## Removed from this preview
- Surface Targets registration UI.
- Cross-mesh surface snapping and per-anchor mesh binding.
- Free Move and Surface Offset modes.

## Notes
- The active target must have the existing supported non-convex MeshCollider for placement/movement.
- Anchors are stored in active-target local space.
- Clicking other meshes does not switch the Painter target or add a registered surface.
- Paths containing preview.2 secondary-mesh bindings or Free Move anchors should be recreated.
