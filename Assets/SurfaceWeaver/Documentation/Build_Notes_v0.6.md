# SurfaceWeaver v0.6.0 — Original Build Notes (superseded by v0.6.1)

## Summary

Version 0.6 introduces world-space UV raster projection for seam-aware paint/erase/smooth stamping and spline/path authoring. It continues to author linear RGBA mask pages; it does not yet generate the final profile-driven multi-layer URP material.

## New and changed files

### Runtime
- `Runtime/SurfacePaintPathAsset.cs`: reusable path asset containing target GlobalObjectId text, target name, target-local points/normals, layer/page/channel, brush settings, alpha override, operation flags and random seed.

### Editor
- `Editor/SurfaceUvRasterMesh.cs`: creates a temporary triangle mesh in UV0 space with original local positions and per-triangle face normals in UV channels 1 and 2. It processes triangle submeshes and reports skipped non-triangle submeshes. It rejects non-finite and out-of-range UV0 coordinates.
- `Editor/SurfaceGpuBrushEngine.cs`: new mesh-projection stamp entry point. It copies the current mask to the write target and draws the temporary UV raster mesh into the mask target.
- `Editor/Shaders/SurfaceMeshMaskStamp.shader`: reconstructs world positions and normals, applies the brush in world space and modifies only the selected RGBA channel.
- `Editor/SurfaceWeaverWindow.cs`: seam-aware freehand painting, Catmull-Rom path authoring, path asset persistence and loading.

## Seam-aware projection model

The raster mesh duplicates source triangles, not shared vertices. Each triangle is positioned in the mask using UV0 while carrying its original local positions and local face normal. The brush is evaluated in world space. At a geometric UV seam, the same physical area is therefore stamped in each UV chart that represents it.

The current implementation uses a per-triangle face normal and a fixed dot-product cutoff (`0.15`) to limit projection onto nearby surfaces with very different orientations. It does not yet apply a normal-depth/thickness limit, so nearby parallel surfaces with similar normals may also receive the stamp. A stable tangent frame is derived from target-local X (with fallbacks) so brush alpha orientation does not follow each UV island's rotation.

## Path authoring

1. Save the scene at least once.
2. Enable Path Authoring in the Painter window.
3. Click two or more points on the target mesh in Scene View.
4. Use Backspace to remove the most recent anchor or Clear Anchors to start again.
5. Save Path Asset to store the path and current brush recipe, or Apply Spline to paint.
6. Applying the spline is a single GPU Undo step. Save the mask page to persist the output.

The path asset stores a `GlobalObjectId` as text. The scene containing that object must be loaded for automatic resolution. Moving the target to another scene changes its GlobalObjectId; if it cannot be resolved, loading requires an explicit choice to use the currently selected target.

## Compatibility and constraints

- Unity 6 + URP is the current target.
- Static MeshFilter/MeshRenderer only.
- UV0 only and 0–1 domain only.
- Non-convex MeshCollider must use the same mesh as the MeshFilter.
- Read/Write mesh data must be enabled.
- Triangle submeshes are rasterized; non-triangle submeshes are skipped/reported.
- Overlapping UV charts cannot store independent masks.
- Smooth remains UV-neighbourhood based; use it away from tight UV chart gaps.
- No UV padding/dilation or mip bleed correction yet.
- One mesh target at a time.

## Performance notes

One full temporary UV raster mesh draw is issued per stamp. This allows the stamp to hit all UV charts corresponding to the same world-space surface region, but its cost grows with source triangle count and number of stamps. Profile large meshes and long paths; this is a development backend, not a claim of optimized large-world throughput.

## Validation status

Static source and package checks were performed in the packaging environment. Unity Editor C# compilation, URP shader compilation, graphics API correctness and runtime painting have not been run here. Use `Manual_Test_Plan_v0.6.md` in a backup Unity project before adopting it in a production scene.
