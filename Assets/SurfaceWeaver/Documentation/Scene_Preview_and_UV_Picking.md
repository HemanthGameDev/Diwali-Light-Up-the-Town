# Scene View Brush Preview and UV0 Picking (v0.2.0)

This milestone adds preview-only interaction. It does **not** modify textures or paint masks.

## Setup

1. Select a GameObject with `MeshFilter`, `MeshRenderer`, and `SurfacePaintTarget`.
2. Make sure its mesh is readable. For imported models, enable **Read/Write** in the Model Import Settings and reimport.
3. Open **Window > SurfaceWeaver > Painter**.
4. Assign the target in the Target Mesh section.
5. Click **Add / Refresh MeshCollider**. SurfaceWeaver creates or refreshes a non-convex `MeshCollider` using the target's current `MeshFilter.sharedMesh`.
6. Keep the target UV channel set to `0` for this milestone.
7. Enable **Scene View Brush Preview**, then move the pointer over the target in the Scene View.

## What the preview displays

- An outlined circular footprint with a radius measured in world units.
- A footprint plane aligned to the picked surface normal.
- UV0 coordinates at the hit point.
- World position, world normal, and triangle index in the SurfaceWeaver window.

Hold **Alt** to use normal Scene View navigation. The preview does not paint, change the material, or modify the mesh.

## UV channel limitation

The current picker uses `RaycastHit.textureCoord`, which provides the primary UV coordinates (UV0). Other UV channels are not supported by this preview milestone. The target's configured UV channel must therefore be `0`.

Unity documents that `RaycastHit.textureCoord` requires a `MeshCollider`; the collider must be non-convex for this use case. In Player builds, UV coordinates are returned as `(0, 0)` unless the mesh has Read/Write enabled. The current workflow is Editor-only, but the tool still asks for a readable mesh so diagnostics and picking use consistent data.

## Known limitations

- No painting or erasing yet.
- No GPU stamping or RenderTexture management yet.
- No custom alpha brush preview yet; the footprint is a circular world-space guide.
- No UV island overlap or seam-padding analysis.
- Static `MeshFilter` + `MeshRenderer` targets only; no `SkinnedMeshRenderer` support.
- The preview raycasts only against the assigned target's MeshCollider, so other scene objects do not occlude the pick.

## Manual verification

1. Use a flat plane. Confirm the ring is circular and changes size with the World Units slider.
2. Move over a sloped section. Confirm the ring tilts to match the surface normal.
3. Confirm UV0 changes as the pointer moves across the mesh.
4. Confirm the displayed triangle index changes as expected across triangles.
5. Hold Alt and orbit/pan the Scene View; confirm the preview does not capture navigation.
6. Disable preview; confirm the ring disappears.
7. Change the target UV channel to a nonzero channel; confirm SurfaceWeaver warns that the preview currently reads UV0.
8. Disable Read/Write on the imported mesh and reimport; confirm the picker reports the readable-mesh requirement.

## API references

- Unity `RaycastHit.textureCoord`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/raycasthit/texturecoord
- Unity `HandleUtility.GUIPointToWorldRay`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/handleutility/guipointtoworldray
- Unity `Handles`: https://docs.unity3d.com/ScriptReference/Handles.html
