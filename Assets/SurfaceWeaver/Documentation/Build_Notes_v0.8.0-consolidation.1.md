# SurfaceWeaver v0.8.0-consolidation.1 — Advanced Authoring Consolidation Candidate

## Purpose
This package assembles the v0.8 implementation sequence into one importable `SurfaceWeaver` folder. It is intended for final regression testing in the consuming Unity 6 URP project. It is not yet a certified Asset Store release.

## Consolidated feature set
- Editable spline anchors and incoming/outgoing Bézier handles; selectable anchors; closed-path compatibility.
- Per-anchor width scale and pressure, smooth profile interpolation, and non-destructive live spline preview with Commit/Cancel.
- Batch export of persisted mask pages across selected targets, including collision-safe paths and optional CSV manifest.
- Reusable Multi-Mesh Workspace target sets resolved through GlobalObjectIds.
- Separate Multi-Mesh Painter with cross-target paint/erase/smooth strokes, per-target mask sessions, grouped Undo/Redo, cancellation and Save All Masks.
- Editor-only object/vegetation scatter with weighted prefab selection, slope/spacing/exclusion controls, deterministic instance metadata, temporary preview ghosts, and optional live update of compatible existing instances.
- Connected-region Flood Fill on the selected RGBA channel with tolerance, 4-/8-neighbour connectivity, Undo/Redo, persistence, and optional UV0 seed-chart restriction.

## Import and upgrade
1. Commit/backup the current project and preserve user-created profiles, target sets, scatter profiles, path assets, mask pages and scenes.
2. Close Unity before updating package files.
3. Merge this package's `SurfaceWeaver` folder into `Assets/SurfaceWeaver`, allowing package source and documentation files to be updated. Do not delete user-created assets or scenes.
4. Reopen Unity and allow compilation/domain reload to finish.
5. Check Console for compile errors before opening the tools.
6. Run `Manual_Test_Plan_v0.8.0-consolidation.1.md` from top to bottom.

## Important compatibility / scope notes
- Keep the single-target Painter and spline editor as the standard workflow. Multi-Mesh Painter is a separate EditorWindow.
- Spline anchor authoring is single-active-target; this consolidation does not add cross-target spline paths.
- Batch export only reads saved mask-page source files. Save masks and commit spline previews first.
- Multi-Mesh Painter requires each target to meet its documented MeshRenderer, readable mesh, UV0, matching non-convex MeshCollider, material and unique mask-page requirements.
- Scatter live update affects only metadata-backed instances. Adopted legacy placements may be approximate under non-uniform prefab scales or complex parenting. Weight affects future prefab selection; density/spacing/radius affect future stamps.
- Flood Fill is UV0 chart-restricted texture-space connectivity, not geodesic world-surface connectivity. Overlapping/stacked charts sharing texels are ambiguous. Large masks need memory testing.
- v0.7 layered URP shader remains limited to one base plus up to four overlays and one packed RGBA mask page. Full mobile/API compatibility is not claimed.

## Validation status
The consolidated source tree was assembled from the supplied v0.7.12 package and the v0.8 preview/accepted hotfix sequence. Static package checks: ZIP integrity, Unity `.meta` GUID uniqueness, presence of `.meta` files for C# scripts, and C# brace-balance screening. These checks are not a substitute for Unity compilation. Actual Unity compilation, Scene View interaction, Undo/Redo, persistence, cross-target painting, scatter live-update behavior, Flood Fill chart isolation, and performance regression must be verified in the consuming project.
