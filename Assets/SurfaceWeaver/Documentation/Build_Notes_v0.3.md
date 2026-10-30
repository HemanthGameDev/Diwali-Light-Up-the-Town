# SurfaceWeaver 0.3.0 - Build Notes

## Included

1. `SurfaceGpuBrushEngine` creates two linear ARGB32 RenderTextures and initializes them from the selected mask Texture2D.
2. Every stamp blits from the read texture to the write texture, then swaps them.
3. `SurfaceMaskStamp.shader` preserves all unselected channels and changes only the active channel. Paint targets 1; erase targets 0.
4. Brush radius is specified in world units. `SurfaceUvMetric` derives world-space basis vectors per UV unit from the picked triangle, transformed vertex positions and UV0 coordinates.
5. Stroke interpolation uses world-space distance and the configured spacing value. A large UV jump breaks interpolation to reduce unintended strokes across UV seams.
6. Brush masks can optionally use a supplied grayscale texture sampled from its red channel.
7. Mask pages are created as linear, uncompressed PNG assets and are stored in `SurfacePaintProfile.MaskPages`.
8. Saving reads the current GPU page back once and writes a PNG. It does not read pixels back per stamp.
9. The working RenderTexture is bound to a configurable renderer MaterialPropertyBlock property for live preview. Default property name: `_PaintMask`.

## Known limitations / next work

- No per-stroke Undo/Redo yet.
- No automatic material/shader generation; the target shader must expose the configured preview texture property.
- Only UV0 and packed RGBA page storage are supported.
- UV overlap detection, island padding analysis and seam-aware multi-island painting are not implemented.
- Brush rotation is available; random rotation uses UnityEngine.Random and is not yet seed-persisted.
- Scatter/jitter, smoothing, fill, spline/path painting and multi-target workflows are not included.
- GPU operations and save/readback require a graphics device; validate on the intended Editor platform and graphics API.

## Important graphics implementation note

The engine uses ping-pong RenderTextures. Unity documents that `Graphics.Blit` with the same source and destination texture can produce undefined behavior.
