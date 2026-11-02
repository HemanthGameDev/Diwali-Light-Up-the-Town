# SurfaceWeaver Development Roadmap

This roadmap distinguishes source implementation from validated release status. A feature is not production-ready until it passes its consuming Unity project's acceptance tests.

## v0.7 — UV quality and rendering foundation
- UV island grouping and mirrored/stacked UV diagnostics.
- Automatic UV gutter padding, UV-constrained mask dilation and mip-map bleeding mitigation.
- Profile-driven multi-layer URP shader and RGBA mask-to-layer binding.
- Per-layer albedo, tint, tiling and opacity.
- Mobile shader options and an initial OnePlus CPH2467/Vulkan profiling baseline. Full device/API matrix remains for v0.9.

## v0.8 — Advanced authoring (feature-complete candidate)
- Editable spline anchors and Bézier tangent handles; single-active-target spline editing.
- Closed spline paths, per-anchor width and pressure profiles, and non-destructive live preview with Commit/Cancel.
- Batch export of persisted mask pages for multiple selected targets.
- Reusable Multi-Mesh Workspace target sets using scene GlobalObjectIds.
- Separate Multi-Mesh Painter for supported cross-target paint/erase/smooth operations with grouped stroke Undo/Redo.
- Editor-only object and vegetation scatter with weighted prefab selection, deterministic instance metadata, preview ghosts, and optional live update of compatible existing instances.
- Connected-region UV mask Flood Fill with tolerance, 4-/8-neighbour connectivity, channel isolation, Undo/Redo and UV0 seed-chart restriction.
- v0.8 consolidation is now a regression and compatibility pass; no further feature expansion should be accepted until the full matrix passes.

### v0.8 known boundaries
- Spline authoring remains single-active-target; cross-target spline paths are not implemented.
- Batch export reads persisted mask-page files, not unsaved GPU paint or uncommitted spline previews.
- Multi-Mesh Painter has explicit per-target collider, UV0, mask-page and material requirements; it does not perform cross-mesh spline authoring or geometry-aware interpolation through gaps.
- Scatter live updates require instance metadata. Adopting earlier instances is approximate for unusual prefab transforms. Weight only affects future prefab selection; density, spacing and brush radius affect future stamps.
- Flood Fill is UV0 chart-constrained texture-space connectivity, not geodesic mesh-surface flood fill. Stacked/overlapping UV charts sharing texels remain ambiguous; large masks require memory testing.

## v0.9 — Quality and optimization

### Preview.1 — Automated EditMode test foundation (prepared; Unity run pending)
- Added a dedicated Editor Test Framework assembly.
- Added tests for Flood Fill channel/tolerance/connectivity/region behavior and invalid inputs.
- Added tests for UV-island separation across a UV seam.
- Added tests for Multi-Mesh target-set add/remove/clear semantics.
- Added tests for spline path point width/pressure data and clamping.
- Remaining work: run tests in Unity, add integration tests for Undo/Redo and AssetDatabase persistence, then expand compatibility and performance suites.

- Unity EditMode automated tests for geometry/UV diagnostics, channel preservation, Undo/Redo and asset persistence.
- Graphics API compatibility: Direct3D 11/12, Vulkan and Metal where available; Android OpenGLES3 testing.
- Unity/URP compatibility matrix and clean-import validation.
- Large-mesh and long-stroke profiling.
- Memory budgeting, allocation reduction and asynchronous GPU readback where supported.
- Comprehensive edge-case testing: submeshes, non-readable meshes, missing/mismatched colliders, non-uniform/negative transforms, UV boundaries, thin charts and overlapping UVs.
- Repeated mobile performance captures and thermal testing on reference and lower-tier devices.

## v1.0 — Asset Store release candidate
- Professional Editor UI, contextual help, first-run setup and actionable diagnostics.
- Free Lite / paid Pro feature split.
- Demo project, sample meshes/materials/textures and brush/scatter presets.
- Complete user documentation and tutorials.
- Third-party/license audit and required generated-content disclosures.
- Asset Store package structure, screenshots, trailer, changelog, support policy and validator pass.

## Future backlog
- True geodesic world-surface Flood Fill.
- Independent editing for overlapping/stacked UV charts (requires a different mask representation or UV separation).
- Per-layer normal maps, metallic/smoothness and advanced blend modes.
- Reusable surface recipes and project-level preset management.
- Stronger autosave/recovery, undo compression and history budgeting.
