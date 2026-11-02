# SurfaceWeaver v0.7.11 — Manual Test Plan

## Before testing

- Back up the project and `Assets/SurfaceWeaver`.
- Install the v0.7.11 incremental patch.
- Confirm Unity finishes compiling with no new Console errors.
- Use a duplicate test profile/material and preserve a copy of the original mask PNG.

## Test 1 — Existing asset migration

1. Choose a mask PNG with Texture Importer > Mip Maps enabled.
2. Open **Window > SurfaceWeaver > Painter** and load that page.
3. Expand **Mip-map Bleeding Prevention**.
4. Confirm the page name and `Enabled — mip bleeding risk` status.
5. Click **Apply Bleed-Safe Import Settings to Current Mask Page** and accept.
6. Select the mask PNG in Project.

Expected importer values:
- Mip Maps: Off.
- sRGB (Color Texture): Off.
- Alpha Is Transparency: Off.
- Filter Mode: Bilinear.
- Wrap Mode: Clamp.
- Compression: None/Uncompressed.

Expected content: existing RGBA pixel data and painted appearance are preserved.

## Test 2 — Close/medium/far visual behavior

1. Create or use a mesh with two non-overlapping UV islands separated by a small gap.
2. Paint a Red-channel patch close to one island boundary.
3. Paint a Green-channel patch close to the neighboring island boundary.
4. Assign clearly different overlay albedos to Red and Green.
5. Save the mask page and verify Mip Maps is Off.
6. Inspect the mesh at close, medium and far camera distances.

Expected:
- No mask color contamination caused by lower mask mip levels.
- No changes to the existing visible mask at close range.
- Distant mask edges may show increased aliasing/shimmering due to LOD 0 sampling. Record its severity and device/camera distance.
- Base and overlay albedo textures continue to minify using their own normal mip chains.

## Test 3 — Save automatically applies safe settings

1. Load a mask page, paint one small stroke, then save.
2. Inspect the mask texture importer.

Expected: Mip Maps Off, Linear, Bilinear, Clamp, Uncompressed.

## Test 4 — New mask page defaults

1. Create a new mask page from the Painter.
2. Inspect its Texture Importer.

Expected: the same bleed-safe importer settings as Test 1.

## Test 5 — Undo/Redo and painting regression

1. Paint a stroke, Undo and Redo.
2. Confirm the live preview still shows the expected mask.
3. Save and reload.

Expected: painting, undo history and persistence remain functional. The mipmap importer policy must not modify the mask pixels.

## Test 6 — Optional shader LOD guard

1. Duplicate a mask page.
2. Manually enable Mip Maps on the duplicate.
3. Temporarily assign it to the test material's `_MaskPage0`.
4. Inspect mask behavior at distance.

Expected: the SurfaceWeaver layered shader still samples the mask at LOD 0. Restore the duplicate to bleed-safe settings after the test. Do not use standard-mip mask pages in production.

## Test 7 — Platform smoke test

Build a small scene with the test mesh for the target mobile device. Inspect at the same close/medium/far distances and record GPU memory, visual aliasing and frame time. This is a smoke test, not a completed mobile optimization benchmark.

## Report back

Record:
- Unity version and graphics API.
- Mask resolution.
- Mip Maps setting before/after migration.
- Whether adjacent Red/Green regions remain isolated at close/medium/far distances.
- Whether distant mask-edge aliasing is acceptable.
- Whether albedo mipmapping still looks normal.
- Any Console errors or warnings.
