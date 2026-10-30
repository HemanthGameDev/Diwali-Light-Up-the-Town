# SurfaceWeaver – Architecture

## Product direction

SurfaceWeaver is a **URP-first, custom-static-mesh environment surface authoring tool**. It is not intended to be a general runtime painting system in its first release.

The product should help environment artists paint and bake layered materials on Blender/Maya/custom meshes. It should be useful for ground pieces, cliffs, modular environment meshes and large static environment surfaces.

The differentiators to validate with users are:

1. UV diagnostics and clear warnings before painting.
2. Path/spline-assisted painting for roads, trails, riverbanks and other environment strips.
3. Reusable surface recipes and portable baked masks.
4. A mobile-conscious URP output workflow with predictable texture/shader budgets.

These are product hypotheses, not established competitive advantages. The generic mesh-painting category already contains mature products; validate demand before expanding scope.

## Runtime / Editor boundary

```text
Assets/SurfaceWeaver/
  Runtime/
    SurfacePaintTypes.cs
    SurfaceLayerDefinition.cs
    SurfacePaintProfile.cs
    SurfacePaintTarget.cs
    SurfaceBrushPreset.cs
    *.asmdef

  Editor/
    SurfaceWeaverWindow.cs
    SurfacePaintTargetEditor.cs
    MeshUvValidator.cs
    MeshUvValidationReport.cs
    *.asmdef

  Shaders/                 (next milestone)
  Samples~/                (later; source samples)
  Documentation/
```

### Runtime assembly

`HemanthGameDev.SurfaceWeaver.Runtime`

Contains serializable data and components only. It must not reference `UnityEditor`.

- `SurfacePaintTarget`: identifies the target `MeshFilter` and `MeshRenderer`, stores a `SurfacePaintProfile`, and chooses the UV channel.
- `SurfacePaintProfile`: reusable recipe containing a base texture, ordered overlay definitions, mask-page references and output resolution.
- `SurfaceLayerDefinition`: one overlay's albedo texture, tint, tiling, opacity, and packed mask-page/channel assignment.
- `SurfaceBrushPreset`: optional brush alpha texture and default brush settings.
- `SurfacePaintTypes`: mask channel, operation, storage mode and common brush settings.

### Editor assembly

`HemanthGameDev.SurfaceWeaver.Editor`

Editor-only assembly. It owns Scene View interaction, GPU resources, UV diagnostics, mask persistence, Undo/Redo, UI and import settings. It may reference the runtime assembly, but the runtime assembly must never reference this assembly.

## Mask storage model

The planned default is packed RGBA pages. One RGBA mask texture stores four independent grayscale layer masks:

- R = overlay layer 0
- G = overlay layer 1
- B = overlay layer 2
- A = overlay layer 3

Additional mask pages can hold further layers. A layer definition stores a mask page index and channel. The shader samples the selected channels and blends the base surface with the enabled overlay layers in stack order.

Keep an option for separate grayscale masks where artists need simple exports or a target shader cannot use packed pages.

## Painting backend

Planned Editor-only backend:

1. Scene View raycast against a non-convex `MeshCollider` on the selected static mesh.
2. Read the hit UV from the configured UV channel.
3. Calculate a local UV footprint for the requested world-space brush radius using the hit triangle's world positions and UVs. Do not estimate brush scale from the overall mesh bounds; that stretches on rotated or irregular meshes.
4. Stamp the brush alpha into a ping-pong `RenderTexture` pair using a small URP-compatible hidden paint shader.
5. Keep the render targets transient while the session is active; commit to a persistent mask asset only on explicit Save/Apply.
6. Record stroke checkpoints for Undo/Redo. Do not serialize a full-resolution PNG after every brush sample.
7. Save/import mask assets with explicit sRGB, mipmap, wrap and compression settings. Use atomic file replacement and a backup before overwriting user assets.

## Shader plan

Use a URP-compatible shader/material template rather than requiring users to hand-build a Shader Graph. Shader parameters should support:

- base albedo, tint and tiling;
- overlay albedo, tint, tiling and opacity;
- packed mask pages;
- optional edge variation/noise;
- a configurable maximum overlay count per shader variant.

The first production target should be mobile-friendly: opaque surface, no screen-space effects, no runtime painting, no unnecessary normal/metallic maps, and predictable sampler count. The tool must disclose texture-sample cost as layers increase.

## UV constraints for initial release

- Static `MeshFilter` + `MeshRenderer` meshes.
- Triangle topology.
- A valid selected UV channel, default UV0.
- UVs normally contained in 0–1 for the first painter backend.
- Overlapping UV islands share mask pixels; this must be detected or clearly warned about before painting.
- UV seam padding/dilation is required for reliable mipmapped texture output.

The current validator checks basic UV validity but does **not** yet detect overlaps or create padding. Do not describe arbitrary UV support as production-ready until these tests exist.

## Architecture rules

- No `UnityEditor` references in Runtime.
- No modification of the source imported mesh asset; keep working data in profile/mask assets.
- Prefer `sharedMesh` for inspection; do not instantiate or mutate the source mesh by accident.
- Use `Undo` for all editor object changes.
- Save mask output explicitly and preserve existing asset GUID/meta files when overwriting.
- Keep target material assignment non-destructive. Use a documented binder/MaterialPropertyBlock path when the renderer integration is implemented.
- No DRM, account login, usage telemetry or external web service is needed.
