# SurfaceWeaver v0.9.0-preview.1 — Automated EditMode Test Foundation

## Scope
This preview starts the v0.9 quality programme with a Unity Test Framework EditMode assembly. It adds automated regression tests around deterministic logic already present in the v0.8 consolidated package. It does not claim full v0.9 compatibility or performance completion.

## Added test coverage
- Connected-region Flood Fill: selected-channel mutation, preservation of other RGBA channels, 4-/8-neighbour connectivity, seed-value tolerance, allowed-region restriction, and invalid input handling.
- UV-island mask builder: a geometric edge separated by a UV seam creates distinct allowed regions; invalid triangle seeds are rejected. UV validator tests cover normalized readable triangles, missing UV0 and invalid channel indices.
- Multi-Mesh target set: blank/duplicate handling, add/remove/remove-at/clear behavior, and AssetDatabase save/reload persistence.
- Spline path asset: point width/pressure values, channel, loop state, pressure behavior settings and deterministic seed are retained by Configure; path point values are clamped.

## Import
1. Back up or commit the current SurfaceWeaver package.
2. Merge the patch into `Assets/SurfaceWeaver`.
3. Ensure Unity Test Framework is available in Package Manager (`com.unity.test-framework`). The test assembly uses Unity's `TestAssemblies` optional reference.
4. Wait for compilation and inspect Console.

## Run tests
1. Open `Window > General > Test Runner`.
2. Select **EditMode**.
3. Find `HemanthGameDev.SurfaceWeaver.Editor.Tests`.
4. Run all tests.
5. Save the XML test report from Test Runner for CI/archive use.

## Current limits
- These are EditMode unit tests, not a guarantee of Scene View interaction or full GPU painting integration.
- Undo/Redo integration, AssetDatabase save/reload across domain reload, cross-target stroke interaction, scatter placement determinism and GPU preview lifecycle still need integration tests.
- No Unity compilation or test execution is possible from this patch-generation environment. These tests must be run in the consuming Unity project.
- Graphics API, URP version, device and large-mesh benchmark work remains for subsequent v0.9 previews.
