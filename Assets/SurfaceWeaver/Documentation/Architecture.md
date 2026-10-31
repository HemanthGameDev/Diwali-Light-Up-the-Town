# SurfaceWeaver — Architecture

## Product direction

SurfaceWeaver is a **URP-first, Editor-only custom static-mesh surface authoring tool**. Its first release is for environment artists painting and baking surface masks on Blender/Maya/custom meshes. It is not a general runtime painting system in the current scope.

Product hypotheses to validate with users include pre-paint UV diagnostics, seam-aware world-space projection, spline/path painting, reusable path recipes, and a mobile-conscious baked-mask output workflow. These are proposed differentiators, not proven market advantages.

## Assembly boundary

```text
Assets/SurfaceWeaver/
  Runtime/
    SurfacePaintTarget.cs
    SurfacePaintProfile.cs
    SurfaceLayerDefinition.cs
    SurfaceBrushPreset.cs
    SurfacePaintPathAsset.cs
    HemanthGameDev.SurfaceWeaver.Runtime.asmdef

  Editor/
    SurfaceWeaverWindow.cs
    SurfaceScenePicker.cs
    SurfaceGpuBrushEngine.cs
    SurfaceUvRasterMesh.cs
    MeshUvValidator.cs
    HemanthGameDev.SurfaceWeaver.Editor.asmdef
    Shaders/

  Documentation/
```

`HemanthGameDev.SurfaceWeaver.Runtime` contains serializable data and components only and must not reference `UnityEditor`. `HemanthGameDev.SurfaceWeaver.Editor` owns Scene View interaction, GPU resources, path editing, mask persistence, diagnostics and the editor UI.

## Target constraints

- Static `MeshFilter` + `MeshRenderer`.
- Readable mesh data and UV0 with one coordinate per vertex.
- UV0 coordinates inside normalized 0–1 for the current mask raster canvas.
- Non-convex `MeshCollider` referencing the same mesh for Scene View picking and path projection.
- Triangle submeshes are rasterized; non-triangle submeshes are skipped and reported.
- Overlapping UV charts cannot represent distinct masks in the same texels and must be resolved or accepted knowingly.

## Mask storage model

Packed RGBA pages store four independent grayscale surface masks:

- R = layer 0
- G = layer 1
- B = layer 2
- A = layer 3

Additional pages can hold more layers. Painting and mask operations must write only the selected channel, preserving all other channel values. Pages are linear RGBA32 RenderTextures during authoring and are explicitly saved as PNG assets.

## Seam-aware UV raster backend (v0.6)

`SurfaceUvRasterMesh` builds a temporary mesh by duplicating each source triangle into UV0 coordinates. The temporary mesh uses:

- `POSITION`: UV0 converted to a 0–1 raster surface.
- `TEXCOORD0`: original UV0, used to sample the mask page.
- `TEXCOORD1`: source mesh local-space position.
- `TEXCOORD2`: source triangle local-space face normal (constant for the three duplicated triangle vertices).

For each world-space brush stamp, `SurfaceGpuBrushEngine` copies the current page into the write target, then draws the temporary UV raster mesh into the write target. The shader transforms interpolated source positions to world space, transforms face normals using the inverse-transpose matrix, applies a world-space radial brush and a normal-alignment cutoff, and modifies only the selected mask channel. Duplicated geometry at a UV seam is represented as separate UV triangles, so every chart triangle under the same world-space brush footprint is stamped in its own UV coordinates.

The brush alpha frame is derived from a stable target-local axis projected onto each surface tangent plane. This gives duplicated seam vertices the same orientation for a given surface normal, rather than deriving orientation from UV chart directions.

### Backend limitations

