# SurfaceWeaver v0.7.9 — UV Gutter Padding

## Scope

This incremental Editor patch adds island-aware padding to the active packed RGBA mask page. It copies the complete `Color32` value (R, G, B, A) from the nearest rasterized UV-island boundary texel outward into unoccupied atlas pixels.

It does not modify the mesh, source UV coordinates, or any pixel whose texel center is covered by a valid mesh UV triangle. The operation is performed on the active GPU mask session and recorded as one Undo history step. The user must click **Save Mask PNG** to persist it to the profile's mask-page asset.

## Safety behavior

The operation refuses to run when:

- The selected mesh is missing or unreadable.
- The selected UV channel is missing, non-finite, out of the 0–1 range, or contains degenerate UV triangles.
- UV island or triangle overlap analysis is incomplete.
- Separate connected UV islands have positive-area overlap. Same-island mirrored overlaps are permitted because the mask texels are intentionally shared.
- Two different island padding fronts reach the same unused texel at the same distance. The entire operation is rejected rather than choosing one island arbitrarily.
- The island count, candidate raster coverage, boundary queue, or padding width exceeds the configured safety limits.

The gutter width is limited to 1–32 texels and uses an 8-neighbor (square/Chebyshev) expansion. Texture content is copied as a whole RGBA pixel, so all four mask channels are preserved together.

## Import and use

1. Back up the project's `Assets/SurfaceWeaver` folder.
2. Extract the v0.7.9 patch into the Unity project root and allow files to merge.
3. Open the project in Unity 6 and wait for compilation. Resolve all Console errors before testing.
4. Open **Window > SurfaceWeaver > Painter** and select the intended target.
5. Select a layer on the mask page to process, then load that page.
6. Validate the mesh UV channel. For the first success test, use separate non-overlapping islands inside 0–1 with sufficient spacing.
7. Expand **UV Gutter Padding**, choose a small width (start at 4 or 8 pixels), and click **Apply UV Gutter Padding to Active Mask**.
8. Inspect the mask page and the live mesh. Pixels on the mesh surface should remain unchanged; only unused atlas texels outside the UV coverage should receive copied edge values.
9. Test Undo and Redo. Save the mask PNG only after the result is correct.

## Limitations / future work

- This operation pads the mask texture only. It does not pad albedo, normal, or other material texture atlases.
- This operation does not perform surface-mask dilation inside UV islands. Dilation will be a separate explicit action because it changes values on the mesh surface.
- Mip-map generation remains Unity TextureImporter behavior after saving. Mip-level bleed prevention and mip-chain-aware padding are not complete in this patch.
- It does not repair UVs, move islands, or support independent masks for overlapping UV regions.
- The algorithm is CPU-side Editor processing with bounded work queues. Profile it on production-scale 2K/4K mask pages before making performance claims.

## Verification status

The algorithm has source-level and offline geometry/ownership checks only. It has not been compiled or run inside Unity by the assistant. Unity compilation, GPU upload, undo/redo behavior, saved PNG import, and visual seam checks must be verified in the consuming project.
