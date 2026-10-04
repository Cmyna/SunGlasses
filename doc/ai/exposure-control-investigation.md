# Exposure Control Investigation and Proposed Volume-Based Override

Session date: 2026-09-29

Target project: SunGlasses

Game version observed: Cities: Skylines II 1.6.2f1

Unity version observed: 2022.3.71f1

## Purpose and conclusion

The user reported that increasing the amount of sky visible in the camera causes terrain and mountains to become excessively dark. A first experiment constrained automatic exposure to a narrow range and noticeably improved brightness stability between camera angles, although the resulting image was too bright overall.

The agreed direction is to control exposure bounds through a dedicated, higher-priority global HDRP Volume. A custom controller would evaluate editable curves based on solar elevation and write the resulting bounds to that Volume. The game would retain responsibility for the sun, sky, moon, weather, and other lighting, while HDRP would continue calculating automatic exposure inside our bounds.

This document distinguishes live observations, implemented experiments, and the proposed replacement architecture. The higher-priority Volume approach was discussed but was not implemented or tested during this session.

## Evidence and access

Two MCP interfaces were successfully used against the running game:

- Gameface: UI screenshots, DOM inspection, debug-menu navigation, and widget access. The endpoint was `http://localhost:9444`, with target `assetdb://gameui/index.html` and Cohtml 2.2.1.3.
- Unity: live C# evaluation, type/member inspection, lighting configuration reads and writes, game screenshots, and GPU exposure-texture readback.

The authoritative exposure settings were read from the main camera's `HDCamera.volumeStack`. An early read of `VolumeManager.instance.stack` returned default-looking values (`Fixed`, exposure 0, tonemapping `None`) that did not describe the main camera. Those values must not be used as evidence about the rendered game view.

Final image screenshots alone cannot establish the original HDR luminance difference between sky and terrain. They already include exposure, grading, and tone mapping. They also cannot establish whether shadow detail has been lost in the scene buffer.

## Initial main-camera configuration

These values were observed during the initial investigation; they are not universal defaults or guarantees for another map, save, or graphics configuration.

| Property | Observed value |
| --- | --- |
| Exposure mode | `AutomaticHistogram` |
| Metering mode | `CenterWeighted` |
| Histogram percentiles | 40–90 |
| Daytime exposure bounds | 4–14 EV100 |
| Exposure compensation | 0 |
| Adaptation | `Progressive`, speeds 11 / 11 |
| Color Adjustments: post exposure | +0.75 EV |
| Color Adjustments: contrast | +20 |
| Color Adjustments: saturation | +30 |
| Tonemapping | ACES |
| Bloom intensity / scatter / threshold | 0.2 / 0.7 / 0 |
| White balance temperature | +15 |
| Screen-space global illumination | Disabled |
| Sky ambient mode | Dynamic |
| Indirect diffuse / reflection multipliers, daytime | 1 / 1 |
| Sun illuminance, initial daytime sample | 110,000 lux |
| Sun elevation, initial daytime sample | Approximately 20 degrees |
| Volumetric fog | Enabled; mean free path 7,000 m |
| Physically Based Sky aerosol anisotropy | 0.6 |

The lack of SSGI does not mean there is no ambient lighting: dynamic sky lighting was active. Similarly, 110,000 lux is a plausible bright-sunlight value, and a Bloom threshold of zero is consistent with HDRP's energy-conserving Bloom design. Neither value by itself proves a fault.

Potential contributors to the initial image included global automatic metering, additional post exposure, ACES plus increased contrast, atmospheric scattering, Bloom, and the balance of direct and indirect illumination. No controlled experiment isolated all of those contributors.

## Why changing the camera angle changes terrain brightness

Center-weighted automatic exposure measures image luminance with greater emphasis near the screen center. When the center shifts from terrain to bright sky, the metered luminance can increase and HDRP can choose a higher exposure EV100, darkening the entire image.

Histogram percentiles reject brightness outliers, not semantic categories. With a large sky area, removing the brightest samples does not necessarily prevent the sky from dominating the remaining histogram.

