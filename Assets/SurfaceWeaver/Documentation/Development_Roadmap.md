# SurfaceWeaver – Development Roadmap

## Phase 0 – Foundation (current starter)

- [x] Runtime/editor assembly separation.
- [x] Surface profile and layer data model.
- [x] Static custom-mesh target component.
- [x] Brush preset data asset.
- [x] First-pass UV validator.
- [x] Editor window shell.
- [ ] Compile and validate inside the user's Unity 6 URP project.

## Phase 1 – Scene View painting MVP

- [ ] Mesh picking for `MeshFilter` + `MeshRenderer` targets.
- [ ] Collider setup/validation and UV-channel selection.
- [ ] Correct world-space brush footprint derived from hit triangle UV/world basis.
- [ ] Built-in soft round brush.
- [ ] GPU stamp shader and ping-pong RenderTexture backend.
- [ ] Paint, erase, smooth and fill operations.
- [ ] Stroke interpolation/spacing.
- [ ] Save/load a mask page as a persistent PNG asset.
- [ ] Material preview binding without modifying shared material assets.

## Phase 2 – Authoring reliability

- [ ] Undo/Redo at stroke granularity.
- [ ] Autosave/recovery for interrupted editor sessions.
- [ ] UV overlap detection and warnings.
- [ ] UV seam padding/dilation on export.
- [ ] Atomic save with backup/rollback.
- [ ] UV channel selection and UV range visualization.
- [ ] Diagnostics for unreadable meshes, non-triangle submeshes and invalid collider setup.

## Phase 3 – Brush library

- [ ] Hard round brush.
- [ ] Soft round brush.
- [ ] Imported grayscale brush alpha.
- [ ] Brush rotation and randomized rotation.
- [ ] Spacing, flow, opacity, hardness and jitter.
- [ ] Stamp preview and cursor visualization.
- [ ] Brush preset ScriptableObjects and sample presets.

## Phase 4 – Layer stack and URP material output

- [ ] Base + overlay texture layers.
- [ ] Packed RGBA mask pages.
- [ ] Layer reorder, enable/disable, duplicate and remove.
- [ ] Per-layer texture, tint, tiling and opacity.
- [ ] URP shader template and material setup assistant.
- [ ] Layer-count shader variants with explicit mobile texture-sample budgets.
- [ ] Profile reuse across targets.

## Phase 5 – Differentiating environment tools

- [ ] Scene View path/spline painting with width, falloff and taper.
- [ ] Path noise/jitter and edge breakup.
- [ ] UV diagnostic visualization (overlap, degenerate triangles, island bounds).
- [ ] Batch mask export for selected targets.
- [ ] Surface recipe presets (grass/dirt/stone/mud/sand examples).

## Phase 6 – Production QA

- [ ] Unity 6.0 LTS smoke test.
- [ ] Latest supported Unity Editor validation before submission.
- [ ] URP project test and clean import test.
- [ ] Android build test (64-bit target where applicable).
- [ ] Scene/prefab persistence and domain reload tests.
- [ ] Test on UV0 and alternate UV channel meshes.
- [ ] Test multiple submeshes, seams, overlapping islands, rotated/scaled objects, negative scale and non-readable imports.
- [ ] Profile GPU/CPU time and texture memory at 512/1024/2048/4096 resolutions.
- [ ] Build demo scene and comprehensive offline documentation.

## Milestone update – v0.2.0

Completed in source:

- Scene View event registration and preview enable/disable control.
- Target-specific ray picking through a non-convex MeshCollider.
- World-space circular brush footprint aligned with the hit normal.
- Live UV0, world position, world normal and triangle-index diagnostics.
- MeshCollider add/refresh workflow with readable-mesh validation.

Next implementation milestone:

- GPU brush stamping using ping-pong RenderTextures.
- UV-space brush stamp projection and continuous stroke interpolation.
- Channel-specific mask painting for packed RGBA pages.
- Undo/Redo and persistent mask asset import/export.
