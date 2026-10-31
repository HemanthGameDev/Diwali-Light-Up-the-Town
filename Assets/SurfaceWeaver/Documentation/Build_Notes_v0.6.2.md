# SurfaceWeaver v0.6.2 — UV raster and preview hotfix

## Changes

1. **Raster/source UV consistency:** `SurfaceMeshMaskStamp.shader` now transforms the UV used for render-target placement and the UV used to sample the source mask in lockstep. Previously only the clip-space raster Y was flipped on top-origin render-target APIs; the fragment could therefore sample an opposite-row texel while writing a chart-shaped region. This is a plausible source of mirrored/rectangular corruption on affected APIs.
2. **Selected-channel preview:** the painter can display only the selected R/G/B/A mask channel in grayscale. Turn the toggle off to inspect the combined RGBA page. This makes it easier to distinguish actual selected-layer paint from data in other packed channels.

## Important

- This is a targeted correction to UV raster sampling and preview diagnostics. It is not a guarantee that all mesh seam cases are solved.
- Overlapping/stacked UV charts remain ambiguous.
- Smooth is still not seam-aware.
- Use a duplicate profile and a newly-created black mask page for the first validation. Do not overwrite production mask assets during testing.
- Unity C# compilation, shader compilation, and GPU output must be tested inside the target Unity 6 URP project on the user's graphics API.
