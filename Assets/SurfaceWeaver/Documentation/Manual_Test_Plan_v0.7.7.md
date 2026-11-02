# Manual Test Plan — v0.7.7 UV Island Diagnostics

This patch extends **Window > SurfaceWeaver > Painter > Mesh Diagnostics > Validate UV Channel**. It is diagnostic-only: it must not change mesh vertices, UVs, indices, import settings, or mask textures.

## Before testing

1. Commit or back up the current project and duplicate the test scene.
2. Import the v0.7.7 patch into the project root and allow Unity to finish compiling.
3. Confirm there are no compiler errors in the Console.
4. For imported models, enable **Read/Write** in the model import settings and reimport before validation.
5. Open **Window > SurfaceWeaver > Painter**, select a `SurfacePaintTarget`, then open **Mesh Diagnostics** and press **Validate UV Channel**.

## Test A — Continuous plane

1. Create a Unity Plane or a quad made from two triangles with continuous UV0 coordinates.
2. Add/configure `SurfacePaintTarget` and run validation.

Expected:
- UV island count: **1**.
- Fully stacked, fully mirrored, and partial island overlap pairs: **0**.
- Triangle overlap pairs: **0**.
- Island bounds list contains one entry.

## Test B — Separate stacked UV islands

1. Create one mesh containing two spatially separated quads. They must not share geometric edges.
2. Give both quads the same square UV0 layout with the same orientation, for example `(0,0)`, `(1,0)`, `(1,1)`, `(0,1)`.
3. Run validation.

Expected:
- UV island count: **2**.
- Fully stacked island pairs: **1**.
- The overlap-pair details identify the two island IDs as **Fully stacked**.
- Fully mirrored island pairs: **0**.
- Partial island overlap pairs: **0**.

## Test C — Mirrored, fully overlapping UV islands

1. Duplicate Test B's mesh.
2. Keep the second quad spatially separate, but mirror its U coordinates (`U = 1 - U`) so its UV footprint still occupies the same square.
3. Run validation.

Expected:
- UV island count: **2**.
- Fully mirrored island pairs: **1**.
- The overlap-pair details identify the two island IDs as **Fully mirrored**.
- Fully stacked island pairs: **0**.
- Partial island overlap pairs: **0**.

## Test D — Partial island overlap

1. Create two spatially separate quads.
2. Give the first a UV square from `(0,0)` to `(0.6,0.6)`.
3. Give the second a UV square from `(0.3,0.3)` to `(0.9,0.9)` with the same orientation.
4. Run validation.

Expected:
- UV island count: **2**.
- Partial island overlap pairs: **1**.
- The overlap-pair details identify the two island IDs as **Partial overlap**.
- Fully stacked and mirrored island pairs: **0**.
- The UVs remain within the 0–1 range.

## Test E — A geometric seam splits islands

1. Create two adjacent triangles that share the same geometric edge positions, but duplicate the edge vertices and assign different UV coordinates on each side of that edge.
2. Ensure each triangle has non-zero UV area and the triangles do not overlap in UV space.
3. Run validation.

Expected:
- UV island count: **2**.
- UV seam edges: at least **1** for the shared geometric edge.
- Fully stacked, fully mirrored, and partial island overlap pairs: **0**.

## Test F — Ordinary production mesh regression

1. Validate the cylinder and the custom Blender mesh already used by the project.
2. Compare the new island and seam counts with the actual UV layout in Blender.
3. Open the Painter, paint on two separate UV islands, save the mask, close/reopen the Painter, and confirm existing painting/persistence behavior remains unchanged.

Expected:
- Diagnostics completes without errors or freezing.
- Island groups correspond to the visible UV shells.
- Existing painting, RGBA channels, live preview, undo/redo, and save/reload behavior are unchanged.

## Interpretation and limitations

- An island is a connected set of non-degenerate triangles joined across a shared geometric edge whose canonical UV edge endpoints match within the validator tolerance.
- **Fully stacked** means two separate islands have at least 98.5% mutual UV-area coverage and predominantly matching triangle UV orientation.
- **Fully mirrored** means two separate islands have at least 98.5% mutual UV-area coverage and predominantly opposite triangle UV orientation.
- Other positive-area island intersections are reported as **partial overlaps** instead of being forced into stacked or mirrored categories.
- Mirrored/stacked classification is aimed at overlapping UV islands that share mask texels. Spatially separated mirrored layouts are not classified as overlapping pairs.
- Geometric adjacency is inferred from mesh-local edge endpoint positions quantized to 1e-5 units. Coincident but topologically unrelated geometry can therefore be grouped as connected if it also has matching UV edge mappings; check unusual non-manifold or duplicated geometry manually.
- Island-pair bounds checks, island triangle-pair overlap checks, and triangle-overlap scans are each safety-bounded at five million candidate comparisons. A **Partial / safety limit reached** result is not a complete no-overlap result.
- This patch does not unwrap the mesh or change any asset data. UV gutter padding, mask dilation, and mip-bleed prevention remain separate v0.7 tasks.
