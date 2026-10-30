# SurfaceWeaver Manual Test Plan

## Foundation and profiles

1. Add `SurfacePaintTarget` to a GameObject with `MeshFilter` and `MeshRenderer`.
2. Create a `SurfacePaintProfile`, assign it to the target, and add paint-layer definitions.
3. Run UV diagnostics for UV0. Confirm finite coordinates, expected range, non-zero UV area, and triangle topology.

## Scene View preview and UV0 picking (v0.2.0)

1. Select the target and open **Window > SurfaceWeaver > Painter**.
2. Click **Add / Refresh MeshCollider**.
3. Move the pointer over the target in Scene View.
4. Confirm the ring follows the hit surface and uses the configured world-space radius.
5. Confirm UV0, world position, world normal, and triangle index update as the pointer moves.
6. Test flat, sloped, rotated, and non-uniformly scaled target meshes.
7. Confirm Alt navigation remains available.
8. Disable preview and confirm all Scene View guides disappear.
9. Confirm a target configured to use a nonzero UV channel displays the UV0-only warning.
10. Confirm missing, convex, stale-mesh, and non-readable MeshCollider configurations produce useful messages.

## Not implemented in v0.2.0

- GPU painting / RenderTexture ping-pong.
- Brush stamping, custom alpha brushes, stroke interpolation.
- Undo/Redo of painted strokes.
- PNG mask persistence and reload.
- UV overlap and padding/seam analysis.
- Runtime/mobile painting.

Run these checks in the target Unity Editor version before accepting a release build. The package has not been compiled in a Unity Editor as part of this source-generation step.
