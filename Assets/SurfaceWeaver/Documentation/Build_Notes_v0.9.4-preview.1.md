# SurfaceWeaver v0.9.4-preview.1 — Editor Repaint Allocation Reduction

## Scope
This preview targets managed allocations observed in the SurfaceWeaver Painter EditorWindow repaint path. The user's Unity Profiler capture identified `SurfaceWeaverWindow.Paint` / `SurfaceWeaverWindow.Repaint` and `SurfaceWeaverWindow.OnGUI` as allocation-heavy samples.

## Changes
- Reuses immutable `GUIContent` labels for serialized profile fields.
- Reuses the Paint / Erase / Smooth toolbar label array and Undo/Redo counter width option.
- Caches the profile `SerializedObject` per active `SurfacePaintProfile`, while still calling `Update()` on each GUI pass and `ApplyModifiedProperties()` after drawing fields.
- Caches paint-layer popup labels and rebuilds them only when the active profile, layer count, or a layer display name changes.
- Caches spline-anchor popup labels and rebuilds them only when the anchor count changes.
- Replaces repeated `Renderer.sharedMaterials` array retrieval in preview-property checks with `Renderer.GetSharedMaterials(List<Material>)` and a reusable scratch list.
- Resolves `_MaskPage0` / `_PaintMask` compatibility candidates in one material-list pass.

## Behavior intended to remain unchanged
- Target/profile selection and assignment.
- Serialized profile editing, including Undo/Redo and external Inspector changes.
- Layer and spline-anchor popup contents after add/remove/rename operations.
- Existing live preview property precedence: configured property first, then `_MaskPage0`, then `_PaintMask`.
- Paint, Erase, Smooth, Flood Fill, mask preview, mask save, spline preview, and Undo/Redo workflows.

## Profiling baseline supplied by the user
The Editor-mode capture showed a selected-frame median of 14.420 ms and maximum of 306.549 ms across the selected range, with 34% of frames over the 16.667 ms target. The expanded Hierarchy showed `SurfaceWeaverWindow.Paint` at 6.95 ms / 149.5 KB GC allocation, `SurfaceWeaverWindow.Repaint` at 6.90 ms / 149.5 KB, and `SurfaceWeaverWindow.OnGUI` at 6.04 ms / 118.7 KB in the selected frame. These values are the pre-patch baseline from that capture, not a promised post-patch result.

## Validation status
- Source-level inspection and patch consistency checks only in the patch-generation environment.
- Unity compilation, interaction regression testing, and repeat Profiler capture must be completed in the consuming Unity 6.3 project.
- Do not consider the optimization accepted until the same benchmark scene and comparable UI state are profiled before/after.
