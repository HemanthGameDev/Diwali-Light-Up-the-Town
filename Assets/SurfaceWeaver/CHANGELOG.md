# Changelog

## 0.6.6 – Scale-independent UV raster validation

- Replaced the absolute local-space triangle-area rejection with scale-independent degeneracy validation so small local meshes with large Unity transforms are not rejected.

## 0.6.5 — cylindrical projection correctness
- Replaced the planar-only brush reach test with a true world-space spherical distance test plus the existing projection-depth slab.
- Made the surface-facing threshold mandatory in seam-aware mode (default 0.2), preventing stamps from reaching the far side of cylinders and perpendicular cap faces.
- Changed rejected/zero-contribution fragments to use HLSL `clip`, preserving earlier accepted writes when UV triangles overlap the same mask texels.
- Oriented per-triangle geometric normals against authored vertex normals when available, improving compatibility with imported meshes whose triangle winding differs from visible shading orientation.
- Added a guard against singular/zero-scale target transforms.
- Preserved the legacy UV painter, ping-pong mask buffers, RGBA channel writes, and session Undo/Redo.
- UV gutter padding/dilation is intentionally not included yet; it will be addressed after the cylinder projection gate passes on the user's Unity Editor/GPU.
- Static checks only; Unity compilation and GPU tests remain required.


## 0.6.4
- Fixed UV-raster/source-mask coordinate mismatch on top-origin graphics APIs.
- Added render-texture-aware selected-channel mask preview orientation.
- Made world-space normal filtering optional and disabled it by default for imported mesh robustness.
- Added D3D11-focused hotfix test plan.

# Changelog

## 0.6.3 — D3D11 shader declaration hotfix
- Fixed the D3D11 HLSL compile error in `SurfaceMeshMaskStamp.shader`: `_FlipRasterY` (and the related shader uniforms) are now declared before the vertex function that references them.
- No painting behaviour or serialized data format was intentionally changed by this compiler hotfix.
- Unity Editor compilation and graphics-device testing are still required after importing this package.

## 0.6.2 — UV raster sampling and mask preview hotfix
- Fixed UV-space raster coordinate mismatch: when the render-target Y convention requires a flip, the raster position and source-mask sampling UV are now flipped together. This prevents reading stale texels from the opposite row while writing a UV chart, which could create mirrored/rectangular artifacts.
- Added a selected-channel grayscale mask preview. Developers can inspect the active R/G/B/A layer without unrelated packed channels making the preview appear magenta or confusing. Combined RGBA preview remains available.
- This hotfix does not claim to solve every UV seam case. Seam painting must still be verified on duplicated UV charts with a clean mask page and a controlled cylinder test.

## 0.6.1 — hotfix
- Added platform-aware Y orientation when rasterizing UV-space triangles into RenderTextures, using Unity's graphics UV-origin convention.
- Added a projection-depth slab so seam-aware stamps do not affect similarly oriented surfaces far along the brush normal.
- Stabilized custom brush orientation per stamp by deriving the tangent frame from the picked brush normal rather than each rasterized triangle normal.
- Added a projection-depth multiplier control for uneven meshes.
- Enabled spline application in both seam-aware and legacy UV modes; legacy mode may still show gaps at chart boundaries.
- Added closed-loop spline paths for square/closed outlines.
- Added an opt-in per-stamp randomization toggle for splines; it is off by default so a path keeps a consistent brush orientation and size.
- Tightened spline sample projection to reject distant or oppositely-facing collider hits.


## 0.6.0
- Added UV-raster mesh projection for world-space paint/erase/smooth stamps across duplicated UV seam charts.
- Added per-triangle local face-normal data and a world-space normal gate to reduce projection onto nearby back-facing or sharply angled surfaces.
- Added non-uniform transform-safe world position/normal handling and stable brush orientation across duplicated seam vertices.
- Added Catmull-Rom spline/path authoring with mesh projection, one-stroke Undo, reusable path assets, brush settings, and deterministic random stamp seed.
- Path assets now persist a GlobalObjectId string rather than a direct scene-object reference, avoiding an invalid scene-object reference from a project asset.
- Added multi-submesh triangle gathering and clear warnings for skipped non-triangle submeshes.
- The seam-aware raster painter now rejects UV0 coordinates outside the normalized 0–1 mask domain.
- Added v0.6 build notes, manual tests, UV projection and spline workflow documentation.

## 0.5.0
- Added positive-area UV triangle overlap diagnostics with a bounded candidate-pair analysis.
- Added geometric UV seam-edge diagnostics.
- Added a GPU Smooth brush mode for the selected mask channel.
- Added full-channel Fill and Clear operations that preserve other RGBA channels and support session Undo.
- Updated diagnostics UI, build notes, and manual test plan.

## 0.4.0
- Added GPU snapshot-based per-stroke Undo/Redo.
- Added a starter brush library with Soft Round, Hard Round, Grunge, and Scatter presets.
- Added brush rotation and scale jitter controls.

## 0.3.0
- Added GPU-backed mask painting, erasing, RGBA channel preservation, and PNG persistence.

## 0.2.0
- Added Scene View target picking, UV hit information, and world-space brush preview.

## 0.1.0
- Added runtime target/profile/layer data and first-pass UV diagnostics.


## 0.7.0 – Profile-driven URP rendering

- Added a URP layered shader for a base surface plus up to four overlay layers stored in one packed RGBA mask page.
- Added UV0 and World Planar XZ albedo mapping.
- Added runtime binder that writes into a dedicated material asset without MaterialPropertyBlocks or `Renderer.material`.
- Added Editor workflow to create/assign the rendering material and apply profile settings.
- Added a shared UnityPerMaterial layout across UniversalForwardOnly, ShadowCaster and DepthOnly passes. The forward-only pass can render in URP Forward and Deferred renderer configurations.
- Added v0.7 rendering setup and manual testing documentation.
- Unity compilation, shader compilation, and Android profiling remain to be verified in the consuming project.
