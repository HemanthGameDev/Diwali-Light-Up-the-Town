# UV Seam-Aware Projection — v0.6.0

## Why this backend exists

A single UV stamp can stop at a chart boundary even though the corresponding 3D surface continues across a seam. v0.6 evaluates the brush in world space while rasterizing every source triangle in its UV0 location.

## Temporary UV raster mesh

For each source triangle, SurfaceWeaver adds three independent raster vertices. Each vertex contains: UV0 in POSITION, original UV0 for mask sampling, source local-space vertex position in TEXCOORD1, and the triangle's local-space face normal in TEXCOORD2. The temporary mesh is not written to the source imported mesh asset.

Each triangle is drawn into the mask texture at its UV0 location. The fragment shader reconstructs world position and world face normal. In v0.6.5 it applies an always-on face-facing threshold, a 3D spherical distance test, and a projection-depth slab before computing the radial/alpha stamp and writing the selected RGBA channel.

## Why seam duplicates match

At a geometric seam, the source mesh may contain duplicate vertices with different UV0 coordinates but matching local positions. Those vertices become separate triangles at separate mask locations. Since the brush is evaluated from their matching world-space positions and face normals, the brush updates both corresponding UV chart locations when they fall under the same world-space brush footprint.

## Stable alpha orientation

The brush tangent basis is built from target-local X transformed to world space and projected onto each face tangent plane, with a local-Z fallback. This avoids using UV chart directions, which may rotate or mirror between islands. The face normal is transformed with the inverse-transpose matrix to support non-uniform object scale.

## Important constraints

- UV0 only, normalized to 0–1.
- Overlapping or stacked UV charts are ambiguous because they share output texels.
- The face-facing threshold is configurable in the Editor window and defaults to 0.2. Nearby surfaces with similar facing directions can still be affected if they are within both the sphere and projection-depth slab; a depth-buffer visibility test is not included.
- Smooth remains a 3x3 UV-neighbourhood filter; it is not island-aware. Avoid smoothing next to tightly packed charts.
- No automatic gutter dilation/padding or mip bleed correction.
- One full UV raster mesh draw per stamp: large meshes and high-density strokes can be expensive.
- The method is designed for static MeshFilter/MeshRenderer targets, not skinned meshes.

## Cylinder test setup

For a conventional Blender cylinder unwrap, mark three seam cuts: the top ring, the bottom ring, and one longitudinal edge from top to bottom. Unwrap to one rectangular side island plus two circular cap islands. A ring-only seam layout does not fully open the cylindrical side surface. For the first seam-aware test, use a small brush radius (0.15–0.25 world units for a cylinder approximately 1 unit in diameter), opacity 1, hardness 1, projection depth 0.75x radius, and facing threshold 0.2. Test a side dab away from the seam before testing a dab directly on the longitudinal seam.

## v0.6.5 projection behavior

The spherical distance check bounds the stamp in 3D. The face-facing threshold limits the stamp to triangles whose geometric face normal points sufficiently toward the picked surface. The projection-depth slab remains as an additional filter for nearby parallel surfaces. Rejected fragments use HLSL `clip` so they do not overwrite an accepted write already present in the destination RenderTexture. Per-triangle geometric normals are oriented to agree with averaged authored vertex normals when these are available.

UV gutter padding/dilation is not implemented in v0.6.5; narrow UV gutters can still show filtering seams.
