# SurfaceWeaver Development Roadmap

This roadmap reflects the product plan confirmed by the developer.

## v0.7 — UV quality and rendering
- UV island grouping and mirrored/stacked UV diagnostics.
- Automatic UV gutter padding and mask dilation.
- Mip-map bleeding prevention.
- Complete profile-driven multi-layer URP shader.
- Automatic RGBA mask-to-layer binding.
- Per-layer textures, tint, tiling and opacity.
- Mobile shader optimization.

## v0.8 — Advanced authoring
- Editable spline anchors and Bézier tangent handles (preview.1 source; developer-reported working in Unity).
- Multi-mesh path authoring and per-anchor surface binding (deferred; current preview intentionally remains single-target).
- Closed paths (already supported by the v0.7.12 path authoring toggle; retain and regression-test).
- Reliable single-target anchor selection and editable Bézier tangents (current hotfix; validate in Unity).
- Variable path width and pressure (preview.3 source; developer-reported working in Unity).
- Live, non-destructive spline preview while editing width/pressure, with Commit/Cancel and one-step Undo (preview.3.1 source; Unity validation pending).
- Multi-mesh authoring and cross-target mask painting, plus batch export.
- Object and vegetation scatter placement.
- Flood Fill and additional surface tools.

## v0.9 — Quality and optimization
- Automated tests.
- Graphics API compatibility.
- Large-mesh profiling.
- Memory optimization.
- Comprehensive edge-case testing.

## v1.0 — Release candidate
- Professional Editor UI.
- Free Lite and paid Pro editions.
- Demo project and sample assets.
- Documentation and tutorials.
- Asset Store validation and release preparation.

## Future enhancement backlog
- Per-layer normal maps.
- Reusable surface recipes.
- Advanced save/recovery workflows.
- Additional robustness for non-uniform/negative transforms, submeshes, UV boundaries and high-curvature geometry.
