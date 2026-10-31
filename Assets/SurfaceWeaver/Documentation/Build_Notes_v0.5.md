# SurfaceWeaver v0.5.0 — Build Notes

## Included in this milestone

- Extended UV diagnostics for finite UVs, normalized range, degenerate UV triangles, positive-area UV triangle overlap pairs, and geometric UV seam edges.
- Bounded overlap analysis (up to 5,000,000 candidate pairs) to avoid unbounded Editor scans on large meshes.
- Scene View brush modes: Paint, Erase, and Smooth.
- GPU Smooth operation uses a compact 3x3 Gaussian-like neighborhood filter and edits only the selected RGBA mask channel.
- Full-channel Fill and Clear operations, each grouped as an undoable action and preserving the other packed mask channels.
- Updated status/help text and development roadmap.

## Compatibility and implementation notes

- The current painter remains Editor-only and paints UV0.
- UV overlap diagnostics report overlapping triangle pairs, not unique overlapping island pairs.
- Geometric seam detection compares mesh-local edge endpoint positions quantized to 1e-5 mesh units. It is diagnostic only; it does not propagate stamps across UV islands.
- Overlap analysis uses a sweep over triangle UV AABBs with a hard candidate-pair limit. A report marked Partial must not be interpreted as a clean overlap result.
- Smooth performs a 3x3 neighborhood filter on the selected mask channel. It is not a color-texture smudge operation.
- Fill/Clear change only the selected layer channel within the active mask page.

## Important limitations

- No seam-aware cross-island brush propagation or automatic seam dilation/padding yet.
- No spline/path painting, scatter placement of geometry, or multi-object batch painting.
- No completed profile-driven multi-layer URP surface shader in this milestone.
- Runtime painting API is not included; this package release is an Editor authoring tool with PNG mask assets.

## Verification status

Static source checks were performed for balanced braces, updated call signatures, assembly separation, metadata presence, and package contents. This package has not been compiled or run inside Unity in the authoring environment. Complete the manual test plan in the target Unity version and graphics API before treating it as validated.
