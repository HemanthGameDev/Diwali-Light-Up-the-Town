# SurfaceWeaver v0.8.0-preview.2 — Manual Test Plan

Use a duplicate scene, duplicate SurfacePaintProfile, duplicate mask page and duplicate path asset. Preserve the working v0.8.0-preview.1 backup.

## 1. Import and compile

1. Import the preview.2 patch into the same SurfaceWeaver folder.
2. Wait for Unity's domain reload.
3. Check Console for C# errors.
4. Open Window > SurfaceWeaver > Painter and select the primary SurfacePaintTarget.

Pass: Painter opens and the existing preview.1 anchor/tangent workflow remains available.

## 2. Register surfaces

1. Confirm the active target MeshCollider is listed automatically.
2. Select a separate building GameObject with a MeshCollider.
3. Click **Add Selected MeshCollider**.
4. Confirm both colliders appear under Surface Targets.
5. Remove the non-primary collider and add it again.

Pass: only valid enabled MeshColliders with an assigned shared mesh are accepted; the active target collider cannot be removed from the list.

## 3. Add and move surface-bound anchors

1. Select Surface Snap mode.
2. Click the ground collider to add two or more anchors.
3. Select one anchor and drag it with the Scene View cursor over the registered building collider.
4. Move the cursor over the ground again and drag it back.

Pass: the selected anchor changes its surface binding to the collider under the cursor. It must not remain constrained to the original target collider. Clicking empty space must not rebind the anchor.

## 4. Transform-following

1. Bind an anchor to the building MeshCollider.
2. Move the building GameObject in the Scene View or Inspector.
3. Rotate and uniformly/non-uniformly scale the building.
4. Inspect the anchor, tangent handles and path preview.

Pass: the anchor and its curve attachment follow the bound GameObject transform. This preview does not promise vertex-deformation/skinned-mesh attachment.

## 5. Surface offset

1. Select an anchor bound to a collider.
2. Choose Surface Offset mode.
3. Drag the normal-axis arrow slider.
4. Change the numeric Surface Offset field.
5. Move the bound mesh.

Pass: the point moves along the transformed surface normal and retains its collider binding/offset after transform changes.

## 6. Free Move

1. Select an anchor and choose Free Move.
2. Drag it away from all surfaces.
3. Move and rotate the original collider GameObject.
4. Inspect the point and tangent handles.

Pass: the point remains at its world-space position and does not follow the former surface. Surface Snap can reattach it when dragged over a registered collider.

## 7. Tangent preservation during rebind

1. Edit incoming and outgoing handles on an anchor.
2. Rebind the anchor from ground to building using Surface Snap.
3. Inspect the Bézier preview and both handles.
4. Reset selected handles and reset all handles.

Pass: custom tangent vectors are converted so their world-space directions/lengths are preserved on rebind; reset restores automatic tangents.

## 8. Save/load and legacy compatibility

1. Add anchors while the test scene is still unsaved, then save the scene.
2. Save a path asset containing points on at least two registered colliders and one Free Move point.
3. Clear in-window anchors and load the path asset again.
4. Move each bound object and verify the correct points follow.
5. Repeat after closing and reopening the scene and Unity Editor.
6. Load a path asset created before preview.2.

Pass: new collider bindings, world-space points, offsets and tangent edits persist. Legacy points with no per-point binding remain attached to the root target. Saving refreshes the current GlobalObjectIds (including after the scene is first saved), rejects unresolved collider bindings and rejects unsaved scene references.

## 9. Apply safety boundary

1. Create a path whose points all belong to the active SurfacePaintTarget collider. Apply it on a duplicate mask page.
2. Create a path with at least one point attached to a different collider.
3. Click Apply Spline and inspect the message and mask.

Pass: active-target-only paths apply using the existing one-stroke Undo workflow. Multi-collider or Free Move paths are rejected with an explicit message and do not change any mask. Cross-target mask painting is deferred to the v0.8 multi-mesh authoring feature.

## 10. Existing workflow regressions

1. Test open and closed paths.
2. Test Backspace/Delete anchor removal.
3. Test custom tangent persistence.
4. Test Apply, Undo, save PNG and reload for a path on the active target.
5. Confirm no new runtime assembly references to UnityEditor were introduced.

Pass: no exceptions, missing references, unintended mask writes or regressions in preview.1 functionality.
