# SurfaceWeaver v0.7.0 – Profile-driven URP rendering

## Included

- Added `HemanthGameDev/SurfaceWeaver/URP Layered Surface` opaque URP shader.
- Added runtime `SurfacePaintMaterialBinder`, writing profile values into a dedicated `.mat` asset through `sharedMaterials` (without using `Renderer.material` or `MaterialPropertyBlock`).
- Added Editor workflow to create/assign a material and apply the target profile.
- Added profile texture mapping mode: Mesh UV0 or World Planar XZ.
- Supports one base albedo plus up to four ordered overlay layers backed by one packed RGBA mask page.
- Each overlay has independent albedo, tint, tiling, opacity, and one selected RGBA channel.
- The shader samples the packed RGBA mask once per pixel and reuses its channels for the four overlays.
- The UniversalForwardOnly lighting pass supports URP Forward rendering and forward-only rendering in a Deferred renderer. Lighting includes the main light, SH ambient, optional additional lights and fog. Custom ShadowCaster and DepthOnly passes share the same UnityPerMaterial layout as the forward pass. A DepthNormalsOnly pass and Meta/lightmap pass are not included in this release. Baked lightmap sampling is not included in this first pass.

## SRP Batcher and material ownership

The binder writes directly to a dedicated material asset and does not use `MaterialPropertyBlock`, because Unity documents that MaterialPropertyBlocks are not compatible with the SRP Batcher in URP. The Editor creates a separate material asset by default. Do not reuse that material asset for a different profile; create another material for each distinct profile. This avoids renderer-specific overrides and runtime material instantiation.

## Deliberate v0.7 limits

- Maximum four overlay layers (R/G/B/A of one RGBA mask page), plus the base surface. This is a deliberate mobile-conscious limit, not a free/Pro gate.
- `PackedRgbaPages` is supported. `SeparateTextures` storage is rejected with a clear message.
- One profile is bound to one selected MeshRenderer material slot. Multi-material/submesh profile binding is not implemented.
- Albedo mapping is either UV0 or world XZ. World XZ is planar, not triplanar, and can stretch on vertical/curved surfaces. Choose Mesh UV0 for cylinders and props.
- Masks always sample UV0, matching the current painting/raster backend. Set `SurfacePaintTarget.UvChannel` to 0.
- Normal maps, metallic/smoothness maps, height blending, per-layer blend modes, triplanar mapping, multiple RGBA pages and runtime painting are deferred.
- No mobile performance claim is made before testing on target devices.

## Setup

1. Assign a `SurfacePaintProfile` to the `SurfacePaintTarget`.
2. Set Base Albedo, Base Tint, Base Tiling, and Texture Mapping Mode on the profile.
3. Add up to four overlay layers in the SurfaceWeaver Painter. These are assigned to R, G, B and A of mask page 0.
4. Assign each overlay Albedo, Tint, Tiling and Opacity. Ensure mask page 0 is saved and assigned in the profile.
5. On the target Inspector, click **Create / Assign SurfaceWeaver URP Material** and save the `.mat` inside `Assets`.
6. The tool adds/configures `SurfacePaintMaterialBinder`. It writes the profile into the dedicated material asset and reapplies it on enable at runtime.
7. After changing profile values or mask page assets, click **Apply Profile to Renderer Now**. The binder applies on enable at runtime.

## Texture importer guidance

- Albedo textures: sRGB on, Repeat wrapping for tiled textures, mipmaps enabled.
- Mask page: sRGB off, Clamp wrapping, bilinear filtering. Existing mask importer configuration is retained.

## Suggested first settings

- Village ground: World Planar XZ mapping. Use a world tiling frequency around 0.2–0.4 as a starting point, then tune it to the actual texture size.
- Cylinders/props: Mesh UV0 mapping. Tune each tiling against the mesh unwrap.
- Mobile: use only the layers needed; one base + four overlays is the shader maximum. Profile on an actual Android device.

## Validation status

Static source/package checks only. This package has not been compiled in Unity 6 or run through URP shader compilation/GPU validation in the user's project. Follow `Manual_Test_Plan_v0.7.0.md` before accepting it.
