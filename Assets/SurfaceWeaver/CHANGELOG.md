# Changelog

## 0.2.0 – Scene View preview and UV0 picking

- Added target-specific Scene View ray picking through a non-convex MeshCollider.
- Added a world-space circular brush footprint aligned to the picked surface normal.
- Added live UV0, world position, world normal, and triangle-index diagnostics.
- Added an Editor action to create or refresh the target MeshCollider.
- Added explicit messages for missing/stale/convex colliders, unreadable meshes, and unsupported UV channels.
- No painting or mask data is modified in this milestone.

## 0.1.0 – Foundation

- Added reusable surface targets, profiles, layer definitions and brush preset data.
- Added first-pass UV validation and the initial Editor window.
