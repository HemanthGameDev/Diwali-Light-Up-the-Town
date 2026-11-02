# Manual Test Plan — v0.8.0-preview.2-hotfix.2

Use a duplicate scene and duplicate path asset. Keep the current working project backup untouched.

## A. Parent target with child mesh collider
1. Create/select a GameObject with `SurfacePaintTarget` on the parent.
2. Put the renderable ground mesh and an enabled `MeshCollider` on a child GameObject.
3. Open Window > SurfaceWeaver > Painter for the parent target.
4. Confirm the child MeshCollider appears in Surface Targets automatically.
5. Enable Path Authoring.

Pass: the Painter target remains the parent SurfacePaintTarget, while the child collider is available for placement.

## B. New anchor placement
1. Click three separated points on the ground surface.
2. Confirm P1, P2 and P3 appear in order.
3. Click an empty part of the Scene View not covered by an anchor and not over a registered collider.

Pass: three valid points are created on the registered mesh; the invalid click does not create an anchor.

## C. Selection must not switch the Painter target
1. While Path Authoring remains enabled, click the child ground mesh and another registered child mesh.
2. Watch the active target and path point count in the Painter.

Pass: clicking child meshes does not switch the active Painter target, clear path points, or disable Path Authoring.

## D. Existing anchors remain editable
1. Drag P1 after P2 and P3 exist.
2. Drag P2 and P3.
3. Select P1/P2/P3 through the Edit Anchor dropdown.
4. Edit incoming/outgoing tangent handles.

Pass: each anchor is independently selectable and movable; editing one does not create a new point.

## E. Surface list behavior
1. Remove one collider from Surface Targets.
2. Toggle Path Authoring off and on.
3. Confirm the removed collider stays out of the snapping allow-list.
4. Use Add Active Target + Children or Add Selected MeshCollider to add it again.

Pass: explicit removal is respected; manual re-registration works.

## F. Transform-following
1. Create an anchor on a registered child mesh.
2. Move, rotate and non-uniformly scale the child mesh.

Pass: the bound anchor follows the collider GameObject transform.

## G. Regression checks
1. Confirm normal brush painting works when Path Authoring is disabled.
2. Confirm spline path creation works when Path Authoring is enabled.
3. Confirm clicking/dragging an existing anchor does not create another anchor.
4. Confirm Apply Spline continues to enforce the active-target-only safety guard.

## Validation record
Record Unity version, Console errors, number of anchors created, and whether the child collider hierarchy was used. Source checks do not replace Unity Editor compilation and Scene View testing.
