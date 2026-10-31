# SurfaceWeaver

**Custom-mesh surface authoring for Unity URP.**

SurfaceWeaver is an Editor-first tool for authoring material masks on custom meshes such as Blender-exported environments. The development build includes reusable surface profiles, custom mesh targets, Scene View UV picking, world-space brush preview, GPU-backed mask painting, brush presets, stroke Undo/Redo, PNG persistence, and extended UV diagnostics.

## Current development version

`0.5.0` — advanced mask authoring with UV overlap/seam diagnostics, a GPU mask Smooth operation, and selected-channel Fill/Clear.

## Requirements

- Unity 6 project using the Universal Render Pipeline.
- A GameObject with `MeshFilter` and `MeshRenderer`.
- Readable mesh data and UV0 for the current painting workflow.
- A non-convex `MeshCollider` for Scene View UV picking.
- A saved `SurfacePaintProfile` asset.

## Quick start

1. Import `Assets/SurfaceWeaver` into a backup Unity project.
2. Open **Window > SurfaceWeaver > Painter**.
3. Assign a custom mesh target and add `SurfacePaintTarget` if required.
4. Add/refresh a non-convex `MeshCollider`.
5. Create or assign a `SurfacePaintProfile`.
6. Set Mask Storage to **Packed RGBA Pages**.
7. Add a painted surface layer. New layers are assigned four-per-page (R, G, B, A).
8. Create a black mask page for the selected layer.
9. Set **Preview Shader Property** to a texture property exposed by the target material (default `_PaintMask`).
10. Load the mask page, then paint, erase, or smooth by holding the left mouse button over the target in Scene View.
11. Use Fill/Clear for full-channel operations. Save Mask PNG to persist the page as a project asset.
12. Use **Validate UV Channel** to inspect UV range, degenerate triangles, positive-area overlaps, and geometric UV seams.

## GPU painting details

- Painting uses two RenderTextures and alternates read/write targets after every operation.
- Do not use the same RenderTexture as both source and destination.
- Mask pages use linear RGBA32 data; each overlay layer reads/writes only its assigned channel.
- A world-space brush radius is mapped to a UV stamp using the hit triangle's UV0-to-world basis.
- Strokes are interpolated by world-space distance. Large UV jumps break interpolation to avoid drawing a long unintended line across a seam.
- Optional brush alpha samples the brush texture red channel. White paints; black contributes no stamp opacity.
- Paint moves the selected channel towards white; Erase moves it towards black.
- Smooth applies a 3x3 Gaussian-like neighborhood filter to the selected mask channel. It smooths a mask; it does not smear albedo pixels.
- Fill/Clear modify only the selected packed RGBA channel.
- GPU readback occurs when saving a PNG, not for every brush stamp.
- Undo/Redo is session-only GPU snapshot history. Save Mask PNG to persist the current page.

## UV diagnostics

- Out-of-range and non-finite UV values.
- Degenerate UV triangles.
- Positive-area overlapping UV triangle pairs (not unique island pairs).
- Geometric seam edges detected by comparing coincident mesh-local edge positions and their UV endpoint mappings.
- Overlap analysis is bounded at five million candidate-pair tests. A Partial result is not a clean bill of health.

See `Documentation/UV_Diagnostics_v0.5.md` for method and limitations.

## Current limitations

- UV0 only in the current Scene View painting workflow.
- Packed RGBA Pages are supported; Separate Textures is not supported by the current GPU painter.
- One selected mesh target is painted at a time.
- Live preview requires a compatible texture property on the target shader; full profile-driven multi-layer URP rendering is still pending.
- Geometric seam detection is diagnostic only. Stamps are not yet propagated across UV islands, and no automatic island dilation/padding or mip-bleed analysis is included.
- Smooth operates on the mask channel and is not a texture smudge tool.
- No spline/path painting, prefab scatter placement, or multi-object batch authoring yet.
- The current package is an Editor authoring tool. A dedicated runtime painting API is not included.

## Package layout

- `Runtime/` — profile, target, layer and brush data types.
- `Editor/` — painter window, mesh picking, UV metric, GPU session, mask asset persistence and diagnostics.
- `Editor/Shaders/` — hidden GPU brush-stamp shader used by the Editor.
- `Documentation/` — architecture, roadmap and manual test plans.

## Validation status

This is a development build. Source-level checks were performed in the packaging environment, but it has not been compiled or exercised inside the target Unity Editor/graphics API. Import it into a backup project, run `Documentation/Manual_Test_Plan_v0.5.md`, and check the Unity Console before using production meshes.

## v0.5.0 changes

- UV overlap detection using positive-area triangle-pair checks.
- Geometric UV seam-edge diagnostics.
- GPU Smooth mode for the selected mask channel.
- Selected-channel Fill and Clear operations with session Undo.
- New guides: `Build_Notes_v0.5.md`, `UV_Diagnostics_v0.5.md`, `Mask_Operations_v0.5.md`, and `Manual_Test_Plan_v0.5.md`.
