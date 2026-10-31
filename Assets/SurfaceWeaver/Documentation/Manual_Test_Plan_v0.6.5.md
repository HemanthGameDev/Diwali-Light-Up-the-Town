# SurfaceWeaver v0.6.5 — Manual Test Plan

Use a duplicate scene, duplicated SurfacePaintProfile, and brand-new black mask pages. Do not overwrite production masks.

## T0 — Shader and window initialization
1. Import v0.6.5 and wait for Unity compilation.
2. Open Window > SurfaceWeaver > Painter.
3. Confirm there are no shader compile errors and the selected target/profile load.
4. Confirm the seam-aware UI displays `Surface Facing Threshold` (default 0.2); there should be no optional normal-filter checkbox.
Pass: no new Console errors and mask page initializes.

## T1 — Plane baseline
1. Use a plane with UV0 in 0–1 and a non-convex MeshCollider referencing the same mesh as its MeshFilter.
2. Load a fresh black R-channel mask.
3. Enable Seam-Aware Mesh Projection. Set brush radius 0.2, opacity 1, hardness 1, projection depth 0.75x radius, facing threshold 0.2, and no custom alpha brush.
4. Paint one small dab at two separated positions.
Pass: both dabs appear in the correct locations; no rotation or mirrored output.

## T2 — Standard cylinder side and far-side rejection
1. Use a cylinder with a conventional UV layout: one rectangular side chart and two circular cap charts, with a longitudinal side seam.
2. Use a fresh black R-channel mask.
3. Set radius 0.15–0.25, opacity 1, hardness 1, projection depth 0.75x radius, facing threshold 0.2.
4. Paint a dab on the visible side, away from the seam. Inspect the cylinder and saved mask.
5. Paint a second dab centred on the longitudinal seam.
Pass: the visible surface receives paint; the opposite side and perpendicular caps do not receive paint; a seam-centred dab appears on both matching side-chart edges and looks continuous on the 3D cylinder.

## T3 — Smart UV cylinder
1. Use a separate cylinder with Smart UV islands inside 0–1 and no unintended overlaps.
2. Repeat the same small-dab and seam-adjacent tests.
Pass: only the physically intended surface region receives paint. Separate islands may contain separate UV patches representing the same physical stroke.

## T4 — RGBA isolation and undo
1. Paint R, then G, then B, then A on fresh mask pages/snapshots.
2. Compare channels before/after.
3. Undo and redo a completed stroke.
Pass: only the selected channel changes; Undo/Redo restores the expected mask.

## T5 — Save/reload orientation
Save the page PNG, close/reopen the Painter window, reload the mask, and inspect both selected-channel preview and target surface.
Pass: orientation and placement are unchanged.

## Report back
Send screenshots of: (a) Plane + selected channel mask, (b) cylinder front view + saved mask, (c) cylinder seam view + saved mask, and any Console errors. If the cylinder still fails, include the facing threshold, brush radius, projection depth multiplier, and whether the visible-side-only dab succeeded.
