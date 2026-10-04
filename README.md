# SunGlasses
 A Cities Skylines 2 graphic mod for controlling sun light in game

Lighting overrides run every frame in PreCulling, after the vanilla lighting system.
Saved options are applied when the mod loads; missing options use the mod defaults.
`SunGlassesSunSpecularLight` follows the vanilla sun's transform, native Light
settings and HDRP settings, with diffuse, volumetric and sky interaction disabled.
The extra light does not cast shadows: HDRP allows only one directional light to
own cascaded shadows, so the vanilla sun retains them.
The vanilla sun keeps its other settings and has only `affectSpecular` disabled.
The extra light is removed and the original specular flag restored when the mod unloads.

In the developer menu, **SunGlasses Sun → Specular sun lightDimmer** controls the
extra light's dimmer from 0 to 1 (default 0.01). This debug value lasts for the session
and is not saved to the mod settings.

**SunGlasses Sun → Sun tint preset** cycles through six
presets: original white (1, 1, 1), mild correction (0.78, 0.88, 1), neutral
correction (0.70, 0.83, 1), cool correction (0.65, 0.80, 1), previous gentle
warmth (0.76, 0.85, 1; default), and golden red (1, 0.86, 0.72).
**Selected sun tint** identifies the selected preset.
**Blend sun tint by elevation** is enabled by default. It uses the actual sun
direction to blend from the selected preset at 0 degrees to white at
**White tint threshold (degrees, 0.01-90)** (default 10 degrees). Above the
threshold the tint stays white; below the horizon it stays at the selected preset.
Disabling the blend applies the fixed preset at every angle. The elevation,
white weight and resulting RGB are displayed in the same debug tab.
The resulting tint is applied after vanilla lighting and before copying the specular
sun, without changing intensity or color-temperature settings. Closing the developer
menu retains the selection; it is session-only. Unloading restores the captured tint
unless another caller has since changed it.

**SunLight intensity multiplier (min 0.01)** accepts a finite number of at least
0.01 (default 1). **Reset SunLight intensity multiplier to 1** restores the default;
the before/after row reports native Light intensity. The multiplier is applied
after vanilla lighting and before copying the specular sun, retaining the latter's
independent lightDimmer. A paused frame does not repeatedly multiply the previous
output. Invalid input keeps the last valid value. This control is session-only;
unloading restores the unscaled intensity unless another caller changed it.

**SunGlasses Exposure → Active interpolation samples** shows the two sample
numbers used for the current bounds, their arc angles, whether a mirrored point
is used, and the interpolation fraction `t` (0 = first sample, 1 = second sample).

The realistic curve is selected and enabled by default when the mod loads.
The same tab provides **Use bright curve** and **Use realistic curve**, bundled
from `Resources/exposure-curve`. Switching enables interpolated exposure immediately
and refreshes the sample list. **Active exposure curve** identifies the selected
curve. **Use custom samples** restores the existing calibration file; bundled
curves cannot be recorded over or edited with the sample removal controls.
Curve selection lasts for the session.

The same tab shows **Average relative luminance** and **Relative luminance P50**.
These are approximate SDR scene brightness statistics after post-processing,
before the game UI: linear RGB uses `Y = 0.2126R + 0.7152G + 0.0722B`, with each
channel clamped to 0–1. Values are percentages relative to SDR white, not nits.
A 256×144 image is sampled twice per second using asynchronous GPU readback while
the rows are visible; sampling stops shortly after the panel closes. P50 is the
median of the sampled luminances, not the luminance of a median RGB color.
No exposure or rendered output is changed by this measurement pass. HDR display
output and physical monitor brightness are outside this metric's scope.

**SunGlasses Atmosphere** controls a separate global `SunGlasses Atmosphere Volume`
at priority 3100. The enable checkbox disables the whole Volume, returning to the
underlying game/Photo Mode settings. Aerosol density, anisotropy, aerosol tint,
atmosphere color saturation, horizon tint, fog tint and fog attenuation distance are overridden.
Defaults are 0.012, 0.8, white RGB (1, 1, 1), 0.7, RGB (0.55, 0.59499943, 1)
and fog RGB (0.7, 0.795, 1).
**Fog attenuation distance (meters)** overrides HDRP's `meanFreePath`, default
20000 meters; greater distances make the fog thinner. It accepts finite values at
or above HDRP's minimum and shares the atmosphere enable checkbox.
**Cloud layer A ambientProbeDimmer** controls ambient lighting of distant clouds
from 0 (suppressed) to 1 (full influence, default). It shares the atmosphere
enable checkbox and only overrides this CloudLayer parameter.
Text inputs accept density and saturation 0–1, anisotropy −1–1 and tint channels 0–1; invalid input
retains the previous valid value. These debug controls reset when the game restarts.

To build and run the official post-processing without deploying to local Mods:

```powershell
dotnet build SunGlasses.csproj -c Debug -p:DeployLocalMod=false
```

Omitting `DeployLocalMod=false` retains the toolchain's normal local deployment.
The complete `Resources` directory is copied to build/publish output with its
relative paths intact, so local deployment includes `Resources/exposure-curve`.
