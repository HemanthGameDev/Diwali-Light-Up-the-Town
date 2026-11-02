# Manual Test Plan — v0.8.0-preview.2-hotfix.3

Use a duplicate scene, duplicate active target, duplicate profile, and duplicate path asset.

## A. Restore the single-target UI
1. Open the Painter and select one `SurfacePaintTarget`.
2. Confirm there is no **Surface Targets** registration section.
3. Confirm there are no Surface Snap / Free Move / Surface Offset mode controls.
4. Confirm Path Authoring still has the original enable/disable toggle.

Pass: the active Painter target is the only path-painting target.

## B. Create and edit multiple anchors
1. Enable Path Authoring.
2. Click the active target mesh three or more times to create P1, P2, P3.
3. Drag P1, then P2, then P3 independently.
4. After creating P3, return to P1 and P2 and drag them again.
5. Select each anchor using the **Edit Anchor** dropdown and confirm its Scene View handle is highlighted.

Pass: creating a new anchor never makes older anchors uneditable. Every point can be selected and moved.

## C. Tangents and paths
1. Select P2 and drag its incoming tangent.
2. Drag its outgoing tangent.
3. Reset selected tangents; test Reset All Handles.
4. Enable Close Path and verify the final segment.
5. Save the path asset, reload its points, and confirm positions/tangents persist.

Pass: curve updates immediately and asset persistence retains custom handles.

## D. Target boundaries
1. With Path Authoring active, click the intended active target and add points.
2. Click a different mesh.
3. Confirm it does not get added as a new target and does not switch the active Painter target.
4. Confirm the original spline remains intact.

Pass: the tool consistently uses only the active `SurfacePaintTarget`.

## E. Apply and mask persistence
1. Apply a path containing at least three anchors.
2. Confirm the selected mask channel receives the path.
3. Undo and redo the applied path.
4. Save the mask page and verify the texture persists.

Pass: existing spline-to-mask workflow remains functional.
