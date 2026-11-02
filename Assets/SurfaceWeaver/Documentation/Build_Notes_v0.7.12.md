# SurfaceWeaver v0.7.12 — Mobile Additional-Light Path

## Scope

This patch adds support for URP's `_ADDITIONAL_LIGHTS_VERTEX` shader variant in the layered URP material. The existing `_ADDITIONAL_LIGHTS` per-fragment path remains available and unchanged.

- **Per Pixel:** additional lights are evaluated in the fragment shader, including additional-light shadow attenuation when URP enables it.
- **Per Vertex:** additional-light diffuse contribution is evaluated in the vertex shader using URP's `VertexLighting` helper, then interpolated across the triangle. This avoids running the per-fragment additional-light loop. Additional-light shadows are not included in this vertex-lighting contribution.
- **Disabled:** URP does not render additional lights for this material; main-light and baked/probe lighting remain subject to the URP asset configuration.

The shader's default behavior remains controlled by the project's URP Asset. The patch does not force a project-wide quality setting and does not change mask sampling, layer count, albedo texture sampling, material properties, or painting data.

## Important limitations

- Per-vertex lighting can look less accurate on large triangles or low-tessellation meshes because the lighting result is interpolated from vertices.
- Per-vertex additional lights do not receive additional-light shadow attenuation in this shader path.
- This is a lower-cost rendering option, not a guarantee of a specific FPS increase. Measure on the actual target devices and scenes.
- No changes are made to the four-layer RGBA mask composition logic.
