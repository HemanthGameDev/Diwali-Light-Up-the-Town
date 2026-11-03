# Manual Test Plan — SurfaceWeaver v0.9.4-preview.1

## 1. Install and compile
1. Back up the working `Assets/SurfaceWeaver` folder or commit it to Git.
2. Merge the patch into the existing `Assets/SurfaceWeaver` folder, replacing `Editor/SurfaceWeaverWindow.cs` and adding the two v0.9.4-preview.1 documentation files.
3. Open the project in the same Unity editor version used for the baseline (Unity 6000.3.9f1).
4. Wait for script compilation. Confirm there are no C# compiler errors or new warnings attributable to SurfaceWeaver.

## 2. Profile serialized-object behavior
1. Open `Window > SurfaceWeaver > Painter` and assign the same target and `SurfacePaintProfile` used for the baseline.
2. Change Base Tint, Base Tiling, Base Albedo, Mask Storage and Mask Resolution individually; verify each edit is applied.
3. Change a value, Undo it, then Redo it. Confirm the UI and profile asset remain synchronized.
4. Edit the same profile through the Inspector while the Painter is open. Confirm the Painter refreshes the serialized values.
5. Switch to a different profile, then switch back. Confirm the correct profile fields are shown and no stale values remain.

## 3. Profile list and popup behavior
1. Add a paint layer. Confirm the Paint Layer popup gains the new layer.
2. Rename an existing layer through the profile UI/Inspector. Confirm the popup label updates.
3. Remove a layer using the existing supported profile workflow or test asset. Confirm popup options and selected index remain valid.
4. Add two or more spline anchors. Confirm Edit Anchor lists None, P1, P2, etc.
5. Add/remove anchors and confirm the dropdown is rebuilt to match the current count.

## 4. Preview material compatibility
1. Use a material exposing `_MaskPage0`. Confirm the preview resolves to `_MaskPage0` when the configured property is unsupported.
2. Use a compatible legacy material exposing `_PaintMask`. Confirm the legacy fallback still works.
3. Use a material exposing the explicitly configured property. Confirm the explicit property takes precedence.
4. Change target/material and verify no stale material reference or preview state remains.

## 5. Painting regression
1. Load a mask page and paint, erase and smooth on the test mesh.
2. Test Undo and Redo after each operation.
3. Test Flood Fill, save the mask PNG, close/reopen the Painter and verify persisted mask contents.
4. Test the live spline preview, Commit, Cancel and Undo.
5. Confirm the selected channel preview and combined RGBA preview still display correctly.

## 6. Repeat the Profiler measurement
1. Use the same benchmark scene, target mesh, profile, mask resolution, docked window layout and expanded/collapsed sections as the pre-patch capture.
2. Set the Profiler target to Editor, stop Play Mode, and capture the same type of brush/Scene View interaction.
3. Compare the same samples: `SurfaceWeaverWindow.Paint`, `SurfaceWeaverWindow.Repaint`, `SurfaceWeaverWindow.Render`, and `SurfaceWeaverWindow.OnGUI`.
4. Record median/max frame time, percentage of frames over 16.667 ms, GC Alloc per selected frame, and total GC allocation over the same sample window.
5. Verify there is no repeated per-repaint allocation from recreating the profile `SerializedObject`, the layer/anchor popup arrays, the toolbar label array, or renderer material arrays.

## Acceptance criteria
- No behavior regression in profile editing, Undo/Redo, target switching, popups, material preview, painting, mask save, or spline workflows.
- No new exceptions, missing material previews, or stale serialized values.
- A repeated Profiler capture demonstrates lower SurfaceWeaver Painter repaint allocations than the supplied baseline. Record actual numbers; do not infer a pass from source inspection alone.
