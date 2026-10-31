# SurfaceWeaver Layered URP Shader Guide

## v0.7 mask layout

One packed RGBA page stores four grayscale masks: R, G, B and A. The profile can define up to four ordered overlay layers. Each layer selects one channel from page 0. The shader samples the packed mask once per pixel and reuses all four channel values.

## Blend order

The shader starts with Base Albedo multiplied by Base Tint. Each enabled overlay is then applied in profile order:

`current = lerp(current, overlayAlbedo × tint, saturate(maskChannel × opacity))`

Later layers are composited above earlier layers where masks overlap. This is an albedo-only blend.

## Texture mapping

- Mesh UV0: samples base/overlay albedo maps using mesh UV0 multiplied by each texture's tiling. Use for cylinders, props and general custom meshes.
- World Planar XZ: samples base/overlay albedo maps using world-position XZ multiplied by each texture's tiling. Use for mostly horizontal terrain. This is not triplanar and may stretch on vertical surfaces.

Mask always samples UV0 because the current painting/raster backend writes UV0 masks. `SurfacePaintTarget.UvChannel` should be 0.

## Binding

`SurfacePaintMaterialBinder` reads the target profile and writes profile properties into a dedicated material asset assigned to one MeshRenderer material slot. It does not use `Renderer.material` or `MaterialPropertyBlock`; this keeps the shader eligible for the SRP Batcher. Do not share a generated material between targets with different profiles.

Create/assign a shader material from the SurfacePaintTarget Inspector. At runtime, the binder applies the profile on enable. After editing profile values in the Editor, use **Apply Profile to Renderer Now**.

## Cost model and limits

The shader uses one base albedo sample, one packed RGBA mask sample and up to four overlay albedo samples (six texture samples maximum). Keep layer count low on mobile and profile target devices. v0.7 supports one packed mask page (four overlays). Additional pages, normal maps, metallic/smoothness maps, height blending, triplanar mapping and per-layer blend modes are future work.


## SRP Batcher note

The binder sets values on a dedicated material asset, not a MaterialPropertyBlock. Unity documents that MaterialPropertyBlock disables SRP Batcher compatibility for the renderer in URP. Keep one generated material asset per distinct profile; targets sharing the same profile may share the matching material.
