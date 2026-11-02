# Manual Test Plan — SurfaceWeaver v0.8.0-preview.6

Use a duplicate scene or a version-control checkpoint. Do not test on the only copy of production scenes.

## 1. Compile and open

1. Import the preview.6 patch.
2. Confirm Unity recompiles without C# errors.
3. Open `Window > SurfaceWeaver > Object & Vegetation Scatter`.

Pass: the window opens without exceptions; the existing Painter, Multi-Mesh Workspace and spline tools still open.

## 2. Create a scatter profile

1. Assign a valid `SurfacePaintTargetSet`.
2. Click `Create Profile…` and save the profile under `Assets`.
3. Expand `Weighted Prefabs` and add at least two prefab assets.
4. Give the prefabs different weights and scale ranges.
5. Save the profile.

Pass: the profile persists after closing/reopening the window and Unity.

## 3. Validate targets

1. Add two saved-scene SurfacePaintTargets to the target set.
2. Ensure each has an enabled MeshRenderer and an enabled, non-convex MeshCollider referencing its MeshFilter shared mesh.
3. Click `Refresh Targets`.

Pass: both appear ready. Disable or mismatch one collider and confirm it is skipped safely; restore it and refresh.

## 4. Paint scatter instances

1. Set a modest brush radius (for example 2), instances per stamp (for example 6), stroke spacing (for example 1) and minimum distance (for example 0.6).
2. Click `Enable Scene Placement`.
3. Drag over the first target in Scene View.
4. Continue the drag onto the second registered target.

Pass: prefab instances are placed on both surfaces, stay on the sampled mesh, respect the spacing rule, and are grouped beneath a `SurfaceWeaver Scatter - <Profile>` scene root with a separate target group per mesh.

## 5. Distribution and alignment

1. Use two prefab entries with clearly different weights.
2. Paint several stamps using a fixed profile seed.
3. Verify both prefabs can be selected and scale varies within each entry's configured range.
4. Enable surface-normal alignment and test sloped geometry.
5. Set maximum slope to a low value and test a steep face; then increase the limit and test again.
6. Enable random pitch/roll and compare.
7. Put a test collider on a dedicated layer, add that layer to Exclusion Layers and set a small exclusion radius. Paint near it, then clear the mask and test with Exclusion Layers set to Nothing.

Pass: selection is weighted, transforms remain within configured ranges, normal alignment works, slope filtering rejects disallowed faces, and exclusion-layer colliders keep placements outside the configured radius. Registered paint surfaces are not treated as exclusions.

## 6. Erase and Undo/Redo

1. Switch to `Erase Objects`.
2. Drag over placements on one target.
3. Undo and redo the erase.
4. Switch back to `Paint Objects`, create another stamp and undo/redo it.

Pass: erase affects only instances under the currently hit target group; each stamp is one logical Undo step; redo restores the expected objects.

## 7. Scene persistence and prefab links

1. Save the scene.
2. Close/reopen Unity or reload the scene.
3. Confirm scatter root, target groups and instances remain.
4. Select an instance and inspect its Prefab link.
5. Reopen the scatter window and paint/erase again.

Pass: all placed objects and their prefab connections persist, and the tool finds the existing per-profile/per-target containers.

## 8. Regression checks

1. Open the original single-target Painter and paint a normal brush stroke.
2. Open spline authoring; create/move anchors and edit width/pressure.
3. Commit/cancel live spline preview.
4. Open Multi-Mesh Painter and confirm the previous cross-target painting workflow remains available.
5. Open Batch Mask Export.

Pass: scatter functionality does not modify or break existing painting, spline, workspace, cross-target painting or export behavior.

## 9. Performance sanity

1. Test with a small number of low-poly prefabs.
2. Test with larger/dense vegetation prefabs and a larger number of placements.
3. Compare Editor responsiveness and resulting scene runtime frame time using the existing benchmark scenes.

Pass: the authoring window does not add runtime scripts to placements. Record runtime performance changes caused by the placed prefab content itself; do not attribute those costs to the editor scatter window.
