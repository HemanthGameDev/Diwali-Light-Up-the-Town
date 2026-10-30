# SurfaceWeaver Development Roadmap

## 0.1 - Foundation
- Runtime target component and reusable ScriptableObject profiles.
- Surface layer and brush preset definitions.
- Initial UV0 mesh diagnostics.

## 0.2 - Scene View Preview
- Target-specific mesh picking.
- UV0 readout and world-space brush footprint.

## 0.3 - GPU Brush Engine (current)
- Ping-pong RenderTexture mask stamping.
- Paint and erase operations for selected RGBA channels.
- World-space brush radius derived from the hit triangle UV metric.
- Continuous stroke interpolation.
- Optional brush alpha texture, opacity, hardness, spacing and rotation.
- Linear PNG mask-page creation, saving and profile references.

## 0.4 - Editing Reliability
- Per-stroke Undo/Redo with bounded memory/history.
- Safe cancel/rollback and clear/restore operations.
- Brush preset library and deterministic random rotation/jitter.
- Improved UV seam and overlap diagnostics.

## 0.5 - Surface Rendering
- Profile-driven URP multi-layer shader/material integration.
- Packed mask page/channel mapping.
- Layer tiling, tint, opacity and edge blending.
- Shader variants and mobile performance controls.

## 0.6 - Advanced Authoring
- Scatter, smoothing and fill tools.
- Spline/path painting for environment workflows.
- Multiple target workflow and batch mask export.

## 0.7 - Production / Store Preparation
- Sample scenes and reusable brush presets.
- Documentation and tutorials.
- Supported Unity/URP compatibility matrix.
- Asset Store validation, screenshots, demo video and release packaging.
