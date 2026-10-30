# Manual Test Plan - SurfaceWeaver 0.3.0

Run these tests in a copy/branch of the Unity project. Confirm the Console contains no shader or C# compile errors.

## Setup

1. Import/merge the package files under `Assets/SurfaceWeaver`.
2. Assign a readable mesh with UV0, MeshFilter and MeshRenderer to SurfacePaintTarget.
3. Add/refresh a non-convex MeshCollider referencing the exact MeshFilter shared mesh.
4. Create and assign a saved SurfacePaintProfile. Set Mask Storage to Packed RGBA Pages.
5. Add one overlay layer; verify it maps to page 0, channel Red.
6. Ensure the target material has the texture property entered in Preview Shader Property (default `_PaintMask`).
7. Create the black mask page and load it.

## Tests

- Hover: brush ring follows the picked mesh surface and UV0 readout changes with cursor position.
- Paint: left mouse drag paints white into only the selected channel.
- Erase: erase returns only the selected channel toward black.
- Channel preservation: paint Red, then select a layer on Green and paint; verify Red remains unchanged.
- Continuous strokes: drag quickly; the stroke should not show obvious gaps at default spacing.
- Spacing: increase spacing and confirm stamps become more separated.
- Hardness: 1 creates a hard round edge; lower values create a soft falloff.
- Opacity: low opacity paints gradually; high opacity reaches white quickly.
- Custom brush: assign a grayscale alpha texture; white areas paint and black areas do not.
- UV seam guard: move across a UV island discontinuity; stroke interpolation should break instead of drawing across the UV atlas.
- Live preview: working GPU mask appears through the configured renderer material property.
- Clear selected channel: only the selected RGBA channel is reset.
- Save and reload: save mask PNG, close/reopen the window, reload the page and verify the result persists.
- Profile references: verify the saved Texture2D is assigned to the correct profile Mask Pages index.
- Non-uniform scale: try a mesh with non-uniform object scale and verify the world brush remains approximately circular.
- Slope: paint across gently sloped and curved triangles and inspect brush scale.

## Expected blockers / report back

Capture the first Console error with its full stack trace. Also capture the Scene View during paint, the selected profile layer's mask page/channel, and the mask PNG preview after saving.