The user's paired screenshots strongly support a composition-dependent exposure explanation: both terrain and sky became darker in the sky-dominated composition. They do not independently prove that the physical sky brightness is incorrectly configured.

Possible approaches discussed were ground-biased procedural or texture metering masks, depth-derived sky weighting, narrower bounds, environment-driven fixed exposure, and changes to sky brightness. Narrower bounds were selected as the first live experiment.

## Exposure bounds: interpretation and first experiment

Higher exposure EV100 means a smaller exposure multiplier and a darker image. Lower EV100 permits a brighter image.

- The minimum bound limits how far automatic exposure can brighten a dark view.
- The maximum bound limits how far automatic exposure can darken a bright view.
- The interval width limits how much exposure can vary as camera composition changes under the same environment.

Changing the minimum from 4 to 10.5 does not automatically darken the image by 6.5 stops. The bound only changes the result when the unconstrained exposure would fall below it.

The experiment changed daytime bounds from **4–14** to **10.5–11.5 EV100**, while retaining automatic histogram exposure. Before the adjustment, one GPU readback reported approximately 12.17 EV100; after the adjustment, a sample reported approximately 11.42 EV100. These samples are observations, not a controlled same-camera luminance comparison.

The user confirmed that brightness across camera angles became substantially more stable, while the overall view was too bright.

To reduce overall brightness without widening the range, move both bounds upward together. Suggested trial ranges were 11–12 and 11.5–12.5. Reducing post exposure from +0.75 to 0 is another option, but these controls should be tested separately. Changes in exposure multipliers do not translate directly into proportional final display brightness after tone mapping.

## Temporary debug-page injection

The user explicitly requested a page injected into the current game process, without rebuilding or restarting the installed mod.

A small temporary C# assembly was compiled and loaded through `Assembly.LoadFrom`, then its installation method was invoked through the Unity MCP. It created a persistent `MonoBehaviour` and registered a `DebugManager` panel named **Exposure Range**.

The page provided:

- `Limit daylight exposure` switch;
- minimum and maximum EV100 controls;
- original daylight range display;
- effective main-camera range display;
- lighting-state/status display.

The user confirmed the page was usable. The controller remained active when the simulation was paused or the debug menu was closed.

The temporary controller obtained the private `LightingSystem.m_NightDayCycleData` and `m_Exposure` fields through reflection. While enabled, it wrote `DayExposureMin` and `DayExposureMax` on the shared runtime configuration object. In the `Day` state it additionally wrote the HDRP Exposure bounds directly.

This did not patch game method bodies, rewrite `Game.dll`, or save changes back to the serialized asset. The implementation has a significant dawn/dusk compatibility defect described below and should not be reused unchanged.

Temporary source and build locations in the AreaBucket workspace:

- `E:\project\code\CS2\AreaBucket\obj\exposure-debug-preview\ExposureRangeDebug.cs`
- `E:\project\code\CS2\AreaBucket\obj\exposure-debug-preview\bin\Release\net48\AreaBucket.ExposurePreview.dll`

Persistent mod-startup registration and project-reference changes initially added for this feature were withdrawn after the user clarified the injection requirement. The temporary implementation lives under the ignored `obj` directory and should not be treated as durable SunGlasses source.

## Confirmed game code ownership

Targeted decompilation of the installed `Game.dll` confirmed the following:

1. `DayNightCycleData` is a `ScriptableObject` with public writable fields, including `DayExposureMin` and `DayExposureMax`.
2. `Game.Rendering.LightingSystem.OnUpdate()` writes exposure bounds according to the current lighting state.
3. `CalculateState()` selects phases using solar geometry and configured angular thresholds, rather than a universal clock-time cutoff.
4. `SetupPostprocessing()` creates `LightingPostProcessVolume`, with priority **1000**, and enables overrides for the minimum and maximum exposure bounds.

The relevant state behavior is:

