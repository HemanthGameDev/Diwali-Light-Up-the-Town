# SurfaceWeaver v0.6.1 — Hotfix Notes

## Why this hotfix exists

The first v0.6.0 field test exposed three confusing behaviours: spline Apply was blocked when Seam-Aware mode was disabled; a world-space projection could stamp more than one similarly oriented surface along the brush normal; and custom brush orientation could vary from stamp to stamp when a preset enabled random rotation. Rendering UV geometry directly to a RenderTexture also needs platform-aware Y orientation.

## Changes

- **Spline works in both modes.** Seam-Aware mode uses the UV-raster mesh. Legacy mode raycasts each spline sample and stamps using that triangle's UV/world metric. Legacy mode can still leave gaps at UV seams by design.
- **Projection depth.** A configurable `Projection Depth (x Radius)` value creates a finite slab around the picked surface. Reduce it to prevent nearby parallel surfaces from receiving duplicate marks; increase it slightly on more curved/uneven surfaces.
- **Stable brush orientation.** The texture tangent frame is now derived from the picked brush normal once per stamp, rather than changing with each rasterized triangle normal.
- **Spline variation is opt-in.** By default, the same spline uses fixed brush rotation and size. Enable `Randomize brush rotation / size per path stamp` only when scatter-like variation is desired.
- **Closed paths.** `Close Path` connects the final spline segment back to the first anchor. This is useful for square/rectangular clearings.
- **Projection snap validation.** Path samples reject distant or oppositely-facing collider hits rather than snapping to an unrelated nearby surface.
- **UV render-target orientation.** The UV raster shader flips its clip-space Y coordinate when `SystemInfo.graphicsUVStartsAtTop` indicates a top-origin texture convention. Unity documents this graphics-API difference for RenderTextures and UV-space rendering.

## Important interpretation of UV diagnostics

- `UV seam edges = 37` means the validator found 37 geometric edges whose adjacent faces use discontinuous UV coordinates. This is a warning/diagnostic, not a failure by itself.
- `Overlapping UV triangle pairs = 0` means no positive-area overlapping UV triangle pairs were found by the validator.
- A seam-aware stroke may appear in more than one location in the *raw UV mask* when the same physical surface is represented by separate UV charts. That is expected. The decisive check is whether the correct physical surface appears once in the rendered mesh, without unrelated surfaces being painted.

## Test sequence

1. Compile in Unity and check Console.
2. On a flat single-chart plane, use Soft Round and apply a simple 3-point L path in both seam-aware and legacy modes. The two modes should both apply; legacy mode is allowed to show seam gaps.
3. On the custom ground, use a 4-anchor square with `Close Path` enabled and per-stamp randomization disabled. Check that the final mask follows one square on the rendered mesh.
4. Toggle seam-aware on and off and compare the rendered result. Check the raw mask separately; UV chart orientation can make its 2D image look rotated compared with the world view.
5. If a nearby parallel surface is still painted, lower Projection Depth. If the stroke is clipped on sloped terrain, raise it gradually.
6. Choose Grunge/Scatter and enable per-stamp randomization only as a separate test.
7. Test Undo/Redo, then save/reload the mask and path asset.

## Limitations not fixed by this hotfix

- Truly overlapping/stacked UV charts cannot store independent paint in one mask page.
- Spline paths are target-local Catmull-Rom curves; they are not yet a full editable spline authoring system with handles or variable width.
- Legacy spline mode can show UV seam gaps.
- Smooth remains UV-neighbourhood based and is not seam-aware.
- UV dilation/padding, mip-bleed correction, multi-mesh painting and full profile-driven URP material generation remain future milestones.

## Validation status

Static source checks and package integrity checks were performed. Unity C# compilation, URP shader compilation, and GPU/graphics-API behaviour still require testing in the user's Unity Editor.
