# Manual Test Plan — v0.8.0-preview.6.2

- [ ] Unity compiles without errors.
- [ ] Prefab entries show Selection Weight; no Enable toggle remains.
- [ ] Weight 0 excludes an entry from preview and committed placement.
- [ ] Weights 1:3 produce approximately proportional selection over a large sample.
- [ ] All-zero weights disable Paint mode with a clear explanation.
- [ ] Temporary prefab preview still refreshes when profile settings change.
- [ ] Newly placed instances receive SurfaceScatterInstanceData.
- [ ] Changing per-entry min/max scale updates existing metadata-backed instances.
- [ ] Changing Global Scale X/Y/Z updates existing instances as width/height/depth.
- [ ] Changing rotation offset/randomization updates existing metadata-backed instances deterministically.
- [ ] Changing surface offset moves existing instances along their stored surface normal.
- [ ] Live Update Existing Instances can be disabled.
- [ ] Adopt Existing Instances adds metadata to prior prefab placements without visibly changing their current appearance.
- [ ] After adoption, changing scale/rotation/surface offset updates the adopted placements; inspect any complex parent/non-uniform-scale prefabs manually.
- [ ] Undo restores transforms after Update Existing Now.
- [ ] Weight, brush radius and density do not unexpectedly replace already placed prefabs.
- [ ] Existing Painter, spline, multi-mesh painting and Flood Fill still work.

Note: brush radius, density, spacing and count changes affect future stamps only in this preview.
