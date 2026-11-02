# SurfaceWeaver v0.8.0-preview.6.2 — Weighted Scatter & Live Update Existing Instances

## Included
- Restores weighted prefab selection. Weight 0 excludes a prefab; larger weights increase selection probability among valid entries.
- Removes the Enabled/Disabled-only UI.
- Adds global non-uniform scale multipliers: X width, Y height, Z depth.
- Stores deterministic per-instance random values and placement surface data.
- Optional Live Update Existing Instances refreshes compatible placed instances after profile edits.
- Adds Adopt Existing Instances for objects created by earlier scatter previews.
- Keeps the temporary prefab ghost preview for the next stamp.

## Live update behavior
- Per-prefab minimum/maximum scale, global X/Y/Z scale, rotation offset/randomization, surface alignment and surface offset update newly metadata-backed instances.
- Existing legacy instances can be adopted. Adoption estimates their prior uniform scale variation and random Euler offsets from the prefab source transform and their current scene transform, then stores normalized values for future live updates. This reconstruction is approximate for prefabs with non-uniform source scale or complex parent transforms; inspect adopted placements before saving.
- Weight affects future prefab selection only; it does not replace existing prefabs.
- Brush radius, density/instances per stamp, stroke spacing and minimum distance affect future stamps only. Changing those settings does not automatically add/remove/reposition prior placements.
- Preview ghosts remain temporary and do not enter the scene or get saved.

## Compatibility
Existing scatter profiles keep their saved weight values. The old `enabled` field is no longer used. Existing pre-preview.6.2 scatter instances should be adopted using the workspace button before expecting live updates.

## Validation
Source-level patch generated against the preview.6.1 scatter implementation. Unity compilation and Scene View testing are required in the consuming project.
