# SurfaceWeaver v0.8.0-preview.2-hotfix.2

## Regression fixes

- Fixed spline point placement: the path background/default Scene View control now owns surface clicks. The previous `GUIUtility.hotControl == 0` condition rejected clicks after Unity assigned the default control, so no new anchors were created.
- Prevented Scene View selection changes from switching the active `SurfacePaintTarget` while Path Authoring is enabled. This avoids clicking a child mesh selecting its parent target, clearing the path, and disabling authoring.
- Active target registration now discovers enabled `MeshCollider` components on the target GameObject and its children. This supports the common hierarchy where `SurfacePaintTarget` lives on a parent and the ground/building mesh collider lives on a child.
- The explicit **Add Active Target + Children** command can re-register those colliders after a user removes them from the Surface Targets allow-list.

## Preserved behavior

- Existing anchor selection and direct movement from hotfix.1.
- Surface Snap, Free Move and Surface Offset.
- Existing collider bindings continue to resolve independently of the snapping allow-list.
- User-removed colliders are not silently re-added when toggling Path Authoring.
- Cross-target mask painting remains out of scope; Apply Spline guards are unchanged.

## Manual validation required

1. Open the Painter for a parent `SurfacePaintTarget` with a child ground GameObject containing an enabled `MeshCollider` and shared mesh.
2. Enable Path Authoring. Confirm the child collider is listed under Surface Targets.
3. Click three different unobstructed ground locations. Confirm P1, P2 and P3 are created.
4. Click/drag P1 and P2. Confirm older anchors remain selectable and movable.
5. Click a child building collider registered as another surface. Confirm a new anchor binds to that child.
6. Confirm the active Painter target does not change and the path is not cleared when clicking the child.
7. Remove one collider from Surface Targets. Confirm it stays removed after toggling Path Authoring off/on.
8. Use Add Active Target + Children or Add Selected MeshCollider to register it again.
9. Confirm anchor bindings to removed colliders still follow those transforms.
10. Confirm no unrelated Scene View clicks create anchors when they are not over a registered collider.

This patch has source-level validation only. Compile and run the above tests in the consuming Unity project before accepting the preview.
