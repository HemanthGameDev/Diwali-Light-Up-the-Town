# SurfaceWeaver v0.7.1 – Layered Shader Preview Compatibility

## Purpose

The v0.6 painter window defaults its live mask preview property to `_PaintMask` for the original Shader Graph material contract. The v0.7 layered URP shader instead exposes the packed RGBA page as `_MaskPage0`.

When a v0.7 generated material was assigned, the painter warned that `_PaintMask` was missing. Mask painting and PNG saving could still work, but the in-progress RenderTexture preview was not bound to the material.

## Change

`SurfaceWeaverWindow` now resolves the preview property against the current target renderer:

1. Keep the currently configured property if the renderer supports it.
2. Otherwise prefer `_MaskPage0` for the v0.7 layered shader.
3. Otherwise fall back to `_PaintMask` for legacy Shader Graph materials.

The explicit Preview Shader Property field remains editable. No profile, material, mask page, or shader asset is modified by this patch.

## Important

The v0.7 `Create / Assign SurfaceWeaver URP Material` action replaces the selected MeshRenderer material slot with a generated SurfaceWeaver material. It does not delete the previous material asset. To restore the prior appearance, reassign the original Shader Graph material in the MeshRenderer. For v0.7 evaluation, use a duplicate target or a dedicated test scene/material.

## Validation status

Static source inspection only. Unity compilation, live MaterialPropertyBlock preview, shader rendering and Play Mode behaviour must be verified in the user's Unity project.
