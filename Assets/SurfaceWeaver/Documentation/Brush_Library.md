# SurfaceWeaver Brush Library

## Starter brushes

Use **Create Starter Brush Library** in the Painter window or **Tools > SurfaceWeaver > Create Starter Brush Library**. The tool creates editable ScriptableObject presets and source alpha PNGs under `Assets/SurfaceWeaver/BrushLibrary/Starter`.

- **Soft Round**: smooth falloff for broad organic blending.
- **Hard Round**: firm circular stamp for precise regions.
- **Grunge**: mottled alpha for irregular surface coverage; random rotation and scale variation are enabled.
- **Scatter**: clustered alpha marks for broken-up coverage; random rotation and scale variation are enabled.

## Custom brushes

Create a preset via Assets > Create > SurfaceWeaver > Brush Preset, assign a grayscale alpha texture, and tune the default brush settings. White values in the texture's red channel paint; black values do not contribute. For predictable mask sampling, set the texture to Default, disable sRGB, enable Read/Write, disable mipmaps, set Wrap to Clamp, and use Bilinear filtering.

`Scale Jitter` on the preset randomizes each stamp radius by the specified fraction. For example, 0.2 varies stamp radius between 80% and 120% of the selected radius.
