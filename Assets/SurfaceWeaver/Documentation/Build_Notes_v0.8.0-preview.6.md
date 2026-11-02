# SurfaceWeaver v0.8.0-preview.6 — Object & Vegetation Scatter

## Purpose

Adds a separate Editor-only scene tool for placing prefab objects and vegetation on registered `SurfacePaintTarget` meshes. This is distinct from the existing grayscale Scatter brush-alpha preset, which remains a texture/mask painting brush.

## Included

- New `Window > SurfaceWeaver > Object & Vegetation Scatter` Editor window.
- Reuses the existing `SurfacePaintTargetSet` asset from the Multi-Mesh Workspace.
- Resolves only targets in the assigned set and performs target-specific raycasts against each target's existing matching, enabled, non-convex `MeshCollider`.
- Creates reusable Editor-only `SurfaceScatterProfile` assets containing weighted prefab entries and scatter settings.
- Scene View Paint and Erase modes, with a world-space brush-radius preview.
- Random in-brush sampling on the tangent plane around the current hit, followed by a target-collider raycast so each candidate follows the sampled surface.
- Per-stamp density, stroke spacing and minimum instance separation.
- Weighted prefab selection, random uniform scale, random yaw, optional pitch/roll, optional surface-normal alignment and maximum-slope filtering.
- Surface offset and projection distance controls.
- Optional exclusion LayerMask and exclusion radius to keep placement away from rocks, props, roads or other user-designated colliders. Registered SurfacePaintTarget colliders are ignored by the exclusion check.
- Deterministic pseudo-random sequence from the profile seed and in-session stamp index.
- Ordinary prefab instances grouped beneath `SurfaceWeaver Scatter - <Profile>` scene roots and per-target child groups. Instances keep their prefab connection.
- Unity Undo for placement, erasing, parent creation and transform setup. Each brush stamp is collapsed into one Undo step.
- Erasing only affects the scatter group belonging to the currently hit target.
- No changes to the Painter, mask pages, spline data, runtime shader, `SurfacePaintTarget` component or Runtime assembly.

## Setup

1. Save the scenes containing the targets.
2. Open `Window > SurfaceWeaver > Object & Vegetation Scatter`.
3. Assign a reusable `SurfacePaintTargetSet` created by the Multi-Mesh Workspace.
4. Click `Create Profile…` and save the profile inside `Assets`.
5. Expand `Weighted Prefabs`, increase its array size and assign prefab assets. Set each entry's weight and scale range.
6. Configure brush radius, instances per stamp, stroke spacing and minimum instance distance.
7. Set maximum slope angle and surface-normal alignment to suit the asset type. Vegetation commonly uses a lower slope limit; rocks/debris can use a higher value.
8. Click `Enable Scene Placement` and drag with the left mouse button over registered target meshes in the Scene View. Hold Alt to navigate the Scene View.
9. Switch to `Erase Objects` to remove instances inside the brush radius on the currently hit target.
10. Save the scene. Use Undo/Redo to reverse or restore individual stamps.

## Target requirements

Each paintable scatter surface must be present in the assigned `SurfacePaintTargetSet` and have:

- An active `SurfacePaintTarget` with a valid `MeshFilter.sharedMesh`.
- An enabled `MeshRenderer`.
- An enabled, non-convex `MeshCollider` referencing the same mesh as the target's MeshFilter.
- A saved, loaded scene so the target's `GlobalObjectId` can be resolved.

The tool deliberately does not discover arbitrary scene colliders, add colliders, change transforms, or modify target registration. Add/remove targets through the existing Multi-Mesh Workspace.

## Performance and authoring notes

- The scatter window and profile type are in the Editor-only assembly. They do not add per-frame runtime scripts to placed prefab instances.
- Created prefab instances are regular scene GameObjects. Runtime cost depends on the prefabs and quantity placed, not on the scatter authoring window.
- Use mesh LODs, sensible texture sizes, instancing-compatible materials and culling for dense vegetation.
- Increase `minimumInstanceDistance` and reduce `instancesPerStamp` for expensive prefabs.
- Each stamp evaluates a bounded number of candidate points (24 attempts per requested instance, with a minimum of 24 attempts).
- The profile's random seed plus the editor-window stamp sequence drives deterministic selection for the same interaction sequence. This is not a persistent per-instance random seed system.
- The current version samples around a local tangent plane and projects onto the mesh hit by the cursor. It is suitable for terrain, roads, rocks and locally smooth surfaces; very sharply curved geometry may need smaller brush radii.

## Deliberate scope limits

- This preview places prefab instances; it does not generate combined meshes, GPU-instanced runtime batches, terrain trees, or vegetation detail prototypes.
- No runtime scatter execution or runtime profile loader is added.
- No spline-based scatter distribution is included yet.
- No per-layer mask density filtering, slope/height maps, arbitrary exclusion volumes or biome rules are included yet; this preview only supports collider-based exclusion through a LayerMask and radius.
- Existing grayscale Scatter brush behavior is unchanged.

## Validation status

Source and package checks only. Compile and run the manual test plan in the consuming Unity project before accepting this preview. Verify prefab links, Undo/Redo, scene persistence and Scene View input on the project's Unity 6 version.
