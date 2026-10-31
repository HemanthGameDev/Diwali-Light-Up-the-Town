# SurfaceWeaver v0.7.0 – Short manual test plan

Use a duplicate scene, duplicate profile and duplicate mask page. Keep the current v0.6.6 painting setup intact.

## 1. Compile and create material
1. Import the v0.7.0 patch.
2. Confirm no C# Console errors.
3. Select a target that has a profile and a MeshRenderer.
4. Click **Create / Assign SurfaceWeaver URP Material** in the target Inspector.
5. Save the generated material inside `Assets`.

Pass: shader is found; no pink material or missing shader.

## 2. Base surface and mapping
1. Assign the base albedo and tint in the profile.
2. Set Mapping Mode to Mesh UV0 and Tiling to (1,1); inspect the mesh.
3. Change tiling and confirm repetition changes.
4. Switch to World Planar XZ on the horizontal village ground only. Start with a frequency of 0.2–0.4.

Pass: base texture and tint are visible. World XZ stretching on vertical props is expected; use Mesh UV0 there.

## 3. First overlay (Dirt in R)
1. Create a duplicate profile with one Dirt overlay assigned to Red/page 0.
2. Assign the Dirt albedo, tint, tiling and opacity.
3. Confirm mask page 0 is assigned to the profile.
4. Click **Apply Profile to Renderer Now**.
5. Paint a patch in Red using the existing SurfaceWeaver Painter, save the mask and apply the profile again.

Pass: white R mask regions show dirt; black areas show base; grey values blend softly.

## 4. Four-channel blending
1. Add Sand in Green, Mud in Blue and Stone in Alpha.
2. Give each layer a clearly different test texture or tint.
3. Paint each channel separately and save mask page 0.
4. Apply the profile again.

Pass: each channel reveals its corresponding surface. Later profile layers composite above earlier layers where masks overlap.

## 5. Binder behaviour
1. Enter Play Mode.
2. Confirm the base and overlays remain visible.
3. Change a tint/tiling in the profile, return to Edit Mode and click Apply Profile to Renderer Now.
4. Enter Play Mode again.

Pass: binder reapplies on enable and does not create per-renderer material instances.

## 6. Expected warnings
- Five or more overlay layers: rejected with clear message (v0.7 maximum is four).
- `SeparateTextures`: rejected with clear message.
- Mask page 0 missing: base renders; overlays read black and binder reports a warning.
- Target UV Channel other than 0: inspector warns.
- Multiple material slots: binder affects only the selected slot; multi-submesh profile binding is deferred.

## 7. Mobile/performance check
Test 1, 2 and 4 overlay layers on an actual Android device. Capture GPU/frame timing and memory in Unity Profiler. Do not assume the shader is mobile-optimized until measured.
