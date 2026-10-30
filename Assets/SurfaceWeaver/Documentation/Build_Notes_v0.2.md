# Build Notes – v0.2.0

## Delivered in this source update

1. `SurfaceScenePicker` converts Scene View GUI coordinates into a world ray and raycasts only against the configured target's MeshCollider.
2. `SurfaceSceneHit` captures the target, collider, hit position, hit normal, UV0, triangle index and distance.
3. `SurfaceBrushPreview` draws a world-space circular guide aligned to the hit surface normal.
4. `SurfaceWeaverWindow` provides preview enable/disable, world-space radius, preview colour, MeshCollider add/refresh, and live hit diagnostics.
5. Preview interaction is non-destructive: it does not paint or modify mesh/texture data.

## Unity setup

- Target object must have MeshFilter, MeshRenderer, SurfacePaintTarget and a non-convex MeshCollider.
- MeshCollider must reference the same mesh as the MeshFilter.
- Mesh must have readable data and a valid UV0 layout.
- Target UV channel must be 0 in this milestone.
- Imported mesh Read/Write must be enabled to ensure UV hit information is available in builds and consistent during authoring.

## Validation performed outside Unity

- New C# files reviewed for namespace and assembly placement.
- New asset metadata GUIDs generated uniquely.
- Assembly definition JSON parsed successfully.
- File-level delimiter balance checked.

## Not verified in this environment

This source package has not been imported or compiled by a Unity Editor. Please compile in the project's exact Unity 6 version and run the manual test plan before merging to the product branch.
