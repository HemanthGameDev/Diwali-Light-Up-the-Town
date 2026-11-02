# SurfaceWeaver v0.8.0-preview.3.1 — Live Spline Preview Editing

## Scope

Incremental update on top of v0.8.0-preview.3. The active workflow remains single-target. This update makes an applied spline result editable in-place without repeatedly clearing or manually reapplying the mask.

## Workflow

1. Load the active mask page and author a spline with at least two anchors.
2. Click **Apply Spline Preview**. The current spline is stamped into the working mask and shown through the existing live material preview.
3. Change an anchor's Width Scale or Pressure, or change the pressure behavior toggles. The editor restores the mask to its pre-preview snapshot and stamps the latest spline settings again.
4. Use **Commit Preview** to keep the final result as one Undo step, or **Cancel Preview** to restore the exact pre-preview mask.
5. Save Mask PNG to persist the committed result. Saving automatically commits an active preview first.

## Implementation details

- The GPU brush engine exposes `ResetPendingStrokePreview`, which restores the working mask from the existing pending stroke snapshot without releasing it.
- Repeated preview rebuilds do not allocate additional Undo snapshots and do not accumulate opacity or stamp overlap from previous preview iterations.
- The final committed path is one logical Undo item, regardless of how many times width/pressure values were adjusted.
- Undo while the preview is active cancels that preview first. Redo is disabled while a preview is pending.
- Editing the path anchors, Bézier tangents, closed-path state, width/pressure values, pressure switches and relevant brush settings schedules a coalesced preview rebuild.
- Saving the mask, switching targets or paint layers, loading another path, applying channel fill/clear, leaving Path Authoring, and closing the Painter commits the current preview to avoid leaving a pending GPU stroke.
- If a preview rebuild fails, SurfaceWeaver attempts to restore the pre-preview mask and reports the error in Path Status.

## Memory and performance

- A live preview retains one RGBA32 RenderTexture snapshot for the pending Undo operation. Rebuilds reuse this snapshot.
- This adds one mask-sized snapshot while a preview is active; it does not allocate a new snapshot for every slider event.
- GPU stamping still runs through the existing seam-aware or legacy UV path.
- Preview rebuilds are coalesced through the Editor delay callback to avoid redundant rebuilds within the same UI event cycle.

## Compatibility

- Existing path asset serialization is unchanged.
- Existing single-target spline behavior, width/pressure profiles, Bézier handles and closed paths are preserved.
- No Surface Targets registration, multi-mesh path binding, or runtime spline component is introduced.

## Validation status

Source-level checks and patch integrity checks are performed before distribution. Unity compilation, live material preview refresh, slider/handle editing, Undo/Redo, Cancel/Commit, mask saving and session transitions must be verified in the user's Unity project using the manual test plan.