- The backend currently paints UV0 only and rejects UV coordinates outside 0–1.
- Positive-area UV overlaps or stacked charts are ambiguous; overlapping surfaces update the same mask pixels.
- The normal alignment cutoff is fixed in v0.6; steep corners may require separate strokes. Nearby parallel surfaces with similar normals can still share a projected stamp because the current projection has no depth-thickness limit.
- Mask Smooth still samples a 3x3 UV-neighbourhood. It is not island-aware and may sample a neighbouring chart where UV gutters are too narrow.
- No automatic mask padding/dilation or mip-bleed correction is implemented.
- The complete temporary UV raster mesh is drawn per stamp, so painting cost scales with target triangle count and stamp count. Large meshes require profiling and later spatial/batched optimization.

## Spline path authoring (v0.6)

`SurfacePaintPathAsset` stores path points as target-local positions and normals, layer identifier, mask page/channel, brush parameters, brush references and a deterministic random seed. The Editor saves the target's `GlobalObjectId` as a string. It does not serialize a direct reference to a scene component from a project asset.

The path is previewed using Catmull-Rom interpolation. Intermediate samples are projected onto the target MeshCollider and applied as one GPU Undo stroke. Random brush rotation/scale uses the stored seed and restores the Unity global random state after path application.

A path asset is a reusable path recipe. Applying it writes to the active mask page; it is not a separate non-destructive material layer. Width is currently constant and based on the brush radius. Closed paths, draggable handles, variable width/pressure curves and multi-path editing are future work.

GlobalObjectId caveats: save the scene at least once before creating path assets; the referenced scene must be loaded to resolve the ID; moving the target to another scene changes the ID. If the ID cannot resolve, the Editor asks whether to use the currently selected target.

## Painting and Undo

- Each paint stroke stores one pre-stroke GPU snapshot for session Undo.
- Spline application is treated as one stroke.
- Ping-pong RenderTextures ensure the source and destination are never the same target for a Blit.
- Save mask page explicitly to persist PNG output.
- Random state is locally seeded/restored for repeatable spline stamp jitter.

## Rendering roadmap (historical v0.6 status)

At v0.6, the build authored mask pages only. A profile-driven URP multi-layer shader/material binder is planned for v0.7, with albedo/tint/tiling and explicit texture-sample budgets. Runtime profile/mask consumption may be provided, but runtime painting is outside the current release scope.


## v0.7 profile-driven URP rendering

`SurfacePaintProfile` now includes `SurfaceTextureMappingMode` for albedo sampling: Mesh UV0 or World Planar XZ. The mask remains on UV0 to match the current paint backend.

`SurfacePaintMaterialBinder` reads the profile and writes its values into a dedicated generated `.mat` asset assigned to one MeshRenderer material slot. It uses `sharedMaterials` (not `Renderer.material`) and does not use `MaterialPropertyBlock`; Unity documents that MaterialPropertyBlocks are not compatible with the SRP Batcher in URP. Use a distinct generated material for each distinct profile.

The v0.7 shader `HemanthGameDev/SurfaceWeaver/URP Layered Surface` blends one base albedo with up to four ordered overlay layers. The four overlay masks come from R/G/B/A channels of one packed RGBA page. The packed mask is sampled once per pixel and its channels are reused. The shader uses a shared `UnityPerMaterial` layout across UniversalForwardOnly, ShadowCaster and DepthOnly passes. `UniversalForwardOnly` renders through the forward path in both URP Forward and Deferred renderer configurations; a `DepthNormalsOnly` pass for SSAO and a Meta pass for lightmap baking are not included. It supports main light, SH ambient, optional additional lights, fog, shadow reception and shadow casting.

Texture mapping applies to albedo only: Mesh UV0 is suitable for props and curved meshes; World Planar XZ is for mostly horizontal environment surfaces and can stretch on vertical geometry. Mask sampling always uses UV0.

### v0.7 rendering limits

- One packed RGBA mask page and four overlay layers maximum.
- `SeparateTextures` storage is not supported by the v0.7 binder.
- One profile bound to one selected MeshRenderer material slot; multi-submesh binding is not implemented.
- No normal maps, metallic/smoothness maps, height blending, triplanar mapping or per-layer blend modes.
- Baked lightmap sampling is not implemented; ambient probe SH and direct URP lights are used.
- Mobile performance and shader variants must be profiled on target Android devices before any performance claim.
