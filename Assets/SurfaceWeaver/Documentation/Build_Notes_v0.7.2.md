# SurfaceWeaver v0.7.2 – Live Preview Binding Fix

## Scope
This is an incremental Editor-only patch for the v0.7.1 API-compatible source. It changes only the live mask preview binding in `SurfaceWeaverWindow.cs`.

## Confirmed user observation
Clearing renderer-wide and per-material MaterialPropertyBlocks on a duplicate ground object restored its green base appearance after mask loading. This isolates the failure to the property-block preview path, although it does not by itself prove whether the renderer-wide scope, an existing per-material block interaction, or a shader/MPB interaction is the exact sub-cause.

## Change
- Stops creating/updating renderer-wide MaterialPropertyBlocks for the live mask preview.
- Finds the renderer material slot that exposes the selected preview texture property (`_MaskPage0` for the v0.7 shader, or `_PaintMask` for legacy Shader Graph materials).
- Creates a temporary `HideAndDontSave` material clone for that slot and assigns the live mask texture to the clone.
- Restores the exact original material reference when changing target/profile or closing/disabling the Painter window.
- Does not modify the source `.mat` asset or the GPU mask texture.
- Preserves the original `SurfaceWeaverWindow.cs.meta` GUID.

## Important limitations / test requirements
- This is an Editor preview workaround, not a runtime rendering change.
- A temporary clone is only active while the Painter window is open and the target is previewed.
- If another tool changes the target renderer's material slot while the preview is active, the Painter will not overwrite that newer material when restoring; it restores only if its own preview clone is still in the tracked slot.
- The patch has passed source-level structural checks only. Unity C# compilation, material assignment, multi-slot rendering, scene save/reload and shader behavior must be tested in the user's Unity 6 URP project.
- Before testing, use the duplicate/test GameObject whose old property blocks were cleared. Do not clear property blocks on the original production object if other systems rely on them.

## Install
Extract the ZIP into the Unity project root. It contains `Assets/SurfaceWeaver/Editor/SurfaceWeaverWindow.cs` and its matching existing `.meta` file. Back up the current folder first.
