# Manual Test Plan — v0.8.0-preview.8

- [ ] Unity compiles without errors or new warnings.
- [ ] Restrict to Seed UV Island is enabled by default.
- [ ] Fill clicked on one chart remains inside that UV chart across a split UV seam.
- [ ] A connected chart made of many triangles is filled as one region.
- [ ] 4-neighbour diagonal-only adjacency remains disconnected.
- [ ] 8-neighbour diagonal-only adjacency is connected.
- [ ] Tolerance and fill value work as expected.
- [ ] Only the selected RGBA channel changes; other three channels are byte-identical.
- [ ] Disabling UV-island restriction restores preview.7 texture-space behavior.
- [ ] A non-readable mesh produces a clear actionable error.
- [ ] A MeshCollider whose sharedMesh differs from MeshFilter.sharedMesh produces a clear error.
- [ ] Seed click maps to the expected UV0 texel on rotated/flipped UV triangles.
- [ ] Small/thin UV charts are tested at multiple mask resolutions.
- [ ] Undo restores the pre-fill mask and Redo restores the fill.
- [ ] Save/reload preserves the result.
- [ ] Escape cancels an armed fill without changing the mask.
- [ ] Existing paint, erase, smooth, spline preview, scatter, multi-mesh workspace and batch export remain functional.
