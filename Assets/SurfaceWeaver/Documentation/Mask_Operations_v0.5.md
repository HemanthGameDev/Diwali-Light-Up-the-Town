# Mask Operations — v0.5.0

## Scene View modes

### Paint
Moves the selected packed mask channel towards white using brush opacity, radial falloff, and optional brush alpha.

### Erase
Moves only the selected packed mask channel towards black. Other channels in the same RGBA page are preserved.

### Smooth
Uses a 3x3 Gaussian-like neighborhood filter on the selected mask channel. The brush footprint and opacity control where and how strongly the filtered value is applied. This smooths a grayscale layer mask; it does not smear albedo pixels.

## Full-channel operations

- **Fill Selected Layer:** sets the selected channel to white across the complete page.
- **Clear Selected Layer Channel:** sets the selected channel to black across the complete page.

Both operations use the active stroke snapshot system and can be undone while the current GPU session remains open.

## Persistence

Use **Save Mask PNG** to export the current packed RGBA page. The profile stores the resulting Texture2D reference. Undo/Redo history is session-only and is not serialized.

## Usage note

Use Smooth after coarse brushwork to soften mask transitions. Use low opacity and a moderate radius. High opacity or large radii can flatten intended detail. Save the page after completing an editing session.
