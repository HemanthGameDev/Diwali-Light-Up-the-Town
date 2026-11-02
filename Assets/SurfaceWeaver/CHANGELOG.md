# SurfaceWeaver Changelog

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
