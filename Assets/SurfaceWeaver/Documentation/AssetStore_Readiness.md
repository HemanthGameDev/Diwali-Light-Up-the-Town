# Asset Store Readiness – Internal Checklist

This is an internal checklist. It is not a guarantee of acceptance.

## Product quality

- [ ] Package imports into a clean Unity project without errors or warnings from package content.
- [ ] Demo scene demonstrates the actual painting workflow.
- [ ] Sample meshes include clearly licensed source assets.
- [ ] All code is in user-declared namespaces.
- [ ] Runtime and Editor code are separated.
- [ ] No deprecated/obsolete API warnings on the latest supported Unity Editor.
- [ ] Code is readable, editable and documented.
- [ ] Menu items are under `Window/SurfaceWeaver`.
- [ ] No automatic web redirects, telemetry or external services.
- [ ] No source mesh is modified destructively.
- [ ] Mask saving preserves asset GUIDs/meta files when overwriting existing assets.

## Compatibility

- [ ] Declare and test the minimum Unity version accurately.
- [ ] URP support is implemented and tested.
- [ ] New submissions made using Unity Editor 6.5 or newer must support URP or HDRP.
- [ ] Android target is tested with 64-bit architecture if Android support is advertised.
- [ ] Test enter/exit Play Mode and Domain Reload disabled on applicable Unity versions.

## Package structure

- [ ] One root folder: `Assets/SurfaceWeaver`.
- [ ] No duplicate/redundant files.
- [ ] No file paths over the current Asset Store limit.
- [ ] Include `.meta` files in the final Unity-imported submission.
- [ ] Do not hide most of the Unity-native package inside ZIP archives.
- [ ] Include comprehensive offline documentation for code/shaders/setup.
- [ ] Include Third-Party Notices for any third-party components.

## AI transparency

The current starter source was drafted with AI assistance and must be reviewed, modified and tested by the publisher. If submitted, describe the AI tool(s) used and the specific code/documentation they assisted with in the Asset Store AI description field. No generated textures, artwork, audio or models are included in this starter package.

If generated demo visuals or textures are added later, disclose the tools used, content generated and the meaningful modifications made. Do not use wording such as “hand-drawn” for AI-generated content.

## Lite / Pro

- [ ] Compare the editions clearly in listing artwork/description.
- [ ] Lite's included features behave identically to their Pro counterparts.
- [ ] No DRM, login wall, time restriction, watermark or disabled-feature teaser UI.

## Legal and publishing

- [ ] Confirm product title and namespace rights.
- [ ] Check all sample content licenses.
- [ ] Review the current Asset Store EULA and submission guidelines at submission time.
- [ ] Prepare support contact and actively maintained publisher website.
- [ ] Prepare store images, accurate screenshots, video and setup guide.