| State | Exposure minimum | Exposure maximum |
| --- | --- | --- |
| Night | `NightExposureLowMin` | `NightExposureMax` |
| Dawn | Lerp from `NightExposureLowMin` to `DayExposureMin` | `NightExposureMax` |
| Sunrise | `DayExposureMin` | `DayExposureMax` |
| Day | `DayExposureMin` | `DayExposureMax` |
| Sunset | `DayExposureMin` | `DayExposureMax` |
| Dusk | `NightExposureLowMin` | `NightExposureMax` |

The dawn code is:

```csharp
m_Exposure.limitMax.value = m_NightDayCycleData.NightExposureMax;
m_Exposure.limitMin.value = math.lerp(
    m_NightDayCycleData.NightExposureLowMin,
    m_NightDayCycleData.DayExposureMin,
    delta);
```

Thus, changing a "day" field also changes the target used during Dawn. It is not an isolated setting applied only in the Day branch.

Observed angular thresholds were -0.1 degrees for dawn start, 2 degrees for the dawn-to-sunrise boundary, and 5 degrees for full day. The clock time of these crossings depends on the current environment.

Decompiled field initializers are not necessarily the loaded asset's active values. For example, `NightExposureMax` initializes to 14 in the decompiled class, but the running configuration used 6.

Targeted source copies and verified line references:

- [DayNightCycleData: day fields](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/DayNightCycleData.cs:30)
- [LightingSystem: Dawn bounds](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/LightingSystem.cs:266)
- [LightingSystem: Sunrise bounds](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/LightingSystem.cs:290)
- [LightingSystem: Day bounds](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/LightingSystem.cs:313)
- [LightingSystem: phase calculation](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/LightingSystem.cs:491)
- [LightingSystem: Volume setup](E:/project/code/CS2/AreaBucket/obj/rendering-inspection/LightingSystem.cs:564)
- [Temporary injected writes](E:/project/code/CS2/AreaBucket/obj/exposure-debug-preview/ExposureRangeDebug.cs:164)

These are locally decompiled copies, not the developer's original source files, and reside in a temporary output directory.

## Dawn brightness discontinuity: reproduced cause

The user observed a pronounced brightness change when adjusting Time of Day from 5.7 to 5.9. These decimal-hour values are 12 minutes apart.

With the temporary limiter enabled, live readbacks showed:

| Time of Day | State | Bounds, EV100 | Actual GPU EV100 |
| --- | --- | --- | --- |
| 5.70 | Night | 0–6 | 0 |
| 5.85 | Dawn | **8.21–6** | 6 |
| 5.90 | Sunrise | 10.5–11.5 | 10.5 |

At 5.85 the minimum exceeded the maximum. The injected high daytime minimum entered the game's dawn interpolation while the maximum remained at the night value of 6. At Sunrise, the bounds changed to the daytime interval, forcing actual exposure from 6 to 10.5 in these samples. A 4.5-stop increase reduces the exposure multiplier by approximately 22.6 times; scene illumination was changing at the same time, so this is not a direct final-image brightness ratio.

With the limiter disabled, samples showed:

- 5.73: Night, bounds 0–6, actual EV100 0;
- 5.74: Dawn, bounds approximately 0.14–6, indirect diffuse multiplier approximately 9.69;
- 5.90: Sunrise, bounds 4–14, actual EV100 approximately 4.77, sun approximately 5,185 lux.

The original dawn behavior includes interpolation. Skipping 12 minutes crosses substantial natural illumination and exposure changes; the evidence does not justify describing every observed original brightness change as a hard switch. The temporary limiter introduced an additional concrete range conflict.

Merely slowing exposure adaptation cannot repair an invalid interval or guarantee continuity when bounds change. Clamping minimum to maximum would prevent invalid ranges but would not by itself eliminate phase-boundary jumps.

At the end of this comparison, time was restored to 5.7 and the limiter was restored to enabled. This describes the last verified experiment state, not a guarantee about subsequent user changes.

## Proposed replacement: dedicated high-priority Exposure Volume

