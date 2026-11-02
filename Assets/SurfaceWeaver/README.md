# SurfaceWeaver v0.7.10

SurfaceWeaver is an Editor-focused custom-mesh surface painting tool for Unity 6 URP. The v0.6.6 authoring foundation supports UV0 mask painting, RGBA channels, brushes, Undo/Redo, UV diagnostics, and spline/path authoring. v0.7 adds the profile-driven URP layered rendering workflow that consumes the painted mask pages. v0.7.7–v0.7.8 add connected UV island grouping and stacked/mirrored/contained/partial overlap diagnostics. v0.7.9 adds island-aware UV gutter padding and v0.7.10 adds selected-channel UV-constrained mask dilation. v0.7.11 adds mip-map bleeding prevention for packed masks. v0.8.0-preview.1 adds draggable path anchors and editable Bézier tangent handles.

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
10. Use **Validate UV Channel** before painting to inspect UV range, degenerate triangles, triangle overlaps, geometric seams, UV island groups, and stacked/mirrored/contained/partial overlaps.
11. Load the relevant mask page and use **UV Gutter Padding > Apply UV Gutter Padding to Active Mask** to copy RGBA edge texels into unused UV atlas space. The operation is Undoable; save the page to persist it. It refuses separate-island overlaps or padding-front collisions.
12. Use **UV-Constrained Mask Dilation > Apply Mask Dilation to Selected Channel** to expand existing values in the selected layer's channel inside UV island coverage. It does not change other channels or write into empty atlas space. The operation is Undoable; save the page to persist it.
13. For paths, enable **Path Authoring**, click two or more points on the mesh, select an anchor to move it or edit its incoming/outgoing Bézier handles, then use Apply Spline or Save Path Asset. Reset controls restore automatic tangents.

## Quick start: render profile layers

1. Select the target and assign its `SurfacePaintProfile`.
2. Set the profile's Base Albedo, Base Tint, Base Tiling, and Texture Mapping Mode.
3. Add up to four overlay layers. The profile assigns the next available channel in page 0 (R, G, B, A).
4. Assign each layer's Albedo Texture, Tint, Tiling, and Opacity. Ensure mask page 0 is saved and assigned in the profile.
5. For curved props such as cylinders, choose **Mesh UV0** texture mapping. For mostly horizontal ground, choose **World Planar XZ**; this is planar mapping, not triplanar mapping, and it can stretch on vertical surfaces.
6. In the `SurfacePaintTarget` Inspector, click **Create / Assign SurfaceWeaver URP Material** and save the generated `.mat` inside `Assets`.
7. After replacing a saved mask page, use **Update Mask Only (Preserve All Material Settings)**. Use **Sync Profile Bindings (Preserve Material Tuning)** when profile texture references or layer count need refreshing. Use **Reset Material from Profile** only when you deliberately want to overwrite material tuning.
8. Enter Play Mode and verify the binder applies the assigned material slot.

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
- Overlapping/stacked UVs cannot store independent masks in the same texels. UV diagnostics report connected islands and stacked/mirrored/contained/partial overlaps; they do not automatically unwrap or move them. v0.7.9 adds non-destructive-in-session gutter padding into unused atlas texels. It refuses separate-island overlap cases and insufficient-gap collisions. v0.7.10 adds selected-channel mask dilation inside island boundaries; v0.7.11 adds mip-map bleeding prevention for packed masks; verify the documented minification trade-off on target devices.
- Smooth remains a local UV-neighbourhood operation and is not seam-aware.
- v0.8.0-preview.1 adds draggable anchors and independently editable incoming/outgoing Bézier handles. Per-anchor width/pressure curves remain deferred.
- Large/high-poly meshes and repeated stamps need profiling and future batching/spatial optimization.
- No Android performance claim is made until the shader is profiled on target devices.

## Package layout

- `Runtime/` — target/profile/layer/brush/path data types and runtime material binder; no `UnityEditor` dependency.
- `Editor/` — Painter window, picker, GPU mask engine, UV raster mesh, spline authoring, UV diagnostics, persistence, and custom inspectors.
- `Editor/Shaders/` — hidden GPU mask-stamp shaders.
- `Shaders/` — runtime layered URP surface shader.
- `Documentation/` — architecture, roadmap, build notes, setup guide, and manual test plans.

## Validation status

The v0.8.0-preview.1 spline-anchor/tangent source patch is not yet compiled or exercised in the consuming Unity project. Run `Documentation/Manual_Test_Plan_v0.8.0-preview.1.md` on a duplicate scene/profile/path asset before using it on production content. The first OnePlus CPH2467 Vulkan profiling baseline is recorded in `Documentation/Mobile_Performance_Baseline_OnePlus_CPH2467_2026-11-02.md`; the 1/2/4 overlay comparison, thermal testing, OpenGLES3 run, and second-device validation remain pending.

The v0.7.8 diagnostics were verified by the developer in Unity using a simple quad, overlapping/contained UV islands, and mirrored overlaps within a connected island. The developer verified the v0.7.9 gutter-padding operation, Undo/Redo, and save/reload on a 1024 x 1024 mask page. Collision protection and larger-resolution edge cases still need testing. The v0.7.10 mask-dilation implementation is source-prepared and must be compiled and manually verified in the consuming Unity 6 project. Complete `Documentation/Manual_Test_Plan_v0.7.10.md` on a duplicate mesh/profile before modifying production mask pages. An initial Android baseline was captured on OnePlus CPH2467 using Vulkan; complete the 1-, 2-, and 4-overlay comparison, thermal runs, and a second-device/API pass before making a broad Android performance claim.

See `Documentation/Build_Notes_v0.7.10.md`, `Documentation/Layered_URP_Shader_Guide.md`, and `Documentation/Manual_Test_Plan_v0.7.10.md`.
