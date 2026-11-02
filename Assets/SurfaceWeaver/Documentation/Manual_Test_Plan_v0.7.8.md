# Manual Test Plan — v0.7.8 UV Overlap Classification

This patch refines the existing **Window > SurfaceWeaver > Painter > Mesh Diagnostics > Validate UV Channel** report. It is diagnostic-only and must not modify mesh vertices, UVs, indices, import settings, or mask textures.

## Before testing

1. Back up/commit the project and duplicate your test scene.
2. Import the v0.7.8 patch into the project root and allow Unity to compile.
3. Confirm there are no compiler errors in the Console.
4. For imported models, enable **Read/Write** in model import settings and reimport before validation.
5. Open **Window > SurfaceWeaver > Painter**, select a `SurfacePaintTarget`, expand **Mesh Diagnostics**, and click **Validate UV Channel**.

## Test A — Continuous plane

Use a quad made from two triangles with continuous UV0 coordinates.

Expected:
- Connected UV islands: **1**.
- Fully stacked, fully mirrored, contained and partial island overlap pairs: **0**.
- Overlapping UV triangle pairs: **0**.
- Same-island mirrored/same-orientation overlap pairs: **0**.

## Test B — Separate island overlaps

Use a mesh with three connected UV islands and two overlap relationships:
- Pair 1 partially intersects another island, with neither island fully covered.
- Pair 2 has one smaller island completely covered by part of a larger island.

Expected:
- The first pair is **Partial overlap**.
- The second pair is **Contained overlap (one island fully covered)**, not fully stacked.
- Fully stacked and fully mirrored counts remain **0** unless the two complete island footprints match.
- For the user-tested mesh from v0.7.7, the reported pair coverage was approximately 24.6% / 12.3% and 50.0% / 100.0%; after this patch the second pair should move to the **Contained** count while the first remains **Partial**.
- Any out-of-range UV warning remains a separate issue; the screenshot test mesh had two UV coordinates slightly outside 0–1.

## Test C — Separate fully stacked islands

Create two spatially separate quads with identical square UVs and the same orientation. They must not share geometric edges.

Expected:
- Connected UV islands: **2**.
- Fully stacked island pairs: **1**.
- Fully mirrored and partial/contained pair counts: **0**.

## Test D — Separate mirrored islands

Duplicate Test C and mirror the second quad's U coordinates (`U = 1 - U`) so its footprint still occupies the same square. Keep the quads spatially separate.

Expected:
- Connected UV islands: **2**.
- Fully mirrored island pairs: **1**.
- Fully stacked and partial/contained pair counts: **0**.

## Test E — Mirror modifier with connected geometry

Repeat the user's test: cut a plane in half, mirror it across the center seam, apply the modifier, and keep the generated halves connected at the center edge with matching seam UVs.

Expected for the specific four-triangle test mesh that produced two positive-area overlaps in v0.7.7:
- Connected UV islands may remain **1** because the mapping is connected along the center seam.
- The triangle overlap count remains **2**.
- **Mirrored overlaps within islands** should report **2** opposite-orientation triangle overlap pairs.
- **Fully mirrored island pairs** can correctly remain **0** because this is an internal overlap within one connected island, not a pair of separate islands.
- The **Overlaps Within Connected Islands** foldout should identify the island and triangle pairs as opposite-orientation mirrored-overlap candidates.

## Test F — Partial overlap

Create two spatially separate quads. Give the first a UV square from (0,0) to (0.6,0.6), and the second a UV square from (0.3,0.3) to (0.9,0.9), with matching orientation.

Expected:
- Connected UV islands: **2**.
- Partial island overlap pairs: **1**.
- Fully stacked, fully mirrored and contained pairs: **0**.

## Test G — Geometric seam

Create two adjacent triangles sharing the same geometric edge positions but with duplicated edge vertices and discontinuous UV coordinates. Ensure the triangles have non-zero UV area and do not overlap in UV space.

Expected:
- Connected UV islands: **2**.
- UV seam edges: at least **1**.
- Island overlap and same-island overlap pair counts: **0**.

## Test H — Production mesh regression

Validate the existing cylinder and custom Blender mesh. Confirm that the island/seam counts agree with the UV layout in Blender. Then open Painter and verify painting, live RGBA preview, undo/redo, and mask save/reload remain unchanged.

## Important interpretation notes

- **Fully stacked / fully mirrored island pair:** both separate islands have at least 98.5% mutual UV-area coverage and predominantly matching/opposite orientation.
- **Contained overlap:** one island is at least 98.5% covered, but the other is not; one island lies completely within only part of the other island's UV coverage.
- **Partial overlap:** neither island is almost fully covered by the intersection.
- **Overlap within connected island:** overlapping triangles belong to the same connected island. Opposite signed UV orientation is reported as a mirrored-overlap candidate; matching orientation as a same-orientation overlap. This is a warning clue, not proof of artist intent.
- The scan has safety limits. A **Partial / safety limit reached** result is not a complete no-overlap result.
- This patch does not unwrap or modify UVs. Automatic gutters, mask dilation and mip-bleed prevention remain separate v0.7 tasks.
