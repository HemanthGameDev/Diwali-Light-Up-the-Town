# SurfaceWeaver — Remaining Work After v0.5.0

This list is the current product backlog. “Implemented” means source code exists; it does not mean the feature has been validated in every Unity version or graphics API.

## Next engineering milestones

### 0.6 — UV robustness and path authoring
- Seam-aware cross-island brush propagation so a world-space stroke can continue onto the matching UV island at a mesh seam.
- Seam padding / mask dilation and configurable gutter to reduce texture-filtering and mip bleed.
- UV island grouping and island-level diagnostics, including mirrored/stacked UV identification.
- Better handling of discontinuous UV charts, mirrored mappings, non-uniform scale and high-curvature triangles.
- Spline/path painting with width, falloff, spacing, pressure/opacity profile, and deterministic re-editing.
- Multi-target workflow and batch mask export.

### 0.7 — Production material integration
- A supported URP multi-layer shader/material implementation driven by `SurfacePaintProfile`.
- Automatic mask page/channel binding and layer-order blending.
- Per-layer tiling, tint, opacity, normal map, metallic/smoothness (optional), and edge breakup.
- Configurable layer count / shader variants and mobile-oriented texture-sample budgets.
- Editor preview material generation and validation.
- Dedicated runtime profile/mask loading API. Runtime painting is a separate optional scope and is not promised for the first Asset Store release.
- Separate mask-texture storage mode or a clear decision to support packed RGBA pages only.

### 0.8 — Advanced authoring
- Object/vegetation scatter placement (distinct from the current grayscale scatter brush stamp).
- Smudge / directional blur tools if justified by user testing.
- Region/flood-fill operations that respect mesh connectivity and UV islands.
- Multi-object batch authoring, reusable surface recipes and project-level preset management.
- Stronger save/recovery flow, autosave, undo compression and history budgeting.

### 0.9 — Production quality and compatibility
- Unity EditMode automated tests for UV overlap, seam detection, mask-channel preservation, Undo/Redo and asset persistence.
- Graphics API testing for Direct3D 11/12, Vulkan and Metal where available.
- Unity/URP version compatibility matrix and package validation.
- Performance profiling on low/mid-tier desktop and Android authoring/runtime targets.
- Memory budget diagnostics and asynchronous GPU readback where supported.
- Edge-case testing for large meshes, submeshes, non-readable meshes, missing colliders, non-uniform transforms, mirrored UVs and UVs outside 0–1.

### 1.0 — Asset Store release
- Polished Editor UI, contextual help, first-run setup wizard and actionable error messages.
- Lite/Pro feature split implemented without compromising Lite core workflow.
- Demo project, sample meshes, neutral sample textures, brush presets, tutorial videos and complete documentation.
- Third-party/license audit and AI-content disclosure for included generated content.
- Asset Store package structure, screenshots, trailer, changelog, support policy, validator pass and final compatibility testing.

## Current v0.5.0 status

Implemented in source:
- Custom MeshFilter/MeshRenderer target workflow.
- Scene View mesh picking and UV0 diagnostics.
- GPU paint, erase and mask smooth operations.
- Packed RGBA mask pages, channel-isolated Fill/Clear, PNG persistence.
- Session Undo/Redo and starter alpha brushes.
- Positive-area UV triangle-overlap pair diagnostics and geometric seam-edge diagnostics.

Not yet implemented:
- Seam-aware cross-island paint propagation and seam padding.
- Profile-driven multi-layer URP surface rendering.
- Spline/path painting, object scatter, multi-target batch workflow and runtime painting API.
- Automated Unity test suite and Asset Store release validation.
