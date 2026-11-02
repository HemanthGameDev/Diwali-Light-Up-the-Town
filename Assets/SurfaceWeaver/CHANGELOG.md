# Changelog

## v0.9.0-preview.1
- Added Unity EditMode regression test assembly for Flood Fill, UV island chart building, Multi-Mesh target set data, and spline path profile data.
- Added v0.9 test coverage matrix and manual integration plan.
- Unity compilation/test execution remains pending in the consuming project.

# SurfaceWeaver Changelog

## v0.8.0-preview.5.1 — Continuous cross-target painting

- Added a separate Multi-Mesh Painter window launched from the Multi-Mesh Workspace or `Window > SurfaceWeaver > Multi-Mesh Painter`.
- Resolves targets in a reusable `SurfacePaintTargetSet` and exposes per-target paint-layer mapping.
- Performs Scene View ray picking across ready target colliders; the nearest registered target under the pointer receives each stamp.
- Supports one continuous Paint/Erase/Smooth drag across multiple registered meshes.
- Lazily creates one GPU mask/UV raster session per target touched by a stroke.
- Groups the per-target GPU snapshots into one logical cross-target Undo/Redo action; Escape cancels the active stroke across every touched target.
- Adds Save All Masks to persist all dirty target pages.
- Adds live material preview support for `_MaskPage0` layered URP materials and legacy `_PaintMask` materials.
- Safely saves/disposes an open single-target Painter GPU session before multi-mesh editing and reloads its mask when returning.
- Keeps spline authoring in the original single-target Painter; no multi-surface spline registration controls are introduced.
- Requires each target to have its own unique saved mask-page asset, packed RGBA page 0, UV0, readable mesh, MeshRenderer and matching non-convex MeshCollider.
- Source-level checks only; Unity compilation and consuming-project manual acceptance remain pending.

## v0.8.0-preview.5 — Multi-Mesh Workspace

- Added a separate Multi-Mesh Workspace window for reusable sets of existing SurfacePaintTargets.
- Stores scene target references as GlobalObjectId strings in a target-set asset.
- Supports adding selected targets, discovering targets in loaded scenes, validation, selection, pinging, and opening each target in the existing single-target Painter.
- Added workspace-only removal/clear operations that do not modify scene objects or target components.
- Integrated a shortcut to the existing Batch Mask Export workflow.
- Deliberately keeps the Painter and spline editor single-target; continuous brush strokes across mesh boundaries remain future work.
- Does not reintroduce secondary surface registration or surface binding in spline authoring.

## v0.8.0-preview.4 — Batch mask export

- Added a separate `Tools/SurfaceWeaver/Batch Export Masks` Editor window.
- Supports multi-selection of `SurfacePaintTarget` GameObjects and discovery of all targets in loaded scenes.
- Copies persisted, file-backed lossless mask-page assets (PNG, TGA, EXR, TIF, TIFF) without re-encoding source pixel data.
- Adds optional per-target output folders, collision-safe file naming and a CSV manifest.
- Refreshes the AssetDatabase when exporting into the project's `Assets` folder.
- Does not modify source profiles, targets, meshes or mask assets.
- The normal Painter and spline authoring remain single-active-target; multi-target editing and cross-target painting are still planned separately.
- Unsaved GPU paint/live preview data must be committed and saved in the Painter before export.

## v0.8.0-preview.3.1 — Live spline preview editing

- Added a non-destructive live spline preview workflow: Apply Spline Preview stamps the current path while keeping one pre-preview mask snapshot pending.
- Editing per-anchor width or pressure rebuilds the visible mask preview from the original pre-preview mask, so repeated adjustments do not accumulate paint.
- Path geometry, tangent, closed-path, pressure behavior, and relevant brush-setting changes refresh the preview automatically.
- Added Commit Preview and Cancel Preview controls. Commit records the final path as one Undo step; Cancel restores the pre-preview mask.
- Undo acts as Cancel Preview while a live preview is active; Redo is disabled until the preview is committed or cancelled.
- Saving the mask, leaving Path Authoring, switching target/profile/page, applying another channel operation, or closing the Painter commits the current preview before continuing.
- Added GPU engine support to restore the pending stroke baseline without releasing its Undo snapshot.
- The preview remains Editor-only and continues using the active single SurfacePaintTarget.


## v0.8.0-preview.3 — Variable path width and pressure profiles

- Added per-anchor Width Scale (0.05–4.0) and Pressure (0–1) values.
- Smoothly interpolates width and pressure between anchors using a Catmull–Rom scalar profile.
- Added independent Pressure Affects Width and Pressure Affects Opacity controls.
- Added Scene View width-rail preview and selected/all profile reset controls.
- Spline application now varies stamp radius and opacity along the path in both seam-aware and legacy UV modes.
- Adaptive stamp spacing uses the narrowest sampled effective radius to reduce gaps in narrow sections.
- Path assets now persist width/pressure values and pressure behavior settings. Older path assets remain backward compatible with neutral width/pressure values of 1.
- Anchor movement and tangent edits preserve width/pressure data.

