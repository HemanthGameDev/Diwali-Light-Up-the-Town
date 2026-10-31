## Current build

SurfaceWeaver v0.6.5. This repair makes seam-aware projection surface-facing by default, uses a 3D spherical brush reach test, and clips rejected fragments so prior accepted mask writes are preserved.

# SurfaceWeaver

**Custom-mesh surface authoring for Unity URP.**

SurfaceWeaver is an Editor-first tool for authoring layered material masks on custom static meshes such as Blender-exported environments. The development build includes reusable surface profiles, custom mesh targets, Scene View picking and previews, GPU mask painting, brush presets, Undo/Redo, PNG persistence, UV diagnostics, seam-aware world-space UV raster projection, and spline/path painting.

## Current development version

`0.6.5` — cylindrical projection correctness: mandatory facing gate, spherical brush reach, rejected-fragment clipping, and imported face-normal orientation handling.

## Requirements

- Unity 6 project using the Universal Render Pipeline (URP).
- A GameObject with `MeshFilter`, `MeshRenderer`, and a non-convex `MeshCollider`.
- Readable mesh data (enable Read/Write in Model Import Settings for imported meshes).
- A valid UV0 layout with coordinates inside the normalized 0–1 range for the current mask workflow.
- Triangle submeshes for rasterization; non-triangle submeshes are skipped and reported.
- A saved `SurfacePaintProfile` asset with Mask Storage set to Packed RGBA Pages.
- The scene should be saved at least once before saving reusable path assets, because path assets resolve their target using a `GlobalObjectId`.

## Quick start

1. Import `Assets/SurfaceWeaver` into a backup Unity project.
2. Open **Window > SurfaceWeaver > Painter**.
3. Assign a target with a `MeshFilter` and `MeshRenderer`, and add `SurfacePaintTarget` if required.
4. Add/refresh a non-convex `MeshCollider`; it must reference the same mesh as the target `MeshFilter`.
5. Create or assign a `SurfacePaintProfile`.
6. Set Mask Storage to **Packed RGBA Pages** and add one or more enabled paint layers.
7. Create or load a black mask page for the selected layer.
8. Use **Seam-Aware Mesh Projection** for the world-space UV raster backend.
9. Hold the left mouse button over the target in Scene View to paint, erase, or smooth. Alt remains available for Scene View navigation.
10. Save the mask page as a PNG asset to persist changes. Undo/Redo history is session-only.
11. Use **Validate UV Channel** before painting to inspect UV range, degenerate triangles, overlaps, and geometric seams.
12. For paths, enable **Path Authoring**, click two or more points on the mesh, then Save Path Asset or Apply Spline.

## Seam-aware UV raster projection

The Editor builds a temporary raster mesh from the source mesh's triangle submeshes. Each source triangle is duplicated in UV0 space and carries its original local-space vertex position plus a per-triangle local face normal in extra vertex channels. The temporary mesh is drawn into the mask RenderTexture. The stamp shader reconstructs each pixel's world position, compares it with the world-space brush centre and normal, then updates only the selected RGBA channel.

This means a world-space stamp can affect matching surface areas represented by different UV charts at a geometric seam. It is a projection workflow, not automatic UV unwrapping or island stitching. The raster mesh is cached for the target and rebuilt when the source mesh reference changes.

### Projection constraints

- UV0 must be normalized to the 0–1 range. Out-of-range UVs are rejected by the raster painter rather than silently clipped.
- UV overlaps/stacked charts are ambiguous: separate surfaces that occupy the same mask pixels cannot store independent masks. Run UV overlap diagnostics and resolve unintended overlaps before painting.
- Non-triangle submeshes are skipped and surfaced as a warning.
- Seam-aware stamps use a world-space spherical reach test, an always-on face-facing threshold (default 0.2), and a configurable projection-depth slab. The facing threshold prevents the brush from painting the far side of cylinders and perpendicular cap faces. Lower/raise the threshold cautiously for hard-surface or curved meshes.
- Smooth is a local UV-pixel neighbourhood operation; it is not yet island-aware and should be used away from narrow chart gutters/seams.
- No automatic UV gutter dilation/padding or mip-bleed correction is included in v0.6.
- The temporary raster mesh is drawn for each stamp. Large/high-poly meshes may make authoring slower; profile performance before production use.

