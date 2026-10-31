# SurfaceWeaver Development Roadmap

## v0.6.6 – Seam-aware projection foundation

- Surface-facing spherical projection and scale-independent UV raster triangle validation.
- Standard cylinder seam painting confirmed by the developer in Unity.
- Smart UV regression testing remains part of the quality pass.

## v0.7.0 – Profile-driven URP rendering (implementation created; Unity validation pending)

- Base albedo plus up to four ordered overlay layers.
- One packed RGBA mask page (R/G/B/A) drives the four overlay masks.
- Per-layer albedo, tint, tiling, opacity and channel selection.
- Mesh UV0 or World Planar XZ albedo mapping; paint masks remain on UV0.
- Dedicated material asset binding without MaterialPropertyBlocks, to preserve SRP Batcher eligibility.
- UniversalForwardOnly lighting (Forward and Deferred renderers), main light, SH ambient, optional additional lights, fog, ShadowCaster and DepthOnly passes. SSAO DepthNormalsOnly and Meta/lightmap passes remain deferred.
- Needs Unity C# compilation, URP shader compilation, visual verification, SRP Batcher check and Android profiling.

## v0.8 – UV and authoring quality

- UV gutter padding/dilation and mip-bleed handling.
- Better mirrored/stacked UV diagnostics and island margin warnings.
- Improved spline editing, variable width and efficient batched path application.
- Multi-target authoring and batch mask export.
- Scatter placement, region fill and reusable surface recipes.

## v0.9 – Compatibility and performance

- Automated EditMode tests for diagnostics, seam projection, channel preservation, path assets and Undo/Redo.
- Graphics API testing (Direct3D 11/12, Vulkan and Metal where available).
- Unity/URP compatibility matrix.
- Profiling on low/mid-tier desktop and target Android devices.
- Large-mesh and many-stamp optimization.
- Edge cases: non-uniform/negative transforms, submeshes, UV boundaries, mirrored/overlapping UVs.

## v1.0 – Asset Store release candidate

- Polished Editor UI and first-run setup.
- Final Lite/Pro feature split.
- Demo project, sample meshes, profiles, brush presets and sample textures.
- Documentation, tutorials, support policy and version matrix.
- Third-party/license audit and AI-content disclosure for any generated content included.
- Asset Store packaging, screenshots, trailer, validator pass and final compatibility tests.
