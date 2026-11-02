# SurfaceWeaver v0.8.0-preview.7 — Connected-Region Mask Flood Fill

## Included
- Adds an explicit Flood Fill arm/cancel workflow to the existing single-target Painter.
- Click a point on the selected target mesh to use its UV0 coordinate as the seed pixel.
- Fills the connected region in the selected RGBA mask channel using a configurable seed-value tolerance.
- Supports 4-neighbour or 8-neighbour texture-pixel connectivity.
- Preserves the other three RGBA channels byte-for-byte.
- Applies the result as one mask-processing Undo step through the existing GPU engine.
- Uses the existing live material mask preview and Save Mask PNG workflow.
- Escape cancels the armed tool before a seed click.

## Important behavior and limits
- This first implementation is a UV texture-space connected-region fill, not a mesh-topology flood fill. A region is connected by neighboring mask texels; it can cross between UV islands if those islands touch in texture space. Review the preview before saving if UV islands are close or stacked.
- The active target must use UV0 and the selected mask page must be loaded.
- The tool performs GPU-to-CPU readback and uses additional temporary memory. The UI displays a rough staging estimate and the processor rejects buffers larger than 16,777,216 pixels.
- Other RGBA channels are preserved. Only the selected layer channel changes.

## Manual test plan
1. Open Painter and load a mask page for a selected layer.
2. Click Arm Flood Fill — Click Mesh.
3. Click a seed point on a painted or unpainted region of the selected mesh.
4. Verify only the connected same-value region in the selected channel changes.
5. Change tolerance and repeat with adjacent grayscale values.
6. Compare 4-neighbour and 8-neighbour behavior using diagonal contacts.
7. Verify other RGBA channels remain unchanged in the mask channel preview or exported PNG.
8. Undo and Redo the operation.
9. Save the mask PNG, reload the page and confirm persistence.
10. Press Escape while armed and confirm no mask changes occur.
11. Confirm normal brush painting, spline editing, batch export, multi-mesh workspace and scatter still work.

## Validation status
Source and archive checks only. Unity compilation, GPU readback, pixel orientation, Scene View seed picking, undo/redo and actual UV behavior must be validated in the consuming Unity project.