Create a separate global Volume, with a priority above the game's observed 1000, for example **2000**, and weight **1**. Add only an HDRP `Exposure` component and enable overrides for `limitMin` and `limitMax`.

The intended architecture is:

```text
Solar elevation + optional environment adjustments
                      |
             Editable exposure curves
                      |
       Dedicated high-priority global Volume
                      |
    HDRP automatic metering inside custom bounds
```

Other Exposure properties, including mode, metering, compensation, and adaptation, would remain unoverridden unless explicitly added later. The original lighting system would continue updating its own lower-priority Volume.

Required conditions and validation points:

- Put the new Volume on a layer included in the target camera's Volume Layer Mask.
- Verify that the chosen priority wins against all active competing overrides, including other mods and photo-mode settings.
- Update curves before HDRP evaluates the target camera's Volume stack for the frame.
- Confirm the effective values through the main camera's `HDCamera.volumeStack`, not merely the new profile's fields.
- Implement the control switch by enabling/disabling the dedicated Volume so the original behavior can take over automatically.
- Clean up the owned Volume, profile, GameObject, and debug widgets on removal.

Before testing this design, disable/remove the legacy injected controller and restore its modified `DayNightCycleData` fields. Otherwise, disabling the new Volume would reveal the already-modified original configuration rather than the true baseline.

This design avoids writing the shared day/night configuration and avoids relying on a repeated write race against `LightingSystem`. Its behavior in this game's actual Volume stack still needs live verification.

## Manual calibration model

The desired control is a pair of continuous functions of solar elevation:

```text
minimumEV = MinCurve(solarElevation)
maximumEV = MaxCurve(solarElevation)
```

A convenient editing model is exposure center and interval width:

```text
minimumEV = centerEV - widthEV / 2
maximumEV = centerEV + widthEV / 2
```

Center determines the overall exposure level; width determines the allowed composition-dependent variation. Validate finite values and nonnegative width. Interpolation must preserve legal intervals and continuity, including at curve endpoints and phase crossings.

The proposed debug page would expose the takeover switch, current solar elevation, active bounds, editable curve nodes, and configuration save/load. These expanded controls were discussed but not implemented.

Initially calibrate a clear-weather curve. The same solar elevation can produce different illumination under clouds, different seasons, and different morning/evening conditions. Add weather compensation or separate sunrise/sunset curves only when measurements show a need.

An alternative partial-control design is to blend original bounds toward custom daytime bounds using the same smooth weight for both endpoints. That can preserve original night behavior while gradually applying daytime restrictions. It is different from full range takeover and should be chosen explicitly.

## Suggested validation sequence

1. Restore original configuration and remove the legacy controller.
2. Inject the dedicated Volume with static bounds; verify on/off behavior and camera layer inclusion.
3. Repeat the terrain-dominated versus sky-dominated views at a fixed time and weather.
4. Add solar-angle curves and sample dawn/dusk in small increments, checking bounds and actual GPU EV100.
5. Test manual time jumps, paused simulation, closing/reopening the debug menu, and changing saves.
6. Test clear and overcast conditions and inspect terrain shadows and sky highlights separately.

Success requires legal continuous bounds, no additional phase-boundary jumps from the override, a reliable return to original behavior, and improved subject readability across camera angles. Exposure-range stability alone does not guarantee consistent scene lighting or preserve all sky highlights.

## Unity references

- [HDRP 14 Exposure](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Override-Exposure.html)
- [HDRP 14 Color Adjustments](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Post-Processing-Color-Adjustments.html)
- [HDRP 14 Tonemapping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Post-Processing-Tonemapping.html)
- [HDRP 14 Bloom](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Post-Processing-Bloom.html)
- [HDRP 14 Physically Based Sky](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Override-Physically-Based-Sky.html)
- [HDRP 14 Physical Light Units](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Physical-Light-Units.html)
- [Unity 2022.3 exposure metering shader](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/ExposureCommon.hlsl)

Unity documentation and upstream shaders explain HDRP behavior. The installed game assembly and live camera stack remain authoritative for game-specific control logic and effective settings.
