# SurfaceWeaver v0.6.5 — Cylindrical Projection Correctness

## Changes
- Seam-aware mesh projection now uses a 3D spherical reach test rather than only a planar radial disc.
- A mandatory surface-facing threshold is applied in seam-aware mode (default 0.2). This rejects far-side cylinder faces and perpendicular cap faces.
- A configurable projection-depth slab remains active to reject separate nearby surfaces along the brush normal.
- Rejected fragments and zero-contribution brush fragments now use `discard`, preventing a later rejected triangle from writing the original source pixel over an earlier accepted triangle at overlapping UV texels.
- Per-triangle geometric normals are oriented to agree with averaged authored vertex normals when those are available. This helps imported meshes with inconsistent triangle winding.
- Singular/zero-scale target transforms are rejected with a clear error before inverse-transpose normal matrix setup.

## Preserved
- Legacy UV painting path is unchanged.
- Existing ping-pong RenderTexture flow remains unchanged.
- RGBA channel-isolated writes remain unchanged.
- Existing session Undo/Redo snapshots remain unchanged.

## Not included
- UV island gutter padding/dilation and mip bleeding prevention.
- Depth-buffer visibility/occlusion testing for stacked surfaces with matching normals.
- GPU batching/performance optimization.

## Validation status
Static source checks and CPU-side projection-gate sanity checks were performed while packaging. This package has not been compiled in Unity or executed on D3D11/GPU. Unity Editor compilation and the included manual tests are mandatory before accepting this version.
