# Manual Test Plan — SurfaceWeaver v0.8.0-consolidation.1

Run in a backup copy of the target Unity 6 URP project. Use duplicate profiles, target sets, scatter profiles, path assets and mask pages for destructive tests. Record Console output and screenshots for every failure.

## A. Import and compatibility
- [ ] Import/merge the consolidated `SurfaceWeaver` folder into `Assets/SurfaceWeaver`.
- [ ] Unity compiles without errors; inspect new warnings.
- [ ] Existing v0.7 profiles, mask pages, brush presets, path assets and target components deserialize.
- [ ] Editor windows open after domain reload; no duplicate menu items or missing script references.
- [ ] Existing brush paint, erase, smooth, fill/clear, Save PNG, Undo and Redo remain functional.

## B. Single-target spline authoring
- [ ] Create P1/P2/P3 on the active target.
- [ ] Move each earlier and later anchor independently after all points exist.
- [ ] Select points using Scene View handles and Inspector anchor selector.
- [ ] Edit incoming/outgoing Bézier handles; reset handles without resetting unrelated settings.
- [ ] Save/reload Path Asset; verify points and handles persist.
- [ ] Verify open and closed paths, including the closing segment.
- [ ] Verify anchor movement remains restricted to the active target workflow; no Surface Targets registration UI appears.

## C. Width, pressure and live spline preview
- [ ] Set different width/pressure values on three anchors; verify smooth interpolation along the path.
- [ ] Toggle pressure effects for width and opacity independently.
- [ ] Start live preview; edit width, pressure, anchors, handles and relevant brush parameters.
- [ ] Verify each refresh rebuilds from the pre-preview mask baseline without cumulative opacity buildup.
- [ ] Commit preview and verify one logical Undo operation.
- [ ] Undo/Redo the committed path; Cancel a separate preview and verify original mask restoration.
- [ ] Save/reload the mask and path asset.

## D. Batch mask export
- [ ] Select multiple targets with distinct persisted mask pages.
- [ ] Export to an external folder with per-target subfolders.
- [ ] Verify files are copied losslessly and filenames are collision-safe.
- [ ] Verify CSV manifest columns and paths.
- [ ] Export a second time; confirm existing files are not overwritten.
- [ ] Confirm unsaved GPU changes/live previews are not exported; save/commit and repeat.

## E. Multi-Mesh Workspace
- [ ] Create a Target Set asset and add selected SurfacePaintTargets from saved scenes.
- [ ] Find targets in loaded scenes; remove an entry without deleting/modifying scene objects.
- [ ] Save, close/reopen Unity, reload scenes and verify GlobalObjectId resolution.
- [ ] Test unresolved entries for unloaded scenes; verify they are not silently pruned.
- [ ] Validate active state, profile, readable mesh, renderer, UV0 and matching collider diagnostics.
- [ ] Open each target in the existing single-target Painter; save before switching.
- [ ] Confirm shared-profile warnings are actionable.

## F. Multi-Mesh Painter
- [ ] Configure at least two ready targets with unique saved mask-page assets.
- [ ] Paint, erase and smooth on each target.
- [ ] Drag continuously across a shared screen-space boundary; confirm samples are routed to the hit registered target.
- [ ] Verify each target preview updates independently.
- [ ] Undo/Redo a cross-target stroke as one grouped operation.
- [ ] Press Escape during an active cross-target stroke; verify all touched target sessions revert.
- [ ] Save All Masks, close/reopen workspace and verify persistence.
- [ ] Reject shared mask-page assets, mismatched colliders, missing material properties, unreadable meshes and unsupported mask configurations with clear messages.
- [ ] Return to single-target Painter; verify it reloads saved mask state and still works.

## G. Weighted scatter and live existing-instance update
- [ ] Create/load a Scatter Profile with at least three prefab entries and different weights.
- [ ] Verify weight 0 excludes an entry and non-zero weights affect relative selection frequency over many stamps.
- [ ] Verify deterministic preview ghosts match the committed stamp.
- [ ] Verify ghosts disappear when preview/placement is disabled and never become scene children.
- [ ] Test random uniform scale, non-uniform global X/Y/Z scale, yaw, optional pitch/roll, surface alignment and offset.
- [ ] Test slope, spacing, exclusion layer and exclusion radius.
- [ ] Enable Live Update Existing Instances; modify supported per-prefab scale/rotation/alignment/offset values and verify metadata-backed instances update.
- [ ] Verify weight changes do not replace existing prefab identities.
- [ ] Verify density, brush radius, spacing and instances-per-stamp affect future stamps only.
- [ ] Adopt preview.6/6.1 legacy instances; inspect the estimated reconstructed values before saving.
- [ ] Undo scatter stamps; verify prefab links, hierarchy and scene persistence after save/reopen.

## H. Connected-region UV-island-aware Flood Fill
- [ ] Use a duplicate target/profile/mask page at 1024² or 2048² initially.
- [ ] Create/import a mesh with adjacent UV charts separated by a true UV seam; paint the same channel value on both charts.
- [ ] Enable Restrict to Seed UV Island; fill the first chart and verify the second remains unchanged.
- [ ] Disable restriction; verify legacy texture-space behavior can cross a contiguous texel connection.
- [ ] Verify 4-neighbour vs 8-neighbour diagonal connectivity.
- [ ] Verify tolerance and fill value.
- [ ] Compare R/G/B/A before and after; only the selected channel may change.
- [ ] Verify clicked triangle maps to the correct texel on rotated/flipped UV triangles.
- [ ] Test thin/small charts at 1024² and 2048²; note raster coverage.
- [ ] Test unreadable mesh and mismatched MeshCollider.sharedMesh; expect clear errors.
- [ ] Undo/Redo, Save PNG, reload and compare output.
- [ ] Press Escape while armed; verify no change.

## I. Mobile and performance regression
- [ ] Open the retained `SW_MobileBenchmark` scene and verify its target material/profile are unchanged.
- [ ] Profile on OnePlus CPH2467 using Vulkan; record CPU/GPU frame times, memory, batches and SetPass calls.
- [ ] Compare base-only, 1-, 2- and 4-overlay cases under the same camera/resolution/lighting.
- [ ] Run a realistic sample scene containing painted surfaces and scattered instances.
- [ ] Run a stress scene with more objects/large mesh and monitor memory and thermal behavior.
- [ ] Confirm Editor-only authoring tools do not add runtime MonoBehaviours to built scenes unless explicitly intended.

## Acceptance rule
Do not tag `v0.8.0` until all applicable tests pass or have documented, accepted exceptions. Any regression in the single-target Painter or spline workflow blocks release. Record Unity version, URP package version, graphics API, device model, test asset, and evidence for every failed/waived test.
