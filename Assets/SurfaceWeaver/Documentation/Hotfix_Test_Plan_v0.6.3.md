# v0.6.3 — Short hotfix test plan

1. Import the package in a backup/feature-branch project and wait for Unity import.
2. Confirm the Console no longer reports `undeclared identifier '_FlipRasterY'` for `SurfaceMeshMaskStamp.shader` on D3D11.
3. Confirm there are no new shader compile errors for `Hidden/SurfaceWeaver/MeshMaskStamp`.
4. Create a fresh black mask page and paint a short stroke on a simple Plane.
5. Save and reload the mask; confirm its orientation is unchanged.
6. Only after the Plane test passes, repeat on the cylinder seam test and then the village ground. Do not overwrite production masks during diagnostics.

Report the Unity version, graphics API (D3D11/D3D12/Vulkan/Metal), full first Console error if any, and screenshots of the Plane/cylinder result.
