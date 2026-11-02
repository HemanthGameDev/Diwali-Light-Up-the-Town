# SurfaceWeaver v0.8.0-preview.6.1 — Scatter Toggles & Live Placement Preview

## Changes
- Replaces weighted prefab selection with an explicit Enabled toggle per prefab entry. Enabled entries are selected uniformly; disabled entries are never selected. Legacy weight values remain serialized but are ignored.
- Adds non-destructive Scene View prefab ghosts before a stamp is committed.
- Preview placements use the same seeded placement generator as the eventual stamp, so click/drag commits the placements currently previewed.
- Profile changes (density, radius, slope, offset, scale, yaw, pitch/roll and prefab enablement) invalidate and rebuild the preview.
- Preview instances are hidden, non-persistent editor objects; their colliders and scripts are disabled and they are destroyed when the window closes or placement is stopped.
- Existing scene instances are not cleared or modified when preview settings change.

## Install
Apply this incremental patch over v0.8.0-preview.6. Back up the project first, close Unity, merge the `SurfaceWeaver` folder into `Assets/SurfaceWeaver`, then reopen Unity.

## Manual test plan
1. Open Object & Vegetation Scatter and load the existing Target Set and Scatter Profile.
2. Confirm each prefab entry has an Enabled checkbox. Disable one entry and verify it never appears in preview or committed stamps.
3. Move the cursor over a registered target. Confirm prefab ghosts appear before clicking.
4. Change brush radius, instances per stamp, scale range, random yaw, pitch/roll, slope and surface offset. Confirm the preview updates without clearing or modifying placed objects.
5. Click once and verify the committed stamp matches the visible preview.
6. Undo the stamp; confirm all instances from that stamp are removed together.
7. Stop Scene Placement or close the window; confirm preview ghosts disappear and do not remain in the scene or hierarchy.
8. Confirm existing placed objects, Painter, spline tools and Batch Export are unchanged.

## Scope notes
- This preview is a pre-commit placement preview. It does not retroactively reposition already committed instances when profile settings change. Existing placements remain unchanged by design.
- Preview ghosts instantiate prefab hierarchies temporarily in the Editor. Behaviours, colliders, audio and particle emission are disabled on ghosts. Very heavy prefabs may still make Scene View preview more expensive; use the preview toggle / stop placement when needed.
- Scatter creation remains Editor-only; committed instances are ordinary prefab instances.

## Validation status
Source and archive checks only. Unity compilation, visible Scene View ghost rendering, prefab side-effect safety, Undo and user workflow must be validated in the consuming Unity project.
