# SurfaceWeaver v0.8.0-preview.5 — Manual Test Plan

Use a project backup. This preview adds a separate workspace and must not modify the existing Painter/spline workflow.

## 1. Create and persist a target set

1. Save a scene containing at least two existing `SurfacePaintTarget` GameObjects.
2. Open `Window > SurfaceWeaver > Multi-Mesh Workspace`.
3. Click `New…` and create a `SurfaceWeaverTargetSet` asset under `Assets`.
4. Select two or more GameObjects with existing target components.
5. Click `Add Selected Targets`.
6. Confirm each target appears once and the target-set asset is marked/saved.
7. Close and reopen the workspace; confirm references resolve.

Pass: entries resolve to the original scene objects after workspace close/reopen and scene reload.

## 2. Discover loaded targets

1. Load a second scene additively containing another target.
2. Click `Find All Loaded`.
3. Confirm targets from both loaded scenes are added, without duplicates.
4. Unload one scene and refresh the workspace.
5. Confirm unloaded targets are shown as unresolved until the scene is loaded again.
6. Confirm the workspace does not automatically delete unresolved entries; they may belong to an unloaded scene.

Pass: discovery includes only loaded scene objects, not project prefab assets.

## 3. Validation behavior

Test targets with:
- Missing profile.
- Missing MeshFilter/shared mesh.
- Non-readable mesh.
- UV channel other than 0.
- Missing MeshRenderer.
- Missing MeshCollider.
- Convex MeshCollider.
- Collider referencing a different mesh.
- A fully configured target.

Pass: each row shows a useful status; no collider/component is automatically added or changed.

## 4. Open Painter handoff

1. Save a mask and save the current spline as a Path Asset.
2. Click `Open Painter` for target A.
3. Confirm the existing Painter opens with target A.
4. Return to the workspace and open target B.
5. Confirm the existing Painter opens with target B.
6. Re-test brush painting, spline anchor creation, anchor movement, tangent handles, width/pressure and live preview on each target.

Pass: the workspace does not change the single-target Painter behavior or the spline authoring UI. Confirmed dialog warns to save current mask/path before target switching.

## 5. Shared-profile warning

1. Assign the same `SurfacePaintProfile` to two different targets.
2. Add both targets to the set.
3. Confirm each row warns that the profile/mask pages are shared.

Pass: the warning explains that painting on either target updates the same shared profile mask assets; the workspace does not automatically duplicate or change profiles.

## 6. Workspace-only removal and clear

1. Remove one target from the workspace.
2. Confirm its GameObject and `SurfacePaintTarget` component remain in the scene.
3. Clear the target set after confirmation.
4. Confirm the asset list clears without modifying scene objects.

Pass: only target-set entries are changed.

## 7. Batch export integration

1. Add two targets with persisted mask pages.
2. Click `Open Batch Mask Export`.
3. Confirm those targets are selected in the Hierarchy and the existing batch exporter opens.
4. Export to a temporary folder and inspect the output/manifest.

Pass: exports correspond to the workspace targets and source masks remain unchanged.

## 8. Regression tests

- Existing single-target Painter still opens from the target Inspector.
- Existing single-target Scene View brush drawing works.
- Existing spline point creation and independent point editing work.
- Tangent handles, closed paths, width/pressure, live preview Commit/Cancel, Undo/Redo and saved path assets remain functional.
- No Surface Targets section or secondary-mesh snapping UI appears in the spline editor.
- No compilation errors or new warnings are introduced.
