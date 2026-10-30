# SurfaceWeaver – Starter Foundation

**Working product name:** SurfaceWeaver (provisional; perform a name/trademark search before public release).

SurfaceWeaver is a URP-first editor tool concept for authoring layered surfaces on static custom meshes. The planned workflow is designed around reusable surface profiles, UV diagnostics, editor painting and baked mask textures for game-ready materials.

> This download is an early development build, not a finished painter and not an Asset Store submission. Version 0.2.0 includes target/profile data, first-pass UV diagnostics, target-specific Scene View picking, UV0 readout and a world-space brush footprint. Painting, GPU stamping, mask persistence, Undo/Redo and the production URP shader are still future milestones.

## Install into a project

1. Back up the Unity project.
2. Copy the `Assets/SurfaceWeaver` folder into the project's `Assets` folder.
3. Let Unity compile the two assembly definitions.
4. Open **Window > SurfaceWeaver > Painter**.
5. Select a GameObject that has a `MeshFilter` and `MeshRenderer`.
6. Click **Add Target Component to Selected Mesh**.
7. Click **Create Profile**, assign the profile to the target, and add surface layers.
8. Click **Validate UV Channel** to run the first-pass UV checks.

## Current contents

- Runtime data types for paint operations, mask channels and brush settings.
- `SurfacePaintProfile` ScriptableObject for base surface data, overlay layers and mask-page references.
- `SurfacePaintTarget` component for static `MeshFilter` + `MeshRenderer` targets.
- `SurfaceBrushPreset` ScriptableObject for custom brush alpha and default settings.
- Editor window for target setup, profile editing and UV diagnostics.
- UV validation for missing/invalid UVs, out-of-range coordinates and degenerate UV triangles.
- Scene View target-specific ray picking using the assigned non-convex MeshCollider.
- World-space circular brush footprint aligned to the hit surface normal.
- Live UV0, world position, world normal and triangle-index readout.
- Editor action to add or refresh the target MeshCollider.

## Current limitations

- Scene View interaction is preview-only; no painting yet.
- Picking currently reads UV0 only through `RaycastHit.textureCoord`.
- No GPU stamping backend yet.
- No mask PNG import/export yet.
- No UV-island overlap detection or seam dilation yet.
- Static mesh targets only (`MeshFilter` + `MeshRenderer`); skinned meshes are not currently supported.
- The initial shader/material binding is not included yet.
- UV0 validation is a first-pass check; it does not prove that an arbitrary UV layout is paint-safe.

See `Documentation/Architecture.md`, `Documentation/Development_Roadmap.md` and `Documentation/Scene_Preview_and_UV_Picking.md` before extending the code. See `CHANGELOG.md` for version history.
