# SurfaceWeaver v0.4.0 Build Notes

## Added

- GPU-backed in-memory per-stroke Undo/Redo snapshots.
- `BeginStroke`, `EndStroke`, `CancelStroke`, `UndoLastStroke` and `RedoLastStroke` in `SurfaceGpuBrushEngine`.
- An adjustable history memory budget (default 64 MiB, UI range 16-256 MiB). Oldest history entries are disposed when the budget is exceeded.
- Undoable clear-channel action.
- Escape cancels the current stroke. Mouse-up commits the current stroke.
- Ctrl/Cmd+Z undo; Ctrl/Cmd+Y and Ctrl/Cmd+Shift+Z redo while Scene View input is active.
- Preset scale jitter, applied per stamp. Preview uses the maximum radius range.
- `SurfaceBrushPresetFactory` creates Soft Round, Hard Round, Grunge and Scatter presets with 256x256 linear grayscale PNG alpha maps.

## Important behavior and limitations

- Undo/Redo snapshots are held in GPU RenderTextures and are session-only. They are cleared when the mask page/session is reloaded or the Painter window is closed. Save Mask PNG persists the current page.
- The history memory budget counts stored history snapshots, but does not include the two live painting buffers, source texture, graphics-driver overhead or editor overhead. A pending stroke temporarily requires one additional snapshot.
- A snapshot is taken at the beginning of a stroke. If a snapshot cannot be allocated, painting is refused for that stroke rather than silently allowing an irreversible edit.
- If one snapshot exceeds the configured budget, the completed stroke remains painted but cannot be retained for Undo. Use a higher budget or lower mask resolution.
- Starter brush assets are generated under `Assets/SurfaceWeaver/BrushLibrary/Starter`. Creating the library again creates unique copies rather than overwriting existing assets.
- Custom brush textures use the red channel as alpha. Import them as linear (sRGB disabled), readable, clamp-wrapped grayscale textures for predictable results.
- UV island seam continuation, overlap-aware painting, persistent history, smoothing/fill tools, spline painting and the production multi-layer URP surface shader remain future work.

## Manual verification required

This source package has not been compiled or exercised in a Unity Editor in this environment. Follow `Manual_Test_Plan_v0.4.md` and report any Console errors before treating the milestone as validated.
