# SurfaceWeaver Development Roadmap

This roadmap reflects the product plan confirmed by the developer. A source implementation is not considered complete until it has passed the consuming Unity project's manual acceptance tests.

## v0.7 — UV quality and rendering
- UV island grouping and mirrored/stacked UV diagnostics.
- Automatic UV gutter padding and mask dilation.
- Mip-map bleeding prevention.
- Profile-driven multi-layer URP shader and automatic RGBA mask-to-layer binding.
- Per-layer textures, tint, tiling and opacity.
- Mobile shader options; real-device profiling baseline established. Full graphics API and device matrix remains for v0.9.

## v0.8 — Advanced authoring
- Editable spline anchors and Bézier tangent handles (preview.1; developer-confirmed working).
- Reliable single-target anchor selection and editing (hotfix.3; developer-confirmed working).
- Closed paths (existing behavior retained and regression-tested during spline work).
- Variable path width and pressure profiles (preview.3; developer-confirmed working).
- Non-destructive live spline preview with Commit/Cancel and one-step Undo (preview.3.1; developer-confirmed working).
- Batch mask export from multiple selected targets (preview.4; developer-confirmed working).
- Multi-Mesh Workspace and reusable target sets (preview.5; developer-confirmed working).
- Continuous cross-target mask painting (preview.5.1; developer acceptance status not yet reconfirmed in this branch).
- Object and vegetation scatter placement (preview.6; implementation prepared; consuming Unity project validation pending).
- Connected-region Flood Fill and additional surface tools.

## v0.9 — Quality and optimization
- Automated tests.
- Graphics API compatibility.
- Large-mesh profiling.
- Memory optimization.
- Comprehensive edge-case testing.
- Extended device profiling and performance-regression baselines.

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
