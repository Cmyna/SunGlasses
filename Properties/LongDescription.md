## SunGlasses

This mod focus on solving under-exposure effects in vanilla CS2 game when facing the sun. \
Options to adjust sun & sky & exposure settings, includes:

- Sun disk size
- Sun bloom
- Enable/Disbale lens flare when facing sun
- Sky exposure
- Daytime Ambient lighting control
- Ground fill light (0–1)
- Exposure curve: None, Realistic or Bright
- Independent color correction toggle

### Sun disk size

Sliding the bar to adjust the size of the sun disk

### Sun Bloom

Adjust the sun bloom strength. It actually overrides the Blooms settings in PhotoMode, which means other artifical light bloom will also be affected.

### Lens Flare

Option to toggle sun light lens flare effect when facing the sun

### Sky Exposure

This option can adjust the sky exposure. Higher value will also brighten everything and reduce the impact ratio from sun light in the final view of the scene.

### Ambient Lighting

Behave likes those mods with shadow strength adjustment / ambient lighting in CS1, but only affects daytime lighting. 
High value will make shadow lookes more blue (Because most of ambient lighting comes from sky)

### Ground Fill Light

Adjust daytime ground fill light from 0 (off, default) to 1 (original lighting). Nighttime ground lighting is preserved, with a smooth transition near sunrise and sunset. This option is saved in mod settings.

### Exposure Curve and Color Correction

Choose None to use the game's exposure bounds, or Realistic (default) / Bright to apply a dynamic exposure curve. Color Correction independently enables atmosphere adjustments and is enabled by default. Both choices are saved in mod settings.

### Possible Incompatibilities

Lumina: partial compatibility, based on a source-code review of Lumina main (commit 2a9a4a3, reviewed on 2026-10-10). Combined use has not yet been verified in-game with the published builds.

- Custom Sun: both mods control sun disk size and flare size; SunGlasses reapplies these settings every frame. Disable Lumina Custom Sun and avoid its sun sliders when using SunGlasses.
- HDRI/Cubemap: Lumina replaces the physically based sky that SunGlasses sky exposure, sky color correction, and ground tint adjustments rely on. Disable HDRI/Cubemap to retain the full SunGlasses sky corrections. This does not imply that fog, exposure, or sun controls stop working.
- Exposure and grading: SunGlasses automatic exposure bounds and Lumina Post Exposure affect different parameters and can work together. LUTs, white balance, and other grading also combine with SunGlasses corrections; adjust them together if the result is too bright or has an unwanted color cast. Exposure Curve: None disables SunGlasses automatic exposure if preferred.
- Disabling SunGlasses Color Correction or choosing Exposure Curve: None does not hand all lighting control to Lumina. Sun disk size, ground tint correction, and reduced sunlight specular highlights remain active.

For SunGlasses Debug builds, avoid using exposure calibration time controls together with Lumina time controls. The calibration code is excluded from Release builds.

### NOTES

Sun Bloom Settings in mod will overwrite the effects in PhotoMode, 
while exposure curves and atmosphere color correction yield to enabled PhotoMode overrides.

All cloud settings, including distant clouds and volumetric clouds, can substantially change the color of daytime ambient lighting. In general, more cloud cover makes shadows look more muted and whitish around noon, and more orange or pink at dawn and dusk. Less cloud cover shifts shadows toward blue and cooler tones.


