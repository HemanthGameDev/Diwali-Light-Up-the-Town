# SurfaceWeaver v0.8.0-preview.2 — Surface-Aware Spline Editing

## Included

- Registered MeshCollider surface targets for spline point placement and snapping.
- Surface Snap mode raycasts from the Scene View cursor against registered colliders and rebinds the selected anchor to the hit collider.
- Anchors store a collider GlobalObjectId plus collider-local position and normal, so they follow that mesh object's translation, rotation and scale.
- Incoming/outgoing custom tangent offsets are converted through world space when an anchor is rebound to another collider, preserving the visible handle shape.
- Free Move mode detaches a selected point and stores its position, normal and custom tangent offsets in world space.
- Surface Offset mode provides a normal-axis Scene View slider and a numeric offset field; offsets are stored with the path point.
- Multi-surface paths can be previewed and saved as path assets. Registered collider bindings are restored when the asset is loaded and the scene objects resolve.
- Saving refreshes each surface's GlobalObjectId immediately before serialization. This covers anchors created before the first scene save, provided the relevant scene is saved before the path asset is saved.
- Legacy v0.7 path points with no per-point surface ID continue to use the path asset's original SurfacePaintTarget.
- Existing closed-path, custom tangent, brush settings and deterministic seed data are preserved.

## Important scope boundary

The current Painter mask session still owns one active `SurfacePaintTarget`, one profile and one mask page. Therefore, **Apply Spline is allowed only when all path points are attached to the active target collider**. A path that contains points attached to other colliders or Free Move points can be edited and saved, but the Apply command stops with an explicit message instead of painting the wrong mask. Cross-target mask application belongs to the planned v0.8 multi-mesh authoring workflow and is not claimed in this preview.

Surface binding follows the collider GameObject's transform. It is not a skinned/deforming-mesh attachment system; vertex deformation tracking is outside this preview.

## Setup

1. Import this patch over the existing `Assets/SurfaceWeaver` package folder after backing up the project.
2. Open Window > SurfaceWeaver > Painter and select the primary SurfacePaintTarget.
3. Enable Path Authoring. The active target's MeshCollider is registered automatically.
4. To snap anchors onto another object, assign that object's MeshCollider to Surface Targets or select it and click **Add Selected MeshCollider**.
5. Choose **Surface Snap**, **Free Move**, or **Surface Offset** from Anchor Movement.
6. In Surface Snap mode, drag the selected anchor while the cursor is over a registered MeshCollider. The anchor binds to the collider under the cursor.
7. Move or rotate the bound GameObject to verify that the anchor and path follow its transform.
8. Save the path asset only after all referenced scenes are saved. The asset records each point's collider GlobalObjectId.

## Validation status

Source-level checks were performed for the patch archive and structural consistency. It has not been compiled or run inside the consuming Unity project in this environment. Run `Manual_Test_Plan_v0.8.0-preview.2.md` in Unity 6 before accepting it as validated. In particular, test surface transitions, transform-following, normal offsets, Free Move, legacy asset loading and the active-target Apply guard.