## Spline/path authoring

- Enable **Path Authoring** and click the selected target in Scene View to add anchors.
- The preview uses Catmull-Rom interpolation between anchors.
- Apply Spline projects intermediate samples back onto the target MeshCollider and paints the active layer as one Undo step.
- Seam-aware mode stamps across matching UV charts; legacy mode also applies but can leave gaps at UV seams.
- Brush radius controls approximate path half-width; the current path tool uses one constant radius and does not yet support a per-anchor width/pressure curve or editable path handles. Closed-loop paths are supported.
- A saved `SurfacePaintPathAsset` stores target-local anchor positions and normals, layer identifier, brush settings, and a deterministic random seed.
- The path asset stores the target's `GlobalObjectId` as text rather than a direct scene-component reference. The scene must have been saved at least once and must be loaded for automatic resolution. If it cannot resolve, the user may explicitly load points onto the currently selected target.
- Per-stamp random brush rotation/size variation is off by default for paths; enable it when using scatter-like effects.
- Applying a path modifies the active mask page. The path asset preserves the path recipe, not a separate non-destructive layer stack; use Undo or a duplicate mask page to revert/reapply safely.

## Mask and shader details

- Mask pages use linear RGBA32 data; R, G, B and A store four independent grayscale masks.
- Paint/erase updates only the active channel and preserves the other channels.
- GPU operations use ping-pong RenderTextures; the same texture is never sampled and written in one Blit operation.
- Undo/Redo uses session-only GPU snapshots with a configurable memory budget. Save the PNG mask to persist the current page.
- The current painter writes masks; full profile-driven multi-layer URP material generation/binding is still planned for v0.7. A compatible preview texture property is required for live target preview.

## UV diagnostics

The validator reports UV range, non-finite values, degenerate triangles, positive-area overlapping triangle pairs (not unique island pairs), and geometric seam edges. Overlap candidate-pair analysis is bounded; a partial report is not a clean bill of health. Diagnostics do not unwrap or modify the source mesh.

See `Documentation/UV_Diagnostics_v0.5.md`, `Documentation/UV_Seam_Projection_v0.6.md`, and the version-specific hotfix notes.

## Current limitations

- Static `MeshFilter` + `MeshRenderer` targets; skinned meshes are not supported.
- UV0 only for the current raster painting backend.
- Packed RGBA mask pages only; separate grayscale storage is not supported.
- One selected mesh target is painted at a time; multi-target batch workflows are pending.
- No automatic UV island padding/dilation or mip-gutter correction.
- Overlapping/stacked UVs remain inherently ambiguous for independent painting.
- Smooth remains UV-neighbourhood based and is not seam-aware.
- Path anchors can be added/removed, but there are no draggable Bezier handles, variable width/pressure profiles, or fully non-destructive re-editable paint layers yet.
- The current package is an Editor authoring tool. A dedicated runtime painting API and profile-driven URP multi-layer material are not included yet.
- The temporary UV raster mesh is processed across all source triangles on each stamp; large meshes need profiling and future optimization.

## Package layout

- `Runtime/` — target/profile/layer/brush/path data types; no UnityEditor dependency.
- `Editor/` — painter window, picker, GPU mask engine, temporary UV raster mesh, spline authoring, UV diagnostics, and persistence.
- `Editor/Shaders/` — hidden GPU mask-stamp shaders.
- `Documentation/` — architecture, roadmap, build notes and manual test plans.

## Validation status

This is a development build. Static source and package checks were performed in the packaging environment, but it has not been compiled or exercised in the target Unity Editor or graphics APIs. Import it into a backup project and complete `Documentation/Hotfix_Test_Plan_v0.6.1.md` before using production meshes.
