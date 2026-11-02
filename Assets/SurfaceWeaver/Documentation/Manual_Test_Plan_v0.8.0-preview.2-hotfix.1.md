# SurfaceWeaver v0.8.0-preview.2-hotfix.1 — Manual regression test plan

Use a duplicate scene, path asset, SurfacePaintProfile and mask page. Keep the current preview.2 working copy backed up.

## 1. Import and compile
1. Merge the hotfix into the existing SurfaceWeaver package folder.
2. Wait for Unity's domain reload.
3. Check the Console for C# errors.
4. Open Window > SurfaceWeaver > Painter and enable Path Authoring.

Pass: no compile errors; previous path assets and existing surface anchors still load.

## 2. Multiple anchor direct editing — primary regression
1. Register a ground MeshCollider.
2. Add three anchors: P1, P2 and P3.
3. P3 will be selected automatically after creation.
4. Without changing the point count, drag P1 directly in Scene View.
5. Drag P2 directly in Scene View.
6. Drag P3 directly in Scene View.
7. Repeat in Surface Snap and Free Move modes.

Pass: all three anchors move independently. Adding a later anchor does not lock earlier anchors. In Surface Snap mode the dragged point rebinds to the registered collider under the cursor. In Free Move mode it moves in world space.

## 3. Explicit Inspector selection
1. Create at least three anchors.
2. Use the **Edit Anchor** popup to select P1, then P2, then P3.
3. For each selection, edit incoming/outgoing tangent handles.
4. Use Deselect Anchor and select again from the popup.

Pass: every point can be selected from the popup and exposes its own tangent handles. The selected label and Scene View highlight match the popup selection.

## 4. Surface Offset and detached points
1. Create two surface-bound anchors.
2. Select Surface Offset mode and drag P1's normal slider.
3. Drag P2's normal slider.
4. Detach one point using Free Move.
5. Keep Surface Offset selected and move the detached point.

Pass: bound points offset along their surface normal; detached points remain movable in world space and do not become stuck.

## 5. Surface target removal
1. Register the active target collider and a second building collider.
2. Confirm both appear under Surface Targets.
3. Remove the active target collider using its Remove button.
4. Confirm it disappears from the list.
5. Toggle Path Authoring off and back on.
6. Confirm the removed collider is not silently re-added.
7. Click Add Active Target and confirm it returns.

Pass: the active target can be removed and re-added explicitly. A removed collider cannot receive new anchors or Surface Snap rebindings until registered again.

## 6. Existing anchors after surface removal
1. Add P1 to the ground and P2 to the building.
2. Remove the building collider from Surface Targets.
3. Move/rotate the building GameObject.
4. Inspect P2 and its curve segment.
5. Save and reload the path asset.

Pass: existing P2 remains attached to the building and follows its transform even though that collider is not currently in the snap-target allow-list. Removing the collider only prevents new snapping to it.

## 7. Regression checks
1. Reorder anchor selection P3 -> P1 -> P2 and drag each.
2. Edit tangents on a non-last anchor.
3. Test open and closed paths.
4. Test Delete/Backspace removal.
5. Save/reload a path asset.
6. Confirm Apply Spline still rejects paths spanning other colliders or Free Move anchors without changing the active mask.

Pass: no exceptions, anchors do not jump to another mesh, no previous anchor becomes uneditable, and no unsupported cross-target mask write occurs.
