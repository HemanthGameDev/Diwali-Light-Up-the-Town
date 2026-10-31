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

## Rendering roadmap

The v0.6 build authors mask pages only. A profile-driven URP multi-layer shader/material binder is planned for v0.7, with albedo/tint/tiling and explicit texture-sample budgets. Runtime profile/mask consumption may be provided, but runtime painting is outside the current release scope.
