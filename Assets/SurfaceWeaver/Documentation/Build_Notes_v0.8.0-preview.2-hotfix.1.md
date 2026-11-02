# SurfaceWeaver v0.8.0-preview.2-hotfix.1 — Anchor editing and surface target removal

## Fixed

- Every spline anchor now responds to its own Scene View movement handle. Previously, the handle updated a point only when that point was already selected, so newly-created (automatically selected) anchors worked while earlier points could appear locked.
- Clicking an anchor selects it using the nearest-control or active-hot-control result.
- Added an **Edit Anchor** Inspector popup to select any P1, P2, P3... anchor explicitly. The selected anchor's incoming/outgoing tangent handles are then shown as before.
- Surface Offset mode now uses the normal slider consistently for every surface-bound anchor, including an anchor that becomes selected during a drag.
- Detached/world-space anchors remain movable with Free Move semantics when Surface Offset is selected; they have no surface normal offset to edit.

## Surface Targets behavior

- **Remove** is now enabled for every registered collider, including the collider on the active SurfacePaintTarget.
- The list is an explicit allow-list for placing new anchors and snapping/rebinding anchors in Surface Snap mode.
- Removing a collider does not invalidate existing anchors bound to it. Their saved GlobalObjectId and collider-local data still resolve, so they continue following that object's transform.
- Resolving an existing anchor no longer silently inserts its collider back into the snapping allow-list.
- Toggling Path Authoring no longer silently re-registers the active target after the user removes it. Use **Add Active Target** or **Add Selected MeshCollider** to register it again.

## Compatibility and scope

- No serialized runtime path-point fields or formats were changed.
- Existing preview.1/preview.2 path assets remain compatible.
- Cross-target mask painting is still not implemented. This hotfix changes anchor editing and the registered snap-target list only.

## Import

1. Back up the project or commit the current working state.
2. Merge this patch into the existing `Assets/SurfaceWeaver` folder.
3. Let Unity compile and check the Console.
4. Follow `Manual_Test_Plan_v0.8.0-preview.2-hotfix.1.md`.

## Validation status

The changed source passed lexical C# delimiter checks and targeted source regression checks in the patch-building environment. Unity Editor compilation, Scene View handle interactions and target-list behavior must still be verified in the consuming project.
