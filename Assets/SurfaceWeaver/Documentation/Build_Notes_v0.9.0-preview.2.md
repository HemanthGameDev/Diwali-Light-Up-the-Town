# SurfaceWeaver v0.9.0-preview.2 — Undo/Redo and Asset Persistence Integration Tests

## Scope
This preview extends the v0.9.0-preview.1 EditMode suite with integration tests for the GPU mask session history and AssetDatabase-backed authoring data. It is test coverage, not a change to the production painting algorithms.

## Added coverage
- GPU stroke history: one completed stroke is one Undo entry; Undo and Redo restore the expected mask channel values.
- Multi-operation strokes: several channel operations within one pending stroke remain one history entry.
- Cancel: restores all original RGBA values and creates no history entry.
- Pending operation guards: Undo/Redo are rejected while a stroke is active.
- History branching: committing a new stroke after Undo discards stale Redo history.
- CPU-processed mask upload: ApplyPixelChanges is reversible through the GPU session history.
- Multi-target session foundation: two independent GPU sessions can both undo and redo their part of one logical cross-target stroke. The Multi-Mesh Painter's actual Scene View input and group controller still require manual integration testing.
- Mask PNG persistence: mask pixels are saved to PNG, imported with the SurfaceWeaver mask importer contract, and referenced by the profile after asset reload.
- Spline path persistence: anchors, custom tangents, width/pressure, channel, closed-loop state and brush settings survive an AssetDatabase save/reload.
- Scatter profile persistence: prefab reference, weight, per-prefab scale range and global randomization settings survive save/reload.

## GPU test behavior
The GPU-backed tests use the same internal `SurfaceGpuBrushEngine` class used by the Painter. They require the `Hidden/SurfaceWeaver/MaskStamp` shader and an ARGB32-capable graphics device. Tests call `Assert.Ignore` when the EditMode test host is headless or the required graphics resource is unavailable; skipped GPU tests must be reported separately from passed tests.

## Install
1. Commit or back up the currently working SurfaceWeaver v0.9.0-preview.1 package.
2. Merge this patch into `Assets/SurfaceWeaver`.
3. Ensure Unity Test Framework (`com.unity.test-framework`) is installed.
4. Allow Unity to compile and inspect Console before running tests.

## Run
1. Open `Window > General > Test Runner`.
2. Select EditMode.
3. Run `HemanthGameDev.SurfaceWeaver.Editor.Tests`.
4. Export the NUnit XML report.
5. Record passed, failed, and skipped counts separately. GPU tests skipped because of a headless test host are not a pass.

## Manual integration work still required
- Verify actual Scene View brush input and the Multi-Mesh Painter's grouped Undo/Redo across registered targets.
- Verify scatter placement, erase and live existing-instance update Undo against real prefab instances.
- Verify live spline preview Commit/Cancel and Undo in the Painter window.
- Verify domain reload and editor restart with real user assets and scenes.
- Verify mask data on a real project asset and on the OnePlus Android device after save/reload.

## Validation status
Source and archive checks only in the patch-generation environment. Unity compilation and execution must be performed in the consuming project.
