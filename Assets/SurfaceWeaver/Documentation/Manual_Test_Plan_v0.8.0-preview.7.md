# Manual Test Plan — v0.8.0-preview.7

- [ ] Unity compiles without errors.
- [ ] Arm and Escape-cancel Flood Fill work.
- [ ] Clicking the mesh uses the expected UV0 seed.
- [ ] 4-neighbour fill remains inside edge-connected region.
- [ ] 8-neighbour fill includes diagonal neighbours.
- [ ] Tolerance includes nearby grayscale values as expected.
- [ ] Only the selected RGBA channel changes.
- [ ] Other three channels remain byte-identical.
- [ ] Undo restores the original mask.
- [ ] Redo restores the filled result.
- [ ] Save/reload preserves the result.
- [ ] Oversized buffers show a clear error instead of allocating excessively.
- [ ] Existing painting and authoring tools remain unaffected.
