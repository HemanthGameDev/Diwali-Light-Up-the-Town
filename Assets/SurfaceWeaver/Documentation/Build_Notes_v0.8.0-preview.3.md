# SurfaceWeaver v0.8.0-preview.3 — Variable Path Width and Pressure Profiles

## Scope

This is an incremental update to the single-active-`SurfacePaintTarget` spline authoring workflow restored in `v0.8.0-preview.2-hotfix.3`. It does not add multi-target registration or cross-target mask painting.

## Included in source

- Per-anchor **Width Scale** (0.05–4.0) multiplies the existing spline brush radius.
- Per-anchor **Pressure** (0–1) is interpolated along the path.
- Independent **Pressure Affects Width** and **Pressure Affects Opacity** switches.
- Catmull–Rom scalar interpolation between anchor profile values, clamped to supported ranges.
- Scene View width rails display the effective half-width of the path brush.
- Reset selected and all width/pressure profiles.
- Profile-aware stamping in both seam-aware world projection and legacy UV projection.
- Conservative spacing based on each segment's minimum sampled effective radius, so one narrow segment does not force excessive stamping density across the entire path.
- Path assets persist per-anchor profiles and pressure switches.
- Older path assets receive neutral width and pressure values of 1 and default pressure behavior switches, preserving their previous constant-width/constant-opacity output.
- The previous `SurfacePaintPathAsset.Configure` signature remains available for source compatibility.
- Resetting automatic Bézier tangents preserves width/pressure values.

## Behavior notes

- Width Scale changes the radius of the brush stamp, not the spline centerline geometry.
- Pressure is authored per anchor in the Inspector; this preview does not sample hardware stylus pressure.
- Pressure can independently modulate width, opacity, both, or neither.
- When Pressure Affects Opacity is enabled, a pressure value of 0 produces no stamp at that sample.
- Width rails are an Editor visualization. No spline-editing component is added to player runtime.
- The single-target workflow is unchanged.

## Validation status

Source and patch structure checks are performed before distribution. Unity compilation, Scene View drawing, profile persistence, seam-aware projection, legacy UV projection, Undo behavior and actual mask quality must be validated in the user's Unity project using the manual test plan.
