# SurfaceWeaver v0.6.3 — D3D11 shader declaration hotfix

## Fixed

Unity reported:

`Shader error in 'Hidden/SurfaceWeaver/MeshMaskStamp': undeclared identifier '_FlipRasterY'`

The vertex function referenced `_FlipRasterY` before its HLSL declaration. The uniform declarations are now grouped before the vertex and fragment functions, so the identifier is declared before use on D3D11/HLSL compilation.

## Scope

This is a compiler hotfix only. It does not claim to resolve the previously reported seam-continuity or spline-mask orientation behaviour. Those behaviours still require controlled tests in the target Unity project and graphics API.

## Import

1. Commit or stash your current SurfaceWeaver branch.
2. Back up `Assets/SurfaceWeaver`.
3. Replace only `Assets/SurfaceWeaver` with this package's folder.
4. Let Unity reimport and compile shaders.
5. Check the Console for the original `_FlipRasterY` error and any new shader errors.
6. Run a fresh-mask Plane test before testing a cylinder seam or production ground.

## Validation status

Static source inspection confirms `_FlipRasterY` is declared before `Vert` references it and occurs once as a uniform declaration. This package has not been compiled inside the user's Unity Editor or exercised on D3D11/other graphics backends by the package authoring environment. Unity-side validation remains required.
