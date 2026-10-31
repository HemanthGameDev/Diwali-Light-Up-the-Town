# SurfaceWeaver v0.6.4 — UV orientation and projection filtering hotfix

## Why this update exists

Testing on D3D11 reported vertically flipped/mirrored mask output and seam-aware projection failing on cylinder meshes. The v0.6.2+ vertex shader flipped both UV raster placement and the source-mask sampling coordinate. Those are not the same coordinate space on top-origin render-target APIs.

## Changes

- Restored the logical mesh UV0 for sampling the previous mask while applying platform Y correction only to clip-space raster placement.
- Updated the selected-channel preview shader to respect Unity's render-texture texel-size orientation convention (`_MainTex_TexelSize.y`) under `UNITY_UV_STARTS_AT_TOP`.
- Added optional surface-normal filtering to seam-aware projection. It is OFF by default; world-space projection depth and radial distance remain active. This avoids rejecting all triangles on imported meshes when face winding/custom normals disagree with the MeshCollider hit normal.
- Exposed a normal-dot threshold when the filter is enabled.
- Kept existing profile/mask formats compatible.

## Important limitations

This is a targeted hotfix, not a claim of fully validated seam painting. Overlapping/stacked UV charts remain ambiguous. The product still needs a reliable D3D11 test on a fresh Plane, a cylinder with a single longitudinal UV seam, a Smart UV cylinder, and the village ground.

## Install

Back up/commit the previous version, then replace only `Assets/SurfaceWeaver` with this package. Let Unity reimport. Create a new temporary mask page before testing. Do not overwrite production village mask data.
