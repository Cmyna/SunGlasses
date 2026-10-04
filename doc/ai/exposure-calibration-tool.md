# Exposure calibration debug tab

The `SunGlasses Exposure` tab in the game's debug menu is an exposure curve and manual calibration tool. It uses a dedicated global HDRP Volume at priority 3000, above the game's observed priority 1000 and SunGlasses' existing priority 2500. The Volume copies the layer of `LightingPostProcessVolume`, has weight 1, and overrides only `Exposure.limitMin` and `Exposure.limitMax`. The bundled `realistic` curve and interpolation are enabled by default. Turning the preview off returns control to the other Volumes. Bundled curves cannot be edited; switch to Custom to record or remove calibration samples.

The Exposure component must be created with `VolumeProfile.Add<Exposure>(false)` before the two bound override flags are enabled. Passing `true` enables all 23 Exposure overrides, including `mode = Fixed` and `fixedExposure = 0`; a live game check on 2026-09-30 confirmed this made the view white even while the displayed bounds matched. Clearing the other override flags immediately restored `AutomaticHistogram` and normal rendering. The preview check now flags unexpected parameter overrides and a nonautomatic effective mode.

The time text field accepts decimal hours from `0` to less than `24`, for example `5.85` means 05:51. A valid entry immediately enables `PlanetarySystem.overrideTime` and sets its time. `Release time control` restores the previous override state and time. The controller also restores those values when the mod is disposed.

`Center (EV100)` and `Range width (EV100)` are numeric text fields. Valid edits enable and update the preview immediately. The resulting limits are `center ± width/2`. Entries are accepted only when both limits remain within -10 to 20 EV100 and width is nonnegative. Invalid or incomplete text stays visible for editing but does not change the applied values; the status row explains the error. The main-camera effective limits and a match check are shown separately because values in a Volume profile alone do not prove they won the HDRP stack.

`Record sample` stores UTC timestamp, time of day in decimal hours, solar elevation in degrees, center EV100 and width EV100. Each listed sample has a `Remove sample N` button that removes that specific record. The list is rebuilt after recording or removing a sample. Records are saved to `SunGlasses/exposure-calibration.json` under Unity's `Application.persistentDataPath`. Its versioned JSON shape is:

```json
{
  "version": 1,
  "samples": [
    {
      "recordedAtUtc": "2026-09-30T00:00:00.0000000Z",
      "timeOfDayHours": 6.5,
      "solarElevationDegrees": 5.0,
      "centerEv100": 11.5,
      "widthEv100": 1.0
    }
  ]
}
```

`Interpolate samples by solar angle` evaluates the sun on a signed circular arc from -180° to 180°. Before noon its arc angle is the measured elevation; after noon positive elevations map to `180° - elevation` and negative elevations to `-180° - elevation`. Thus a morning sample at 27.32° also supplies a virtual node at 152.68°, while a -1° sample supplies one at -179°. Every real sample contributes its mirrored node only during interpolation; the list and JSON contain real samples alone. The two nodes belonging to the highest morning sample enclose angles such as 50° and return that sample's exposure bounds throughout that interval. Neighboring nodes are interpolated by angular distance, including across the ±180° seam. Game time selects the morning or evening half of the arc but never supplies interpolation distance. A single sample supplies a constant range. The `Solar arc angle` row shows the coordinate used for interpolation. Enabling interpolation also enables the preview Volume; the `Preview exposure bounds` switch remains the master control for applying it. The `Interpolated bounds` row shows the computed range. Editing center or width switches back to manual bounds. Recording while interpolation is enabled stores the currently computed range. If the last sample is removed, interpolation and preview turn off. Interpolation is a session-only debug setting.

The sample file is loaded at startup and written after every record or removal. Loading does not clear records already held in memory. The entire file is validated before its records are added; if reading or validation fails, subsequent saves are skipped so a blank or partial list cannot overwrite the existing file. `DataContractJsonSerializer` writes explicitly marked fields and checks a read-back of every sample before replacing the file. Unity's `JsonUtility` omitted the `samples` field from the old container, producing a file with only `version`. A legacy file containing only `version: 1` loads as an empty list. Load and save failures are reported in the tab and log.

## Live verification

The C# compiler and game mod postprocessor both succeeded. On 2026-10-04 a startup check reproduced `SunLight.isValid == true` while its scene Transform was null. Reading its direction raised an exception every frame after enabling the default realistic curve. The controller now requires a valid sun, a non-null Transform and finite game time before interpolation, angle display or recording. The override stays dormant until the sun is ready and resumes automatically when it becomes available. Disabled preview skips interpolation entirely. After deployment and restart, the realistic curve remained enabled, angle/bounds rows returned their waiting status and the current Player.log contained no exposure exceptions.

For further scene validation, set a fixed time and confirm `Preview check` says `Applied to main camera`. Check terrain and sky camera angles, dawn and dusk in small time steps, pause, menu close/reopen, save changes, and mod disposal. A mismatch means the camera's Volume layer mask or another higher-priority Volume needs inspection. Confirm custom samples are created and loaded on the next game launch.
