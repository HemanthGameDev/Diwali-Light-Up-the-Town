# Manual Test Plan — v0.9.0-preview.1

## A. Automated EditMode tests
- [ ] Unity compiles without errors after import.
- [ ] Unity Test Framework is installed/enabled.
- [ ] `HemanthGameDev.SurfaceWeaver.Editor.Tests` is discovered in Test Runner.
- [ ] All EditMode tests pass.
- [ ] XML test result is saved.

## B. v0.8 integration smoke tests (not automated by this preview)
- [ ] Open the standard single-target Painter and confirm the active target remains selected.
- [ ] Create, edit, save and reload a spline with custom tangents, width and pressure.
- [ ] Start a live spline preview, adjust width/pressure, Cancel, and verify original mask restoration.
- [ ] Commit a live spline preview and verify Undo/Redo.
- [ ] Open a Multi-Mesh Target Set and validate all resolved scene targets.
- [ ] Paint across two registered meshes; verify each mask and grouped Undo/Redo.
- [ ] Run Batch Mask Export and check manifest/output files.
- [ ] Scatter using weighted prefab selection; test spacing, slope, exclusions and Undo.
- [ ] Change live-update scale/rotation on metadata-backed instances; verify adopted legacy instances are treated per documented limitations.
- [ ] Flood Fill with 4-/8-neighbour, tolerance, selected channel and UV-island restriction.
- [ ] Save and reload changed mask pages.

## C. Capture evidence
Record Unity version, URP version, package version, test count/pass/fail, Console errors, and attach Test Runner XML. Do not mark v0.9 compatibility as complete from EditMode unit tests alone.
