# SurfaceWeaver – Market Positioning Notes

## Category reality

The Unity Asset Store already has mesh painting and mask painting products. A generic feature checklist of “any mesh, multiple layers, custom brushes, GPU painting and URP” is not sufficiently differentiated by itself.

Examples reviewed on 2026-10-30:

- Universal Mesh Painter lists multi-layer mesh painting, custom brushes, GPU painting, layer controls and URP support.
- Mesh Mask Painter lists RGBA mask authoring, static/skinned mesh support, channel operations, UV-channel selection, edge padding and PNG export.
- Paint in Editor lists mesh analysis, UV unwrapping/remapping and PBR material painting.

## Proposed position to validate

**Environment path and surface authoring for custom meshes.** Focus on designers who import environment geometry from Blender/Maya and need to create convincing paths, clearings, banks and material transitions with reliable UV diagnostics and mobile-conscious baked output.

Potential differentiators:

1. Spline/path-based material strokes with width, taper, falloff and edge variation.
2. UV health report with actionable explanations before painting.
3. Reusable surface recipes/profile assets.
4. Explicit mask packing, seam padding and mobile output budgets.
5. Simple, consistent URP integration rather than a broad multi-pipeline system.

## Validation before committing to a commercial launch

- Interview 5–10 Unity environment artists/indie developers.
- Ask them to perform a representative ground/path task using an existing product.
- Identify the most painful gaps, not merely requested feature lists.
- Compare ease of setup, painting quality, UV robustness, save reliability and build/runtime cost.
- Revisit the product name and trademarks before public marketing.

Do not use competitor product screenshots, icons, code, marketing text or other proprietary materials in the package.
