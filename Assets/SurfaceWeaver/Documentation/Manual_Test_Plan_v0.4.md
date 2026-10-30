# SurfaceWeaver v0.4.0 Manual Test Plan

## Setup
1. Back up the Unity project and commit the known-working SurfaceWeaver v0.3.0 branch state.
2. Replace `Assets/SurfaceWeaver` with this v0.4.0 package.
3. Wait for Unity to finish compiling. Resolve all compile errors before continuing.
4. Open Window > SurfaceWeaver > Painter and load a 1024 or 2048 RGBA mask page.

## Brush library
1. Click **Create Starter Brush Library**.
2. Confirm Soft Round, Hard Round, Grunge and Scatter `.asset` presets and their `.png` textures appear in `Assets/SurfaceWeaver/BrushLibrary/Starter`.
3. Assign each preset and confirm radius, opacity, hardness, spacing and scale randomness populate the window.
4. Paint a short test stroke with each brush. Confirm Grunge/Scatter use their texture alpha and random rotation as configured.
5. Select a custom grayscale brush texture and confirm its red channel is used.

## Undo/Redo
1. Paint Stroke A and release the mouse. Undo count should increase by one.
2. Paint Stroke B. Undo once; only Stroke B should disappear.
3. Undo again; Stroke A should disappear.
4. Redo once and twice; A and B should reappear in order.
5. Undo, then paint Stroke C; Redo should be cleared.
6. Start a stroke and press Escape; the entire active stroke should be reverted.
7. Clear the selected layer channel; Undo should restore the previous channel content. Other RGBA channels must remain unchanged.
8. Save Mask PNG, reload the page and confirm the painted result persists. Undo history is expected to reset after reload.

## Memory budget and errors
1. Set the history budget to 16 MiB at 1024 resolution; older snapshots should be trimmed as the limit is reached.
2. Use a higher resolution and verify the Painter reports history memory and the two live-buffer memory separately.
3. Verify painting refuses to start with a clear error if an Undo snapshot cannot be allocated.
4. Confirm no RenderTexture leaks are apparent after repeated page reloads and closing/reopening the Painter window.

## Input and navigation
1. Alt+left/middle mouse navigation should remain available in Scene View.
2. Left mouse over target paints; left mouse away from target does not.
3. Ctrl/Cmd+Z undoes and Ctrl/Cmd+Y or Ctrl/Cmd+Shift+Z redoes when Scene View is receiving the shortcut.
4. Confirm the normal Unity Undo command still works for unrelated editor operations.

## Acceptance criteria
- No C# or shader compiler errors.
- Each mouse stroke is one undo step.
- Undo/Redo restores exact RGBA mask page states.
- New paint after Undo discards the Redo branch.
- Other mask channels are preserved when painting, erasing or clearing one channel.
- Starter brush assets import as linear, readable, clamp-wrapped grayscale textures.
- Mask PNG save/reload retains the current page contents.
