# SurfaceWeaver

**Custom-mesh surface authoring for Unity URP.**

SurfaceWeaver is an Editor-first tool for authoring painted material masks on custom meshes (for example, Blender-exported environment meshes). The current development build includes target setup, initial UV diagnostics, Scene View picking, world-space brush preview, GPU paint/erase stamping and PNG mask persistence.

## Current version

`0.4.0` - GPU brush engine with bounded session Undo/Redo and a generated starter brush library.

## Requirements

- Unity 6 (6000.0 series) project.
- Universal Render Pipeline project for the intended rendering workflow.
- A GameObject with `MeshFilter` and `MeshRenderer`.
- UV0 on the mesh.
- Read/Write enabled on imported meshes while using MeshCollider UV picking.
- A saved `SurfacePaintProfile` asset.

## Quick start

1. Import `Assets/SurfaceWeaver` into the Unity project.
2. Open **Window > SurfaceWeaver > Painter**.
3. Assign a custom mesh target and add `SurfacePaintTarget` if required.
4. Add/refresh a non-convex `MeshCollider`.
5. Create or assign a `SurfacePaintProfile`.
6. Set Mask Storage to **Packed RGBA Pages**.
7. Add a painted surface layer. New layers are assigned four-per-page (R, G, B, A).
8. Create a black mask page for the selected layer.
9. Set **Preview Shader Property** to a texture property exposed by the target material (default `_PaintMask`).
10. Load the mask page, then paint or erase by holding the left mouse button over the target in Scene View.
11. Click **Save Mask PNG** to persist the mask as a project asset and reference it from the profile.

## GPU painting details

- Painting uses two RenderTextures and alternates read/write targets after each stamp.
- Do not use the same RenderTexture as both source and destination.
- Mask pages use linear RGBA32 data; each overlay layer reads/writes only its assigned channel.
- A world-space brush radius is mapped to an elliptical UV stamp using the hit triangle's UV0-to-world basis. This keeps the footprint approximately circular in world space under non-uniform UV scale and transformed meshes.
- Strokes are interpolated by world-space distance to reduce gaps between stamps. UV jumps are treated as likely seam crossings and break interpolation rather than drawing a long unintended line.
- Optional brush alpha samples the brush texture red channel. White paints; black does not paint.
- GPU readback occurs only when saving the PNG, not for every brush stamp.

## Current limitations

- UV0 only; additional UV channels are not supported by MeshCollider textureCoord in this build.
- Packed RGBA Pages are supported; Separate Textures is not supported by the current GPU painter.
- One selected mesh target is painted at a time.
- Live preview requires a texture property on the target shader; `_PaintMask` is the default. The full profile-driven multi-layer URP surface shader is a later milestone.
- Undo/Redo uses session-only full-page GPU snapshots and is cleared when the page is reloaded or the Painter window closes. Save Mask PNG to persist the page. Configure the history memory budget for the current resolution.
- UV overlap detection and padding/seam analysis are not implemented yet.
- Scatter-style alpha presets are included, but object/vegetation scattering is not implemented. Smoothing, fill, spline painting, overlap-aware seam continuation and multi-object batch painting are future work.

## Package layout

- `Runtime/` - profile, target, layer and brush data types.
- `Editor/` - painter window, mesh picking, UV metric, GPU session, mask asset persistence and diagnostics.
- `Editor/Shaders/` - hidden GPU brush-stamp shader used by the Editor.
- `Documentation/` - architecture, roadmap and manual testing instructions.

## Status

This is a development build. It has not yet been compiled or exercised in the consumer's Unity Editor. Test it in a backup project and check the Unity Console before using it on production assets.


## v0.4.0: Undo/Redo and brush library

- Per-stroke GPU RenderTexture snapshots for Undo/Redo.
- Adjustable session history memory budget.
- Escape cancels the active stroke.
- Starter brush generator (Soft Round, Hard Round, Grunge, Scatter).
- Custom preset scale jitter and random rotation.
- See `Documentation/Build_Notes_v0.4.md`, `Documentation/Manual_Test_Plan_v0.4.md` and `Documentation/Brush_Library.md`.

Undo/Redo history is session-only. Save Mask PNG to persist the current mask page.
