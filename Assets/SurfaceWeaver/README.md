# SurfaceWeaver v0.7.0

SurfaceWeaver is an Editor-focused custom-mesh surface painting tool for Unity 6 URP. The v0.6.6 authoring foundation supports UV0 mask painting, RGBA channels, brushes, Undo/Redo, UV diagnostics, and spline/path authoring. v0.7.0 adds a first profile-driven URP layered rendering workflow that consumes the painted mask pages.

## v0.7 rendering at a glance

- One base albedo surface plus up to four ordered overlay surfaces.
- One packed linear RGBA mask page: R, G, B, and A each drive one overlay.
- Per-surface albedo texture, tint, tiling, and overlay opacity.
- Albedo mapping: Mesh UV0 or World Planar XZ.
- Mask lookup always uses UV0 to match the existing painter.
- A dedicated material asset is generated and configured from the `SurfacePaintTarget` Inspector.
- One selected `MeshRenderer` material slot per target.

This is an initial rendering implementation, not a claim of completed mobile optimization. Profile the shader on target Android devices before production use.

## Requirements

- Unity 6 project using the Universal Render Pipeline (URP).
- A GameObject with `MeshFilter`, `MeshRenderer`, `SurfacePaintTarget`, and a non-convex `MeshCollider` for Editor painting.
- For imported meshes, enable Read/Write in Model Import Settings when required by the painting backend.
- A valid UV0 layout within the normalized 0–1 range for mask painting.
- Triangle submeshes for UV raster painting; non-triangle submeshes are skipped and reported.
- A `SurfacePaintProfile` with Mask Storage set to `PackedRgbaPages`.

## Quick start: paint masks

1. Import `Assets/SurfaceWeaver` into a backup Unity project.
2. Open **Window > SurfaceWeaver > Painter**.
3. Assign a target with a `MeshFilter` and `MeshRenderer`, and add `SurfacePaintTarget` if required.
4. Add/refresh a non-convex `MeshCollider` that references the same mesh as the target `MeshFilter`.
5. Create or assign a `SurfacePaintProfile` and select **Packed RGBA Pages**.
6. Add overlay layers and create/load a black mask page for the selected layer.
7. Use Seam-Aware Mesh Projection for world-space UV-raster painting.
8. Hold the left mouse button over the target in Scene View to paint, erase, or smooth. Alt remains available for Scene View navigation.
9. Save the mask page as a PNG asset. Undo/Redo history is session-only.
10. Use **Validate UV Channel** before painting to inspect UV range, degenerate triangles, overlaps, and geometric seams.
11. For paths, enable **Path Authoring**, click two or more points on the mesh, then use Apply Spline or Save Path Asset.

## Quick start: render profile layers

1. Select the target and assign its `SurfacePaintProfile`.
2. Set the profile's Base Albedo, Base Tint, Base Tiling, and Texture Mapping Mode.
3. Add up to four overlay layers. The profile assigns the next available channel in page 0 (R, G, B, A).
4. Assign each layer's Albedo Texture, Tint, Tiling, and Opacity. Ensure mask page 0 is saved and assigned in the profile.
5. For curved props such as cylinders, choose **Mesh UV0** texture mapping. For mostly horizontal ground, choose **World Planar XZ**; this is planar mapping, not triplanar mapping, and it can stretch on vertical surfaces.
6. In the `SurfacePaintTarget` Inspector, click **Create / Assign SurfaceWeaver URP Material** and save the generated `.mat` inside `Assets`.
7. After changing profile properties or replacing a mask page asset, click **Apply Profile to Renderer Now**.
8. Enter Play Mode and verify the binder applies the profile to the assigned material slot.

## Layer and mask model

- Base surface is always visible; its albedo is multiplied by Base Tint.
- Each enabled overlay samples its albedo texture and tint, reads its assigned channel from the packed RGBA mask, multiplies the mask by the layer opacity, then blends over the current result.
- Layers are composited in profile order; later layers appear above earlier layers where their masks overlap.
- The shader samples the packed RGBA mask once per pixel and reuses its channels for all overlay layers.
- Masks are linear RGBA32 data. Import mask textures with sRGB disabled, Clamp wrapping, and Bilinear filtering unless the mask asset utility sets these automatically.
- Albedo textures should use sRGB and Repeat wrapping when tiled.

## Rendering implementation notes

The shader name is `HemanthGameDev/SurfaceWeaver/URP Layered Surface`. It has a `UniversalForwardOnly` lighting pass plus custom `ShadowCaster` and `DepthOnly` passes. It supports main-light direct diffuse, SH ambient, optional additional lights, fog, shadow reception, and shadow casting. It uses a consistent `UnityPerMaterial` constant buffer layout and does not use `MaterialPropertyBlock`; Unity documents that MaterialPropertyBlocks make a renderer incompatible with the SRP Batcher in URP. The binder writes profile values into a dedicated material asset instead. Keep a different generated material for each distinct profile.

## Deliberate v0.7 limitations

- One packed RGBA mask page and up to four overlay layers, plus the base surface.
- `SeparateTextures` mask storage is not supported by the v0.7 shader/binder.
- One profile and one selected `MeshRenderer` material slot per binder; multi-submesh profile binding is not implemented.
- Masks always sample UV0. Set `SurfacePaintTarget.UvChannel` to 0.
- World Planar XZ is planar, not triplanar, and can stretch on vertical or curved surfaces.
- No normal maps, metallic/smoothness maps, height blending, per-layer blend modes, additional mask pages, or runtime painting API.
- No `DepthNormalsOnly` pass for URP SSAO and no Meta pass for baked lightmap generation/sampling.
- Skinned meshes are not supported; the current painter targets static `MeshFilter` + `MeshRenderer` objects.
- Overlapping/stacked UVs cannot store independent masks in the same texels. No automatic UV island padding/dilation or mip-gutter correction is included yet.
- Smooth remains a local UV-neighbourhood operation and is not seam-aware.
- Spline width is constant; draggable handles and per-anchor width/pressure curves are deferred.
- Large/high-poly meshes and repeated stamps need profiling and future batching/spatial optimization.
- No Android performance claim is made until the shader is profiled on target devices.

## Package layout

- `Runtime/` — target/profile/layer/brush/path data types and runtime material binder; no `UnityEditor` dependency.
- `Editor/` — Painter window, picker, GPU mask engine, UV raster mesh, spline authoring, UV diagnostics, persistence, and custom inspectors.
- `Editor/Shaders/` — hidden GPU mask-stamp shaders.
- `Shaders/` — runtime layered URP surface shader.
- `Documentation/` — architecture, roadmap, build notes, setup guide, and manual test plans.

## Validation status

v0.7.0 source is prepared, but it has not been compiled inside Unity 6 or passed URP shader compilation in the consuming project. Complete `Documentation/Manual_Test_Plan_v0.7.0.md` in a duplicate scene/profile before using the rendering workflow on production assets. Then profile 1, 2, and 4 overlay configurations on actual Android hardware.

See `Documentation/Build_Notes_v0.7.0.md`, `Documentation/Layered_URP_Shader_Guide.md`, and `Documentation/Manual_Test_Plan_v0.7.0.md`.
