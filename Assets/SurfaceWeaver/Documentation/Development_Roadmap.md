# SurfaceWeaver Development Roadmap

## 0.1 — Foundation
- Runtime target component and reusable ScriptableObject profiles.
- Surface layer and brush preset definitions.
- Initial UV0 mesh diagnostics.

## 0.2 — Scene View Preview
- Target-specific mesh picking.
- UV0 readout and world-space brush footprint.

## 0.3 — GPU Brush Engine
- Ping-pong RenderTexture mask stamping.
- Paint and erase operations for selected RGBA channels.
- World-space brush radius derived from the hit triangle UV metric.
- Continuous stroke interpolation.
- Optional brush alpha texture, opacity, hardness, spacing and rotation.
- Linear PNG mask-page creation, saving and profile references.

## 0.4 — Editing Reliability
- Per-stroke GPU snapshot Undo/Redo with bounded memory.
- Safe cancel/rollback and selected-channel clear operation.
- Starter brush library: Soft Round, Hard Round, Grunge and Scatter.
- Random brush rotation and scale jitter.

## 0.5 — Advanced Mask Authoring (current)
- Positive-area UV triangle overlap diagnostics with bounded candidate-pair analysis.
- Geometric UV seam-edge diagnostics.
- GPU Smooth operation for the active mask channel.
- Full-channel Fill and Clear operations that preserve other RGBA channels.
- Updated manual test plan and diagnostics documentation.

## 0.6 — UV Robustness and Path Authoring
- Seam-aware cross-island brush propagation.
- UV island padding/dilation and mip-bleed validation.
- More robust non-uniform UV and mirrored-island handling.
- Spline/path painting and deterministic path stamps.
- Multi-target authoring and batch mask export.

## 0.7 — Rendering and Production Integration
- Profile-driven multi-layer URP shader/material integration.
- Packed mask page/channel binding.
- Per-layer albedo, tint, tiling, opacity and blend controls.
- Shader variants and mobile performance controls.
- Runtime profile/mask consumption API (painting remains Editor-only unless a separate runtime module is developed).

## 0.8 — Commercial Readiness
- Sample scenes, example profiles and brush presets.
- Full documentation, setup guide and tutorials.
- Supported Unity/URP compatibility matrix.
- Automated/editor test coverage and graphics API validation.
- Asset Store validator, screenshots, demo video, package cleanup and release checklist.
