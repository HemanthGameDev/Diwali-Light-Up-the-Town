# UV Diagnostics — v0.5.0

Open **Window > SurfaceWeaver > Painter**, assign a `SurfacePaintTarget`, then select **Validate UV Channel**.

## Report fields

- **Vertices / triangles / UV entries:** mesh and UV-channel counts.
- **Out-of-range UVs:** coordinates outside the 0–1 mask domain. This is a warning because some workflows intentionally tile UVs, but the current mask authoring workflow expects normalized UVs.
- **Degenerate UV triangles:** triangles with effectively zero UV area. These can be unpaintable or unstable.
- **Overlapping UV triangle pairs:** pairs with positive-area intersection in UV space. Shared edges or shared vertices alone are not counted. The count is triangle pairs, not island pairs.
- **UV seam edges:** geometric edges whose coincident mesh-local endpoints have more than one distinct UV mapping. This is diagnostic and does not imply the seam is automatically wrong; UV islands normally have seams.
- **Overlap candidate tests / analysis status:** the validator stops after five million candidate comparisons. If the status is Partial, do not treat the report as a complete no-overlap result.

## How the analysis works

Triangle UV bounds are sorted by minimum U. Candidate pairs with non-overlapping U or V bounds are skipped. Remaining pairs are tested with a 2D separating-axis test. Positive projection overlap on all triangle edge normals is required, avoiding reports for triangles that merely touch along an edge or vertex.

Geometric seam detection builds mesh-local edge keys from endpoint positions quantized to 1e-5 units. It then compares UV endpoint mappings after canonicalizing endpoint order.

## Known limitations

- The seam detector operates on coincident mesh-local edge positions; it is not a complete topology reconstruction for independently overlapping shells or near-coincident surfaces.
- It does not measure texel padding, island dilation, mip bleed, or filtering bleed.
- Overlap results may include intentional mirrored/stacked UVs. Review the mesh and painting intent before changing the unwrap.
- Overlap analysis is bounded and may be partial on meshes with extremely dense overlapping UV bounds.
- Validation does not modify or unwrap the mesh.
