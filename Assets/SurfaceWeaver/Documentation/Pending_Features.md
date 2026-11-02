# SurfaceWeaver — Remaining Work After v0.8.0 Feature Development

This backlog records product work that remains after the v0.8 advanced-authoring feature set. Implemented source is not equivalent to release validation.

## v0.8 consolidation requirements
- Complete the consolidated manual regression plan.
- Compile in the supported Unity 6 URP project from a clean import.
- Test existing projects/assets created before v0.8.
- Verify all EditorWindow menu entries and their lifecycle after domain reload.
- Validate spline anchor/handle editing, closed paths, width/pressure interpolation, live preview Commit/Cancel and grouped Undo behavior.
- Validate batch export naming, collision handling, manifest contents and persisted-file-only behavior.
- Validate target-set GlobalObjectId resolution after save/reopen/scene reload.
- Validate Multi-Mesh Painter across different targets, target switching, grouped Undo/Redo, Escape cancel and Save All Masks.
- Validate weighted scatter selection, preview cleanup, deterministic metadata, existing-instance adoption/live updates, prefab links and Undo.
- Validate Flood Fill chart restriction on split-seam meshes, 4-/8-neighbour behavior, selected-channel preservation, pixel orientation, Undo/Redo and save/reload.
- Run the existing SurfaceWeaver mobile benchmark scene and compare against the saved OnePlus CPH2467 baseline.

## v0.9 — Quality and compatibility
- Automated Unity EditMode tests.
- Graphics API and Unity/URP compatibility matrix.
- Large-mesh and long-path profiling.
- Memory optimization and readback-budget validation.
- Comprehensive edge cases: non-readable meshes, submeshes, invalid colliders, non-uniform/negative transforms, UVs outside 0–1, thin charts and stacked UVs.
- Android Vulkan/OpenGLES3 and additional device testing.

## v1.0 — Asset Store release
- Professional Editor UI, first-run setup and contextual diagnostics.
- Lite/Pro split and feature entitlement validation.
- Demo project, sample assets and presets.
- Documentation, tutorials and support policy.
- License/third-party audit, package validator and release marketing assets.

## Explicitly out of scope for v0.8
- Cross-target spline authoring.
- Geodesic world-surface Flood Fill.
- Independent paint data on overlapping/stacked UVs sharing the same texels.
- Per-layer normal/metallic/height blend maps.
