# Manual Test Plan — SurfaceWeaver v0.8.0-preview.4

## A. Preserve the stable Painter workflow

1. Open the normal SurfaceWeaver Painter with one active `SurfacePaintTarget`.
2. Create or load a mask page.
3. Create a spline, change width/pressure, apply the live preview, commit it, and save the mask.
4. Confirm the single-target Painter, anchor editing and live preview still work as before.

Pass: preview.4 adds no Surface Targets registration UI and does not change spline interaction.

## B. Batch export selected targets

1. In a test scene, create at least two GameObjects with `SurfacePaintTarget` components.
2. Assign each target a profile containing one or more saved PNG mask pages.
3. Multi-select both target GameObjects in the Hierarchy.
4. Open `Tools > SurfaceWeaver > Batch Export Masks`.
5. Click `Refresh From Selection`.
6. Confirm both targets, profiles and exportable pages are listed.
7. Choose an empty temporary output folder outside the project.
8. Keep per-target subfolders and CSV manifest enabled.
9. Export.

Pass: each source mask page is copied into the correct target folder; the manifest lists each export; no source asset is changed.

## C. Discover loaded scene targets

1. Open two scenes additively, each containing at least one `SurfacePaintTarget`.
2. Open the Batch Export window.
3. Click `Find All Loaded Scene Targets`.

Pass: targets from loaded scenes are listed; prefab assets and non-scene persistent assets are excluded.

## D. Collision and overwrite protection

1. Export the same selection twice into the same folder.
2. Confirm the second export uses numbered filenames instead of overwriting files.
3. Confirm the CSV manifest also receives a unique filename.

Pass: previous exports remain unchanged.

## E. Export into Assets

1. Choose a temporary folder inside the project's `Assets` directory.
2. Export the selected mask pages.
3. Wait for AssetDatabase refresh.

Pass: copied images appear as imported Texture2D assets with sRGB disabled, mipmaps disabled, Clamp wrapping and no compression. Source import settings are not modified and copies are not automatically assigned to profiles.

## F. Unsupported and missing pages

1. Include a profile with an empty mask page slot.
2. Include a non-file-backed texture or an unsupported source extension if available.
3. Refresh the export window.

Pass: non-exportable pages are skipped and the target entry explains why; valid pages from the same target remain exportable.

## G. Unsaved-preview safety

1. Start a live spline preview and adjust it without committing/saving.
2. Open Batch Export and review its warning.
3. Commit the preview and save the mask in the Painter.
4. Export again.

Pass: users are clearly told that only persisted mask-page files are copied; exported data matches the saved source file.

## H. No mutation of Painter target

1. Set one target as the Painter's active target.
2. Open Batch Export and export a different selection.
3. Return to the Painter.

Pass: the Painter's active target, selected path, and working session are unchanged.
