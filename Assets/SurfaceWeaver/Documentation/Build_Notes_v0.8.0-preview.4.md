# SurfaceWeaver v0.8.0-preview.4 — Batch Mask Export

## Included

- Added a separate `Tools/SurfaceWeaver/Batch Export Masks` Editor window.
- Export multiple selected `SurfacePaintTarget` profiles in one operation without changing the Painter's active target.
- Added a helper to discover all `SurfacePaintTarget` components in currently loaded scenes.
- Exported mask pages are copied from their persisted source image files without decoding or re-encoding pixel data.
- Supported lossless image extensions: PNG, TGA, EXR, TIF and TIFF.
- Optional per-target output folders prevent collisions between targets with similarly named profiles/pages.
- Optional CSV manifest records target, profile, mask page index, source asset path, output path and dimensions.
- Export names are sanitized and collision-safe; existing files are not overwritten.
- Default export destination is a `SurfaceWeaver_Exports` folder beside the project `Assets` folder, keeping exported copies out of the project by default.
- Exporting into the project's `Assets` folder triggers an AssetDatabase refresh so copies are imported by Unity.

## Deliberate scope

- This release adds batch export only. It does not implement simultaneous editing or cross-target painting.
- The Painter remains single-active-`SurfacePaintTarget`, preserving the stable spline authoring workflow.
- Export reads persisted mask-page source files. It does not export unsaved GPU paint data or an uncommitted live spline preview. Commit the preview and save the mask in the Painter first.
- File-backed lossless image assets are supported. Unsupported/non-file-backed pages are skipped and reported in the target entry.
- No profile assets, source meshes, materials or target components are modified by the export operation.

## Workflow

1. Save the current Painter mask pages and commit any live spline preview.
2. Select multiple GameObjects with `SurfacePaintTarget` components in the Hierarchy.
3. Open `Tools > SurfaceWeaver > Batch Export Masks`.
4. Click `Refresh From Selection`, or use `Find All Loaded Scene Targets`.
5. Choose an output directory and export options.
6. Click `Export Mask Page(s)`.
7. Review the CSV manifest and exported per-target folders.

## Validation status

Source structure and archive checks are performed before distribution. Unity compilation, EditorWindow interaction, multi-selection behavior, file export, CSV validity and AssetDatabase refresh must be verified in the consuming Unity project using `Manual_Test_Plan_v0.8.0-preview.4.md`.
