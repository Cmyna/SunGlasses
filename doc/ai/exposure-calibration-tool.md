# Dynamic Exposure and Calibration Tools

## Runtime and Debug boundaries

Dynamic exposure uses the built-in `realistic` curve by default. Each rendered frame calculates automatic exposure bounds from the sun angle, independently of whether the developer menu is open or the Debug system is enabled.

| Module | Responsibility |
| --- | --- |
| `DynamicExposureSystem` | DOTS managed system: reads sun state, evaluates and applies exposure, manages Volume and scene resources |
| `ExposureCurve` | Validates and copies curve data, interpolates by solar arc angle, loads built-in curves; does not reference Debug types |
| `ExposureDebugSystem` | DOTS managed system: owns the calibration Controller, manages temporary time overrides and restoration |
| `ExposureCalibrationController` | Plain C# class: calibration parameters, sample recording and deletion, Custom JSON, operation status, camera diagnostics |
| `ExposureCalibrationDebugTab` | Developer menu Exposure section and action bindings |

The dynamic system is explicitly registered through UpdateSystem in PreCulling after RemapLightingSystem. The Debug system is registered in MainLoop; it performs no exposure calculations each frame and retains the update entry point for stop cleanup when disabled. Both use PlanetarySystem from their own World. Exposure is no longer driven by a custom MonoBehaviour or LateUpdate. Brightness monitoring, CustomPass, GPU readback, and average brightness/P50 rows have been removed.

## Exposure application rules

The system owns a global HDRP Volume with priority 3000 and weight 1, using the layer of LightingPostProcessVolume. It overrides only Exposure.limitMin and limitMax, preserving the exposure mode and other parameters from underlying Volumes. The component is created with VolumeProfile.Add<Exposure>(false); true would enable other overrides, including Fixed mode, potentially overexposing the image.

1. When manual calibration preview is enabled, use center ± width/2.
2. Otherwise, when dynamic exposure is enabled, use the runtime curve's interpolated result.
3. When both are disabled, disable the Volume and let other Volumes control exposure.

Manual preview and the dynamic switch are independent. Disabling manual preview restores the enabled dynamic curve; disabling dynamic exposure does not disable an active manual preview. Valid Center or Width edits enable manual preview without changing the dynamic switch.

Overrides require a valid sun, an existing Transform, finite game time, and an available original lighting Volume. The main menu and unavailable resources produce a waiting status. The system publishes an ExposureSnapshot of the values written to its own Volume for the current frame. Recording and Applied exposure bounds read that snapshot, avoiding treating a separate evaluation as an already-applied result.

Preview check independently reads the main camera's HDRP stack. It reports Applied to main camera when the mode is Automatic or AutomaticHistogram and both bounds differ from the snapshot by less than 0.01 EV100. The snapshot alone cannot prove that another Volume has not overridden it.

## Curve interpolation

The morning solar arc angle equals solar elevation. In the afternoon, positive elevation maps to 180° - elevation and negative elevation maps to -180° - elevation. Each actual sample supplies a mirrored node during evaluation; JSON and the calibration list store only actual samples.

Interpolation uses angular distance between adjacent nodes on the circle, including the ±180° seam. Time selects the morning or afternoon side of the solar arc but does not determine interpolation distance. A single sample produces constant bounds; the interval between the highest sample and its mirror also retains that sample's bounds. Active interpolation samples displays runtime sample indices, angles, mirror markers, and interpolation ratio. It displays Off during manual preview.

Curve acceptance requires at least one sample, time in [0,24), elevation in [-90,90], finite values, nonnegative width, and bounds within [-10,20] EV100. Data is copied, so later calibration edits do not change the runtime curve.

## Calibration operations

Developer menu: SunGlasses → Exposure.

