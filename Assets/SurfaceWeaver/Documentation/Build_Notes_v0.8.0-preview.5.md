# SurfaceWeaver v0.8.0-preview.5 — Multi-Mesh Workspace

## Included

- Added a separate `Window > SurfaceWeaver > Multi-Mesh Workspace` Editor window.
- Added reusable `SurfacePaintTargetSet` assets. Scene targets are stored as `GlobalObjectId` strings rather than illegal direct scene-object references inside an asset.
- Add selected targets that already have `SurfacePaintTarget` components.
- Discover and add targets from all currently loaded scenes.
- Resolve target-set entries when their scenes are loaded; show unresolved entries. Rows can be removed manually. The workspace does not automatically prune unresolved IDs because their scenes may simply be unloaded.
- Validate each target for active state, assigned profile, readable mesh, MeshRenderer, UV0 and matching non-convex MeshCollider.
- Warn when multiple targets in one set share a SurfacePaintProfile, because they also share that profile's mask-page assets.
- Select/ping a target and open that target in the existing Painter.
- Open the existing Batch Mask Export window using the target set selection.
- Removing/clearing workspace entries changes only the target-set asset. It does not remove components or modify scene objects, profiles, masks, meshes or materials.

## Deliberate scope

- The existing Painter remains single-active-`SurfacePaintTarget`.
- The workspace manages and opens multiple targets one at a time. It does not yet perform a continuous brush stroke across mesh boundaries.
- This preview does not reintroduce the abandoned Surface Targets/surface-binding section in the spline editor.
- It does not auto-add or remove colliders, add `SurfacePaintTarget` components, alter object parents/transforms, or rewrite materials.
- Before switching Painter targets, save the active mask and save any path you need as a Path Asset. The existing Painter disposes the previous target's in-memory editing session when changing targets.
- Targets must belong to saved scenes before they can be added to a reusable target-set asset, because the workspace stores stable scene `GlobalObjectId` values.
- Batch Mask Export remains a separate operation and copies persisted mask-page source files only.

## Workflow

1. Save the scene containing your `SurfacePaintTarget` components.
2. Open `Window > SurfaceWeaver > Multi-Mesh Workspace`.
3. Create a new target-set asset, or assign an existing one.
4. Select one or more GameObjects that already have `SurfacePaintTarget` components and click `Add Selected Targets`, or use `Find All Loaded`.
5. Review target validation statuses.
6. Click `Open Painter` on one target to edit it using the existing single-target Painter.
7. Save the mask/path before switching to another target.
8. Use `Open Batch Mask Export` to export persisted pages for the whole set.

## Validation status

The patch has source-structure and archive checks only. Unity compilation, EditorWindow layout, target-set asset serialization, GlobalObjectId resolution after editor restart/scene reload, multi-selection, and Painter handoff must be tested in the consuming Unity project using `Manual_Test_Plan_v0.8.0-preview.5.md`.
