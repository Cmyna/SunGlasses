# SunGlasses
 A Cities Skylines 2 graphic mod for controlling sun light in game

Lighting overrides run every frame in PreCulling, after the vanilla lighting system.
Saved options are applied when the mod loads; missing options use the mod defaults.
PBS ground tint fades from its original color at sun elevation <= 0 degrees to
black at >= 5 degrees by default, independently of Color
Correction, atmosphere overrides and the developer menu. There is no public option
to disable it. **SunGlasses → Atmosphere → Daytime ground tint weight (0 = black, 1 = original)**
is a session-only 0–1 debug control for the daytime endpoint. Night always retains
the original color; 0–5 degrees interpolates to the chosen daytime endpoint.
RGB is interpolated while preserving alpha. The diagnostics row shows sun elevation,
effective original-color weight, original and applied colors. It edits `Render Settings`' runtime Volume profile,
not its shared asset. Scene changes recapture the source; unloading restores it unless
another caller has since changed the parameter. Restarting/reloading the mod resets
the debug weight to 0.

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
correction (0.70, 0.83, 1), cool correction (0.65, 0.80, 1), Gentle warmth
(0.76, 0.85, 1; default), and golden red (1, 0.86, 0.72).
**Selected sun tint** identifies the selected preset.
**Blend sun tint by elevation** is enabled by default. It uses the actual sun
direction to blend from the selected preset at 0 degrees to white at
**White tint threshold (degrees, 0.01-90)** (default 10 degrees). Above the
threshold the tint stays white; below the horizon it stays at the selected preset.
Disabling the blend applies the fixed preset at every angle. The elevation,
white weight and resulting RGB are displayed in the same debug tab.
The resulting tint is applied after vanilla lighting and before copying the specular
sun, without changing intensity or color-temperature settings. Closing the developer
menu retains the Debug selection; it is session-only. Applying the saved Color
Correction option selects Gentle warmth when enabled, or Original / no tint when
disabled, retaining the elevation blend settings. Unloading restores the captured tint
unless another caller has since changed it.

**SunLight intensity multiplier (min 0.01)** accepts a finite number of at least
0.01 (default 1). **Reset SunLight intensity multiplier to 1** restores the default;
the before/after row reports native Light intensity. The multiplier is applied
after vanilla lighting and before copying the specular sun, retaining the latter's
independent lightDimmer. A paused frame does not repeatedly multiply the previous
output. Invalid input keeps the last valid value. This control is session-only;
unloading restores the unscaled intensity unless another caller changed it.

Dynamic exposure runs as a game DOTS managed system in PreCulling after
RemapLightingSystem, using the bundled realistic curve by default. One global
HDRP Volume at priority 1500 overrides only automatic exposure bounds, above
vanilla lighting (1000) and below Photo Mode (2000). It keeps
working when the developer menu is closed or the exposure Debug system is disabled.

The mod options provide an **Exposure Curve** dropdown: **None**, **Realistic**
(default), or **Bright**. **None** disables the exposure override. The independent
**Color Correction** checkbox (enabled by default) controls atmosphere overrides.
Both options are saved and applied at mod load and when changed. Applying options
clears manual exposure preview and restores the selected preset; Debug curve
changes remain temporary. The old dark-area histogram adjustment has been removed.

**SunGlasses → Exposure** separates **Calibration sample source** from
**Runtime exposure curve**. **Use bright curve** and **Use realistic curve**
switch the runtime curve and load their read-only samples. **Use custom samples**
loads the editable file while retaining the runtime curve. **Apply custom curve**
submits a validated copy; recording or deleting samples does not change a
previously submitted curve.

**Manual calibration preview** applies center ± width/2. Turning it off restores
the curve if **Enable dynamic exposure** is on; the switches are independent.
**Applied exposure bounds** reports the last applied frame, and **Preview check**
verifies the main camera. **Active interpolation samples** reports runtime sample
numbers, mirrored nodes, arc angles and interpolation fraction.
Time control and recording belong to Debug. Disabling Debug releases its manual
preview and time override. Brightness monitoring and average/P50 rows were removed.

See [dynamic exposure and calibration documentation](doc/ai/exposure-calibration-tool.md)
for interpolation, persistence, lifecycle and verification details.

Atmosphere overrides now run as a game DOTS managed system in PreCulling after
dynamic exposure, independent of Debug. A global `SunGlasses Atmosphere Volume`
at priority 1500 overrides only eight selected sky, fog and cloud parameters,
above vanilla lighting (1000) and below Photo Mode (2000).
Defaults are aerosol density 0.024, anisotropy 0.8, white aerosol tint,
saturation 0.7, horizon RGB (0.5, 0.65, 1), fog RGB (0.8, 0.8, 1), fog attenuation
distance 20000 meters and cloud layer A ambientProbeDimmer 1. Fog enable, color
mode, height, weather and other cloud parameters remain controlled by the game.

When the native `ClimateSystem.fog.overrideState` is enabled (for example by
Time & Weather Anarchy custom fog or the game's Debug Override Fog control),
SunGlasses suspends both fog tint and attenuation-distance overrides. Turning
custom fog off restores them on the next rendering update. Sky, clouds and sun
color correction remain active. This requires no dependency on another mod.

**SunGlasses → Atmosphere** provides an **Enable atmosphere overrides** checkbox
plus **Blend horizon tint by elevation** (enabled by default) and
**Horizon white tint threshold (degrees, 0.01-90)** (default 20 degrees).
The horizon blends from its configured RGB at 0 degrees to white at the threshold,
staying white above it and at the configured tint below the horizon. Disabling
the blend keeps the fixed configured tint. These controls are independent of
the sun tint threshold, last for the session, and work with the menu closed.
Diagnostics display elevation, white weight, active horizon RGB,
configured and effective camera values, Volume status
and a camera match check. Unchecking restores the underlying game / Photo Mode
values; checking reapplies the current atmosphere configuration. The saved
**Color Correction** option controls the same switch; Debug edits are temporary.
See [atmosphere system documentation](doc/ai/atmosphere-system.md) for lifecycle,
configuration boundaries and verification details.

To build and run the official post-processing without deploying to local Mods:

```powershell
dotnet build SunGlasses.csproj -c Debug -p:DeployLocalMod=false
```

Omitting `DeployLocalMod=false` retains the toolchain's normal local deployment.
The complete `Resources` directory is copied to build/publish output with its
relative paths intact, so local deployment includes `Resources/exposure-curve`.
