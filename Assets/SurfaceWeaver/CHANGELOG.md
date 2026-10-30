# Changelog

## 0.3.0 - GPU Brush Engine (first implementation)
- Added a ping-pong RenderTexture GPU mask stamping backend.
- Added paint and erase operations for one selected channel in a packed RGBA page.
- Added world-space brush sizing derived from the hit triangle UV0-to-world basis.
- Added continuous stroke interpolation based on world-space distance and brush spacing.
- Added optional brush-alpha texture support, hardness, opacity and rotation.
- Added black mask-page PNG creation, GPU readback, PNG persistence and profile references.
- Added live preview through a configurable MaterialPropertyBlock texture property (default `_PaintMask`).
- Added automatic page/channel assignment for newly created profile layers.
- Undo/Redo, seam-aware island continuation, full overlap detection, smoothing/fill tools, spline painting and the production multi-layer URP surface shader remain future milestones.

## 0.2.0 - Scene View Preview
- Added target-specific Scene View mesh picking and UV0 readout.
- Added world-space brush footprint preview.
- Added first-pass UV diagnostics.

## 0.1.0 - Foundation
- Added target component, reusable surface profiles, layer definitions and brush preset data.
