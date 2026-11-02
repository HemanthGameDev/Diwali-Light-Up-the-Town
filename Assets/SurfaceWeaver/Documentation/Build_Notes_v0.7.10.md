# SurfaceWeaver v0.7.10 — UV-Constrained Mask Dilation

## Scope

This incremental Editor patch adds a controlled dilation operation to the active packed RGBA mask page. It expands grayscale values in the selected layer's channel (Red, Green, Blue or Alpha) across texels covered by that mesh's UV0 islands.

The operation uses a two-pass separable maximum filter (horizontal then vertical). The neighborhood is a square/Chebyshev radius controlled from 1 to 32 pixels. Each scanline is split into contiguous runs of a single UV-island owner, and empty/ambiguous texels act as barriers. The filter therefore cannot propagate through empty atlas space or between distinct UV islands.

Only the selected channel is written back. Other RGBA channels, texels outside UV coverage, the mesh and source UV coordinates are preserved. The active GPU mask session records the processed page as one Undo step. The user must click **Save Mask PNG** to persist changes to the profile's mask asset.

## Validation and safety

The operation refuses to run when:

- The mesh is missing or unreadable.
- The active pixel buffer does not match the mask dimensions.
- The selected channel or radius is invalid.
- The target UV channel is not UV0.
- UV validation contains errors, is incomplete, or reports out-of-range/non-finite UVs or degenerate UV triangles.
- Separate connected UV islands overlap in UV space.
- No valid UV island/triangle coverage is available.
- No painted (non-zero) values exist in the selected channel inside the UV islands.
- Candidate triangle raster work exceeds the configured safety limit.

Ambiguous raster texels claimed by multiple island owners are treated as barriers. Mirrored triangle overlaps inside one connected UV island are allowed because those faces intentionally share the same UV mask region.

## Performance/memory

The processor builds a 16-bit UV-island owner map and a one-byte working channel buffer, plus scanline scratch buffers. At 4096 x 4096, the owner and channel buffers alone use approximately 48 MiB. This is additional to the GPU mask buffers, readback texture, and `Color32[]` readback array. Test at 512/1024/2048 first and profile 4096 separately.

The maximum-filter passes are linear in the number of pixels (plus UV triangle raster coverage), rather than repeating a full-neighborhood scan for every radius step.

## Relationship to UV Gutter Padding

- **UV Gutter Padding (v0.7.9):** copies boundary texels outward into unused atlas space. It does not modify texels covered by mesh UV triangles.
- **UV-Constrained Mask Dilation (v0.7.10):** expands the selected channel inward across texels covered by the mesh UV islands. It does not write outside UV coverage.

These operations are intentionally separate because one changes unused atlas pixels and the other changes the visible painted surface mask.

## Import and use

1. Back up `Assets/SurfaceWeaver`.
2. Import the v0.7.10 patch into the Unity project root and allow files to merge.
3. Wait for compilation and resolve all Console errors.
4. Open **Window > SurfaceWeaver > Painter**.
5. Select a layer and load its mask page.
6. Validate the mesh UV0 layout. Use separate, non-overlapping islands inside 0–1 for the first test.
7. Expand **UV-Constrained Mask Dilation**, choose a radius (start at 4 pixels), and confirm the displayed target channel.
8. Click **Apply Mask Dilation to Selected Channel** and confirm.
9. Inspect the mesh and mask preview. Verify only the selected channel changed and the expansion stopped at island boundaries.
10. Test Undo/Redo, then click **Save Mask PNG** and reload the page.

## Known limitations / future work

- The operation affects only the selected layer's channel, not all RGBA channels together.
- It is a grayscale maximum dilation; it does not blur, feather, threshold or shrink mask values.
- It does not modify albedo, normal or other material textures.
- It is UV0-only while the current painter and layered shader are UV0-based.
- Separate overlapping UV islands are rejected. Independent per-island masks for stacked UVs are outside the current packed-mask architecture.
- Mip-map bleeding prevention, mip-chain-aware guttering and mobile optimization remain outstanding v0.7 work.

## Verification status

The patch has source-level checks and offline algorithm tests only. It has not been compiled or run inside Unity by the assistant. Unity Editor compilation, active GPU mask readback/upload, Undo/Redo, PNG persistence, visual mesh behavior and resolution profiling require verification in the consuming project.
