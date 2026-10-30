# Changelog

## 0.4.0 - Undo/Redo and Starter Brush Library
- Added bounded, in-memory GPU mask history with per-stroke Undo and Redo snapshots.
- Added per-stroke commit and Escape-to-cancel behavior.
- Added Undo/Redo buttons and Ctrl/Cmd+Z, Ctrl/Cmd+Y and Ctrl/Cmd+Shift+Z shortcuts while working in Scene View.
- Made the Clear Selected Layer Channel operation undoable.
- Added an adjustable Undo memory budget; older snapshots are trimmed when the budget is reached.
- Extended brush presets with randomized scale.
- Added an Editor utility to generate Soft Round, Hard Round, Grunge and Scatter starter brush presets and their linear alpha textures.
- Added brush-library creation and preset selection controls to the Painter window.
- Undo history is session-only; use Save Mask PNG to persist painted results.

## 0.3.0 - GPU Brush Engine (first implementation)
- Added a ping-pong RenderTexture GPU mask stamping backend.
- Added paint and erase operations for one selected channel in a packed RGBA page.
- Added world-space brush sizing derived from the hit triangle UV0-to-world basis.
- Added continuous stroke interpolation based on world-space distance and brush spacing.
- Added optional brush-alpha texture support, hardness, opacity and rotation.
- Added black mask-page PNG creation, GPU readback, PNG persistence and profile references.
- Added live preview through a configurable MaterialPropertyBlock texture property (default `_PaintMask`).
- Added automatic page/channel assignment for newly created profile layers.

## 0.2.0 - Scene View Preview
- Added target-specific Scene View mesh picking and UV0 readout.
- Added world-space brush footprint preview.
- Added first-pass UV diagnostics.

## 0.1.0 - Foundation
- Added target component, reusable surface profiles, layer definitions and brush preset data.
