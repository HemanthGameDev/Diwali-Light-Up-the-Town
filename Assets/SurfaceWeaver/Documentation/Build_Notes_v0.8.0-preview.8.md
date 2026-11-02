# SurfaceWeaver v0.8.0-preview.8 — UV-Island-Aware Flood Fill

## Included
- Adds **Restrict to Seed UV Island** to Connected-Region Flood Fill (enabled by default).
- Resolves the UV0 chart containing the picked MeshCollider triangle.
- Builds triangle-chart connectivity from shared geometric edges whose endpoint positions and UV0 coordinates match. Split UV seam edges are not joined when their UV coordinates differ.
- Rasterizes the seed chart into a per-texel allow-mask and constrains 4-/8-neighbour fill traversal to that chart.
- Retains value tolerance, fill value, selected RGBA channel isolation, GPU mask preview, Undo/Redo and PNG persistence.
- Keeps an opt-out for the previous whole-texture-space fill behavior.
- Adds an estimated staging-memory readout that includes the extra one-byte-per-texel region mask.

## Behavior and limitations
- This is UV0 chart/island constrained texture-space fill, not a geodesic world-surface flood fill. Pixel connectivity still occurs in mask texture space after the chart is rasterized.
- The active `SurfacePaintTarget` must have a readable `MeshFilter.sharedMesh`, UV0 data and a `MeshCollider` whose `sharedMesh` is the same mesh. The existing painting workflow already requires a matching collider.
- UVs are expected in the normalized 0–1 range. Triangle coverage is rasterized at mask pixel centers; very thin triangles at low mask resolutions can have few or no covered texels. The picked seed texel is included explicitly. Use a higher mask resolution for very small charts.
- Overlapping/stacked UV islands that occupy the same texels remain inherently ambiguous because they share one texture-space mask. Run UV overlap diagnostics and do not expect independent paint data for overlapping charts.
- The chart builder uses local vertex positions and UV0 to reconnect split vertex records across non-seam edges. Coincident duplicated geometry with identical UV0 may be considered connected; inspect unusual imported meshes.
- Disabling the option restores preview.7's unrestricted texture-space connected-region fill.
- The builder allocates an additional byte per mask texel, plus temporary triangle-edge connectivity data. Large meshes and 4096² masks should be tested carefully.

## Manual validation
1. Use a duplicate target, profile and mask page. Start at 1024² or 2048².
2. Create two UV islands with the same mask-channel value that touch or nearly touch in texture space but have a real UV seam between them.
3. Enable **Restrict to Seed UV Island**, arm Flood Fill and click the first island. Verify the second island remains unchanged.
4. Disable the option and repeat. Verify the legacy unrestricted fill can cross a texture-space pixel connection when one exists.
5. Verify 4-neighbour and 8-neighbour diagonal behavior inside a single chart.
6. Verify tolerance and fill-value controls.
7. Compare R/G/B/A before and after; only the selected channel may change.
8. Undo, Redo, save the PNG, reload it and compare.
9. Test an unreadable mesh and a mismatched MeshCollider sharedMesh; expect clear errors rather than a silent fill.
10. Test a thin/small UV island at 1024² and 2048² and document pixel-coverage differences.
11. Regression-test normal brush painting, smoothing, spline live preview, scatter, multi-mesh workspace and batch export.

## Validation status
Source and archive checks only. Unity compilation, triangle-index correspondence, chart separation on real split-seam meshes, pixel orientation, memory usage, Undo/Redo and Save/Reload must be verified in the consuming Unity 6 project.
