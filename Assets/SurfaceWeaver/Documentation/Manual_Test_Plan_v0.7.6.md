# SurfaceWeaver v0.7.6 Manual Test Plan

1. Back up the project and merge this patch into the Unity project root. Confirm there are no Unity Console compile errors.
2. On the generated SurfaceWeaver material, assign distinct test values: Base Tint, Base Tiling, Texture Mapping Mode; for each overlay, Tint, Tiling and Params X/Y/Z/W. Example values for Layer 1: X=2.5, Y=0, Z=0, W=0.6.
3. Save the material asset. Click **Sync Profile Bindings (Preserve Material Tuning)**.
4. Confirm Base Tint/Tiling/Mapping and every Layer Tint/Tiling/Params component remain unchanged. Confirm profile texture references, mask reference and overlay count are refreshed.
5. Open Painter and paint each RGBA channel. Confirm the live mesh preview respects the material's custom Params Y channel values and other per-layer settings.
6. Save the mask, click **Update Mask Only (Preserve All Material Settings)**, and verify that only `_MaskPage0` changes.
7. Do not use **Reset Material from Profile** during the preservation test; that button intentionally overwrites settings. Test it separately only on a disposable duplicate material.
8. Save the project, close Unity, reopen, and confirm the material values persist.

Expected: Safe Sync preserves all material tuning fields and full Params vectors. Mask Only changes only the packed mask texture.
