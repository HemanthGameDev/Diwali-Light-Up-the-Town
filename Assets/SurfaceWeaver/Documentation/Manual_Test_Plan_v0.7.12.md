# SurfaceWeaver v0.7.12 — Manual Test Plan

## 0. Install and compile

1. Back up `Assets/SurfaceWeaver`.
2. Merge this patch into the Unity project root.
3. Wait for shader reimport/compilation.
4. Check the Console for shader errors or pink materials.
5. Confirm the current layered material still renders its base texture and all painted RGBA layers.

## 1. Establish a baseline (Per Pixel)

1. Select the active Universal Render Pipeline Asset.
2. In its Lighting settings, set **Additional Lights** to **Per Pixel**.
3. Use a fixed test scene with one main directional light and two additional point or spot lights that affect the painted mesh.
4. Keep camera position, render resolution, material, mask, lights and quality settings fixed.
5. On the target device, capture a Development Build with the Unity Profiler connected. Record CPU frame time and GPU frame time where the graphics device exposes GPU timings. Also record the device, graphics API, resolution and URP asset used.
6. Confirm the visual result matches the prior shader behavior: additional lights contribute per pixel and their supported shadows still work.

## 2. Test Per Vertex additional lights

1. Change only the URP Asset **Additional Lights** setting to **Per Vertex**.
2. Rebuild and run the same scene on the same device, at the same resolution and camera position.
3. Confirm additional point/spot lights still contribute to the mesh.
4. Inspect large triangles and low-poly surfaces for visibly coarse/interpolated light gradients.
5. Confirm no compile errors, missing-light artifacts, pink materials, or changes to the RGBA painted layers.
6. Capture CPU/GPU timing again using the same profiler method.
7. Repeat enough captures to avoid relying on one noisy frame; compare a representative steady-state range rather than a single best frame.

Expected: the per-fragment additional-light loop is replaced by vertex-evaluated additional-light contribution for the `_ADDITIONAL_LIGHTS_VERTEX` variant. A performance improvement is possible but must be measured; do not assume a gain if the scene is texture-fill-rate or bandwidth bound.

## 3. Test Disabled additional lights

1. Set URP Asset **Additional Lights** to **Disabled**.
2. Rebuild and run the same scene.
3. Confirm point/spot additional lights no longer contribute, while the main directional and supported baked/probe lighting remain.
4. Confirm the layered material remains otherwise unchanged.

## 4. Graphics API coverage

Where supported by the device, make separate builds for Vulkan and OpenGLES3. Do not assume that a successful Editor shader compile proves both mobile backends compile and render correctly.

## 5. Regression checks

- Base albedo and tint remain correct.
- All four RGBA mask channels still reveal their assigned layer textures.
- Layer opacity, channel, page, enabled state, tint and tiling remain unchanged.
- Mesh UV0 and World Planar XZ mapping still work.
- Mask textures remain mipmap-disabled and sampled at LOD 0.
- Gutter padding and mask dilation assets/data remain unchanged.
- ShadowCaster and DepthOnly passes still operate.

## Results to report

Record: Unity version, URP version, device model/GPU, graphics API, render resolution, URP Additional Lights mode, CPU frame time, GPU frame time (if available), visual differences, and Console/shader errors.
