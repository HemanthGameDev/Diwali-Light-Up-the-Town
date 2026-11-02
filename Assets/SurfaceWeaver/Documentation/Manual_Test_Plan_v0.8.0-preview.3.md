# SurfaceWeaver v0.8.0-preview.3 — Manual Test Plan

Requires the single-target spline workflow from v0.8.0-preview.2-hotfix.3. Back up the project and path assets before importing.

## 1. Compile and preserve existing behavior
1. Import the patch and wait for Unity compilation.
2. Confirm there are no C# Console errors.
3. Open a path created before preview.3.
4. Confirm anchors, custom tangents, closed paths and Apply Spline still work.
5. Confirm old paths report neutral Width Scale=1 and Pressure=1.

## 2. Per-anchor profile editing
1. Create at least three anchors on the active target.
2. Select P1, P2 and P3 from Edit Anchor.
3. Set different Width Scale values (e.g. 0.5, 1.0 and 2.0).
4. Set different Pressure values (e.g. 0.2, 0.7 and 1.0).
5. Confirm the Scene View width rails change smoothly between anchors.
6. Move anchors and edit tangents; confirm width/pressure values are preserved.

## 3. Pressure behavior
1. Enable Pressure Affects Width and Pressure Affects Opacity.
2. Apply the path and confirm narrow/low-pressure sections become narrower and lighter.
3. Disable Pressure Affects Width only; reapply and confirm width becomes independent of pressure.
4. Re-enable width, disable Pressure Affects Opacity; reapply and confirm width varies while opacity remains at the base brush opacity.
5. Set pressure to 0 at an anchor and confirm no visible stamp is applied at that exact point when pressure affects opacity.

## 4. Width behavior
1. Set Width Scale to 0.25, 1 and 3 on consecutive anchors.
2. Confirm the preview rails represent the effective brush radius.
3. Apply with seam-aware projection enabled.
4. Repeat with legacy UV projection enabled.
5. Inspect for gaps, abrupt changes or unexpected radius jumps.

## 5. Persistence and reset
1. Save the path asset.
2. Close and reopen the Painter.
3. Load the path asset and confirm per-anchor width/pressure values and pressure toggles persist.
4. Test Reset Width / Pressure and Reset All Profiles.
5. Confirm Reset Handles does not modify width/pressure.
6. Confirm resetting one narrow segment does not cause excessive stamping density on other wider segments.

## 6. Undo and edge cases
1. Apply a path and undo the mask operation.
2. Test open and closed paths.
3. Test a two-anchor path, a path with many anchors and repeated anchors close together.
4. Test non-uniform target scale.
5. Test minimum width 0.05, maximum width 4 and pressure 0/1.
6. Confirm no unexpected allocations occur in normal runtime; the spline tool is Editor-only.

Pass criteria: legacy paths remain unchanged, width/pressure are smooth and deterministic, saved assets preserve all values, and Apply Spline uses the profiles in both projection modes.