# SurfaceWeaver v0.8.0-preview.2-hotfix.3 — Restore single-target spline workflow

## Why this hotfix exists
The multi-surface registration/attachment experiment made point placement and Scene View interaction unreliable in the user's project. This hotfix intentionally removes that experiment and restores the simpler single-active-target workflow from preview.1.

## Changes
- Removed the **Surface Targets** registration list and all registered-collider selection logic.
- Removed per-anchor surface binding, multi-surface snapping, Free Move, and Surface Offset modes.
- Restored spline point placement against the active `SurfacePaintTarget` only.
- Restored target-local anchor positions and tangents.
- Preserved incoming/outgoing Bézier tangent editing, closed paths, reusable path assets, spline application, and the existing mask workflow.
- Added an **Edit Anchor** dropdown so any point can be selected directly from the Inspector.
- Dragging any anchor automatically selects that anchor; each anchor handle remains active regardless of which point is currently selected.

## Deliberate limitation
Spline anchors are placed and moved only on the active `SurfacePaintTarget` mesh, matching the earlier single-target behavior. Clicking or dragging onto a different mesh does not register it or move the path to it. Multi-mesh path authoring is deferred until it can be designed as a complete painting workflow rather than a snapping-only feature.

## Compatibility note
This patch restores the preview.1 `SurfacePaintPathPoint` data model. Paths created with preview.2 that contain anchors bound to secondary meshes or stored in Free Move world space should be recreated on the active target after applying this patch. Standard paths created on the active target and their custom tangent data remain compatible. Back up the project and path assets before installation.

## Validation status
Source-level checks only. Unity Editor compilation, Scene View input, serialization, and painting must be verified in the consuming project using the manual test plan.

## v0.8.0-preview.6 — Object & vegetation scatter

- Added a separate Editor-only Object & Vegetation Scatter window and reusable scatter-profile asset.
- Uses an existing `SurfacePaintTargetSet` and raycasts only registered targets with matching enabled non-convex MeshColliders.
- Supports weighted prefab placement, brush-radius sampling, per-stamp density, stroke spacing and minimum instance distance.
- Supports surface-normal alignment, maximum-slope filtering, surface offset, random scale, yaw and optional pitch/roll.
- Supports optional collider exclusions through a configurable LayerMask and radius; registered paint-target colliders are ignored by this check.
- Adds a Scene View brush-radius preview plus Paint and Erase modes.
- Places ordinary prefab instances in per-profile/per-target scene hierarchy groups; prefab links are retained.
- Adds per-stamp Undo/Redo and target-specific erasing.
- Keeps the existing grayscale Scatter brush, Painter, spline tools, masks, runtime shader and Runtime assembly unchanged.
- Source and archive checks only; consuming Unity project compilation and manual acceptance remain required.


## v0.8.0-preview.6.1
- Added per-prefab Enabled toggles; enabled entries are selected uniformly rather than by weight.
- Added non-destructive live prefab placement ghosts in Scene View.
- Profile changes rebuild the preview; committed objects are not modified retroactively.


## v0.8.0-preview.7
- Added UV texture-space connected-region mask Flood Fill with tolerance and 4-/8-neighbour connectivity.
- Preserves non-selected RGBA channels and creates one Undo step.
- Explicitly documents UV-island adjacency limitations and CPU readback memory costs.


## v0.8.0-preview.8
- Added optional-by-toggle UV0 seed-island restriction for Connected-Region Flood Fill (enabled by default).
- Fill traversal is limited to texels rasterized from the mesh chart containing the picked triangle.
- Preserved preview.7 unrestricted texture-space mode, RGBA isolation, tolerance, 4/8 connectivity, Undo/Redo and PNG persistence.
- Added clear validation for unreadable meshes and mismatched MeshCollider shared meshes.
- Added manual test coverage for split seams, thin islands, channel preservation and regression checks.


## v0.8.0-consolidation.1 — Advanced Authoring Consolidation

- Consolidates the v0.8 spline authoring, live width/pressure preview, batch mask export, Multi-Mesh Workspace, separate Multi-Mesh Painter, weighted object/vegetation scatter with existing-instance updates, and connected-region UV-island-aware Flood Fill work.
- Updates the README, roadmap and remaining-work backlog to reflect the v0.8 feature scope and known boundaries.
- Adds a complete v0.8 regression test plan.
- This is a consolidation candidate, not a claim of Unity compilation or release certification; run the consuming-project validation plan before tagging v0.8.0.
