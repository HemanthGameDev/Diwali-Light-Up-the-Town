# SurfaceWeaver — Remaining Work After v0.6.0

This backlog separates source features from validated release status. A feature is not considered production-ready until it is tested in the target Unity Editor and supported graphics APIs.

## Completed in source by v0.6.0
- Custom static MeshFilter/MeshRenderer targets with non-convex MeshCollider picking.
- Scene View brush preview and UV0 hit diagnostics.
- GPU packed RGBA mask painting, erase, smooth, fill, clear and PNG persistence.
- Session Undo/Redo using GPU snapshots.
- Starter/custom brush alpha presets, rotation, jitter and spacing.
- UV0 range, degenerate, overlap-pair and geometric seam diagnostics.
- Seam-aware world-space projection by rasterizing every source triangle in UV0 space with source local positions and per-triangle face normals.
- Catmull-Rom path preview, anchor authoring, MeshCollider sample projection and one-stroke application.
- Reusable path assets with local-space points, brush configuration, seed and GlobalObjectId string target resolution.

## v0.6 validation still required
- Unity compilation and shader compilation in the supported Unity 6/URP project.
- Seam painting on a mesh with a real split UV seam (e.g. a cylinder or sphere), including both sides of the seam.
- Correct render-texture orientation and UV-to-pixel placement across the supported graphics APIs.
- Normal filtering on adjacent, sharply angled and back-facing surfaces.
- Non-uniform scale/rotation brush footprint tests.
- RGBA channel isolation with a multi-channel mask page.
- Spline preview, application, Undo, deterministic brush jitter, save/load of path assets, and path target resolution after scene reload.
- Large-mesh and long-spline performance profiling.

## v0.7 — UV output quality and rendering
- Island-aware UV gutter dilation/padding, bleed prevention and mip validation.
- Better UV island grouping and mirrored/stacked chart detection.
- Adjustable normal cutoff and more robust high-curvature handling.
- Profile-driven multi-layer URP shader/material generator and binder.
- Automatic RGBA page/channel binding, per-layer albedo/tint/tiling/opacity and optional surface properties.
- Mobile texture-sample budget controls and runtime profile/mask consumption.

## v0.8 — Advanced authoring
- Editable path handles, closed paths, variable width and pressure/opacity curves.
- Multi-target authoring and batch mask export.
- Object/vegetation scatter placement.
- Region/flood-fill respecting mesh connectivity and UV islands.
- Reusable surface recipes and advanced save/recovery.

## v0.9 — Quality and compatibility
- Automated Unity Editor tests.
- Direct3D 11/12, Vulkan and Metal testing where available.
- Supported Unity/URP compatibility matrix.
- GPU/memory profiling and large-mesh optimization.
- Negative-scale, high-curvature, non-readable, boundary and overlapping-UV edge cases.

## v1.0 — Asset Store release
- Product polish, Lite/Pro split, samples, documentation, tutorials, release package, validator, screenshots, trailer, support policy, license audit and AI-content disclosure.

## Known v0.6 limitations
- UV0 only; UV coordinates must be in 0–1.
- Overlapping/stacked charts remain ambiguous.
- Smooth is still UV-neighbourhood based, not island-aware.
- No automatic gutter dilation/mip-bleed correction.
- One target at a time.
- Spline width is constant; no closed paths or editable tangent handles.
- Applying a path modifies the active mask page. The path asset stores the recipe, not a separate non-destructive layer.
- Full profile-driven URP multi-layer rendering is not implemented yet.
- Full UV raster geometry is drawn for each stamp; large meshes may be slow.