| Action or display | Behavior |
| --- | --- |
| Use bright curve / Use realistic curve | Switches the built-in runtime curve, enables dynamic exposure, loads read-only samples, and clears manual preview |
| Use custom samples | Loads editable Custom samples, clears manual preview, and preserves the runtime curve and dynamic switch |
| Calibration sample source | Source of the current calibration list, which may differ from the runtime curve |
| Runtime exposure curve | Name of the current dynamic exposure curve |
| Apply custom curve | Validates and copies the nonempty Custom list into the runtime curve, enables dynamic exposure, and clears manual preview |
| Manual calibration preview | Enables or releases manual bounds overrides |
| Enable dynamic exposure | Controls curve evaluation independently of manual preview |
| Record sample | Only with the Custom source: records an available applied snapshot as a new sample and saves it |
| Remove sample N | Deletes and saves the specified Custom record without changing the submitted runtime curve |

Center and Width accept numeric text. Width must be nonnegative, and bounds must remain within -10 to 20 EV100. Invalid or incomplete text is retained for editing without changing valid values; Status explains the reason. Recording is rejected when no exposure snapshot is available.

After editing Custom samples, Apply custom curve must be invoked again to change the runtime curve. Deleting all records does not disable an existing runtime curve, and an empty list cannot be submitted. Recording or deletion notifies the panel through SamplesChanged to refresh it; the Controller does not directly request UI rebuilding.

## Debug time control and lifecycle

Time accepts decimal hours in [0,24), for example 5.85 means 05:51. The first takeover saves PlanetarySystem's time and override state, then sets overrideTime=true. Release time control restores the saved state. If another caller has changed the override value, the controller relinquishes ownership without overwriting the other caller's new value.

Closing the menu does not release preview or time control. City preload, Debug stop, mod disposal, and system destruction release temporary time and manual preview. The dynamic system releases its Volume/Profile on city preload, stop, disposal, and destruction, then recreates them according to scene resource availability. The World can survive across cities, so system destruction cannot replace city-loading notifications.

Curve selection, the dynamic switch, and calibration parameters are session state and are not written to city saves. Custom samples use a separate file. Disabling Debug does not discard curves or samples and does not stop dynamic exposure.

## Sample file

The Custom file is SunGlasses/exposure-calibration.json under Unity Application.persistentDataPath, retaining the version 1 format:

```json
{
  "version": 1,
  "samples": [{
    "recordedAtUtc": "2026-10-04T00:00:00.0000000Z",
    "timeOfDayHours": 6.5,
    "solarElevationDegrees": 5.0,
    "centerEv100": 11.5,
    "widthEv100": 1.0
  }]
}
```

Serialization uses DataContractJsonSerializer. Loading validates the entire file first; failure preserves the previous list and file. After a read failure, saving is blocked until Custom is successfully selected again. Older files containing only version are treated as empty lists. Recording or deletion triggers saving: all records are serialized and read back for validation before replacement through a temporary file. Errors appear in Status and logs.

## Implementation alignment and verification

The separation was completed on 2026-10-04. Official C# compilation, mod post-processing, and local deployment passed with 0 warnings and 0 errors. Built and deployed DLLs had matching SHA256 hashes. The following were verified in a loaded city through the Unity debugging connection and game UI:

- realistic was enabled by default, dormant in the main menu without a sun, and active in the city. Main-camera bounds matched, and the mode remained AutomaticHistogram.
- Manual center 10 and width 2 produced 9..11 EV100. Disabling preview restored the curve; invalid input did not change valid parameters.
- Selecting Custom preserved the realistic runtime curve. Recording successfully saved a version 1 file, and explicit application produced the range corresponding to the recorded sample.
- After deleting and reloading the calibration list, the runtime curve still retained its previously submitted copy.
- bright took effect at a fixed evening time; known realistic nodes and mirrored nodes both returned 11.5..12.5 EV100.
- With simulation speed 0, Debug time changes continued updating exposure and matching the main camera.
- Disabling Debug released time and manual overrides while dynamic exposure continued. Disabling the dynamic system released resources; re-enabling recreated them and restored their effect.
- The Debug panel built successfully, and logs contained no related exposure exceptions. After testing, the original Custom file was restored with an identical hash, along with realistic, normal simulation speed, and no time override.

An actual second city load and a full process-exit regression had not been performed for this verification record. Cleanup is implemented through preload, stop, and destruction entry points. Visual quality across different maps, seasons, weather, and camera directions still requires further manual calibration.
