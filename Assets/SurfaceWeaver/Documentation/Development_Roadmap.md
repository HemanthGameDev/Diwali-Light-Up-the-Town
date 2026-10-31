# SurfaceWeaver Development Roadmap

## 0.1 — Foundation
- Runtime target component and reusable ScriptableObject profiles.
- Surface layer and brush preset definitions.
- Initial UV0 mesh diagnostics.

## 0.2 — Scene View Preview
- Target-specific mesh picking.
- UV0 readout and world-space brush footprint.

## 0.3 — GPU Brush Engine
- Ping-pong RenderTexture mask stamping.
- Paint/erase operations for selected RGBA channels.
- World-space radius and continuous stroke interpolation.
- Brush alpha, opacity, hardness, spacing and rotation.
- PNG mask-page creation, saving and profile references.

## 0.4 — Editing Reliability
- Per-stroke GPU snapshot Undo/Redo with bounded memory.
- Safe cancel/rollback and selected-channel clear.
- Starter brush library and random rotation/scale jitter.

## 0.5 — Advanced Mask Authoring
- Positive-area UV triangle overlap diagnostics with bounded candidate-pair analysis.
- Geometric UV seam-edge diagnostics.
- GPU Smooth and channel-isolated Fill/Clear operations.

## 0.6 — Seam-Aware Projection and Path Authoring (current)
- Temporary UV raster mesh containing source local positions and per-triangle face normals.
- World-space brush projection across duplicated UV seam charts.
- Normal alignment gate and stable brush orientation across seam duplicates.
- UV0 normalized-domain validation and triangle-submesh processing.
- Catmull-Rom path preview and Scene View anchor authoring.
- MeshCollider projection of path samples and one-step GPU application.
- Reusable path assets with local-space points, brush settings and deterministic random seed.
- Path asset target resolution by serialized GlobalObjectId string.

## 0.7 — UV Output Quality and Production Rendering
- Island-aware UV gutter dilation/padding and mip-bleed analysis.
- UV island grouping and better mirrored/stacked-chart diagnostics.
- Adjustable normal cutoff and improved high-curvature handling.
- Profile-driven multi-layer URP shader/material integration.
- Automatic mask page/channel binding and per-layer albedo, tint, tiling, opacity.
- Optional normal/surface property maps and edge breakup.
- Mobile texture-sample budgets and runtime profile/mask consumption API.

## 0.8 — Advanced Authoring
- Path anchor handles, closed paths, variable width/pressure profiles and path editing.
- Multi-target authoring and batch mask export.
- Object/vegetation scatter placement (distinct from a scatter alpha brush).
- Region/flood-fill operations respecting mesh connectivity and UV charts.
- Reusable surface recipes and project-level preset management.
- Save/recovery improvements, autosave and Undo history optimization.

## 0.9 — Production Quality
- EditMode automated tests for diagnostics, seam projection, channel preservation, path assets and Undo/Redo.
- Graphics API testing (Direct3D 11/12, Vulkan and Metal where available).
- Unity/URP compatibility matrix.
- Profiling on low/mid-tier desktop and target Android devices.
- Large-mesh and many-stamp performance optimization.
- Edge-case testing for non-uniform/negative transforms, submeshes, UV boundaries, mirrored and overlapping UVs.

## 1.0 — Asset Store Release Candidate
- Polished Editor UI, first-run setup and actionable errors.
- Final Lite/Pro feature split.
- Demo project, sample meshes, profiles, brush presets and neutral textures.
- Documentation, tutorials, support policy and version matrix.
- Third-party/license audit and AI-content disclosure for included generated content.
- Asset Store package structure, screenshots, trailer, validator pass and final compatibility tests.
