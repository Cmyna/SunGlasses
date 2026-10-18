using System;
using System.Linq;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using SunGlasses;
using SunGlasses.Systems;
using SunGlasses.Debugging;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Preset = SunGlasses.Setting.ExposureCurvePreset;

var mod = new Mod();
var world = World.DefaultGameObjectInjectionWorld;
AssetDatabase.global.Load = settings =>
{
    Require(world.GetExistingSystemManaged<RemapLightingSystem>() == null, "settings load before render systems");
    settings.ColorCorrection = true;
    settings.ExposureCurve = Preset.Bright;
    settings.SkyExposure = 1.6f;
    settings.GroundFillLight = 0.4f;
};
mod.OnLoad(new Game.UpdateSystem());
var light = new GameObject("Sun").AddComponent<Light>();
var planetary = world.GetOrCreateSystemManaged<Game.Simulation.PlanetarySystem>();
planetary.SunLight.light = light;
planetary.SunLight.additionalData = light.gameObject.AddComponent<HDAdditionalLightData>();
light.gameObject.AddComponent<LensFlareComponentSRP>();
var remap = world.GetExistingSystemManaged<RemapLightingSystem>();
var exposure = world.GetExistingSystemManaged<DynamicExposureSystem>();
var atmosphere = world.GetExistingSystemManaged<AtmosphereSystem>();
var climate = world.GetOrCreateSystemManaged<Game.Simulation.ClimateSystem>();
var lighting = SceneVolume("LightingPostProcessVolume");
var sky = lighting.sharedProfile.Add<PhysicallyBasedSky>(false);
var indirect = lighting.sharedProfile.Add<IndirectLightingController>(false);
var groundVolume = SceneVolume("Render Settings");
var ground = groundVolume.sharedProfile.Add<PhysicallyBasedSky>(false).groundTint;
var assertions = 0;

if (args.Contains("--missing-curve"))
{
    Tick();
    Write(nameof(Setting.ExposureCurve), Preset.Realistic);
    Tick();
    var errors = ((Colossal.Logging.Log)Mod.Log).ErrorCount;
    Require(errors == 1 && exposure.ActiveCurve == "Unavailable" && !ExposureVolume().enabled, "missing resource disables exposure and logs once");
    for (var i = 0; i < 20; i++) Tick();
    Require(((Colossal.Logging.Log)Mod.Log).ErrorCount == errors, "failed resource is not retried each frame");
    Write(nameof(Setting.ExposureCurve), Preset.Bright);
    Tick();
    Require(exposure.GetSnapshot().Ready && exposure.ActiveCurve == "bright", "valid preset recovers after missing resource");
    Console.WriteLine("PASS: missing resource failure, retry suppression and preset recovery");
    return;
}

Tick();
Check(atmosphere.VolumeActive && exposure.GetSnapshot().Ready && exposure.ActiveCurve == "bright", "cold load uses persisted settings");
Check(Close(sky.exposure.value, 1.6f) && Close(ground.value.r, 0.4f), "cold load sky / ground settings");

// Assign through reflection exactly as an external restore does. Never call Apply here.
var initialApplyCount = Mod.Setting.ApplyCount;
Write(nameof(Setting.ColorCorrection), false);
Write(nameof(Setting.ExposureCurve), Preset.None);
Write(nameof(Setting.SkyExposure), 2.3f);
Write(nameof(Setting.GroundFillLight), 0.2f);
Write(nameof(Setting.SunSize), 2f);
Write(nameof(Setting.SunBloom), 1.5f);
Write(nameof(Setting.LensFlare), false);
Write(nameof(Setting.IndirectDiffuseSunLighting), 1.8f);
Tick();
Check(Mod.Setting.ApplyCount == initialApplyCount, "external restore does not call Apply");
Check(!atmosphere.VolumeActive && !ExposureVolume().enabled && light.color.Equals(Color.white), "external toggles affect rendering next update");
Check(Close(sky.exposure.value, 2.3f) && Close(ground.value.r, 0.2f), "external sky / ground affect next update");
Check(Close(planetary.SunLight.additionalData.angularDiameter, 3f) && !light.gameObject.GetComponent<LensFlareComponentSRP>().enabled, "sun size / flare read settings");
Check(Close(indirect.indirectDiffuseLightingMultiplier.value, 1.8f), "indirect light reads settings");
Check(Close(GameObject.Find("RemapLightVolume").GetComponent<Volume>().sharedProfile.components.OfType<Bloom>().Single().intensity.value, 0.3f), "bloom reads settings");

Write(nameof(Setting.ColorCorrection), true);
Write(nameof(Setting.ExposureCurve), Preset.Realistic);
RemapLightingSystem.SunTintBlendByElevation = false;
Tick();
Check(atmosphere.VolumeActive && light.color.Equals(RemapLightingSystem.SunTintColors[4]), "external enable restores atmosphere and sun tint");
var cache = Field(exposure, "_curve");
var unrelatedApplyCount = Mod.Setting.ApplyCount;
Tick();
Check(ReferenceEquals(cache, Field(exposure, "_curve")), "unchanged preset reuses curve object");
RemapLightingSystem.SunTintPreset = 5;
var custom = new ExposureCurve(new[] { new ExposureCurve.Point { timeOfDayHours = 10, solarElevationDegrees = 30, centerEv100 = 9, widthEv100 = 2 } });
exposure.SetCustomCurve(custom);
exposure.SetManualOverride(4, 5);
Write(nameof(Setting.SkyExposure), 1.1f);
Tick();
Check(RemapLightingSystem.SunTintOverrideActive && light.color.Equals(RemapLightingSystem.SunTintColors[5]), "unrelated restore preserves tint experiment");
Check(exposure.CustomCurveEnabled && exposure.ManualOverrideEnabled && Close(exposure.GetSnapshot().MinimumEv100, 4), "unrelated restore preserves exposure experiments");
Check(Mod.Setting.ExposureCurve == Preset.Realistic && Mod.Setting.ColorCorrection, "experiments do not change saved selection");
Check(Mod.Setting.ApplyCount == unrelatedApplyCount, "runtime reads do not save / apply settings");
Write(nameof(Setting.ColorCorrection), false);
Write(nameof(Setting.ExposureCurve), Preset.Bright);
Tick();
Check(!RemapLightingSystem.SunTintOverrideActive && light.color.Equals(Color.white), "relevant color change clears tint override");
Check(!exposure.CustomCurveEnabled && !exposure.ManualOverrideEnabled && exposure.ActiveCurve == "bright", "relevant preset change clears both exposure overrides");
exposure.SetCustomCurve(custom);
exposure.SetManualOverride(3, 4);
exposure.ClearTemporaryOverrides();
Tick();
Check(exposure.ActiveCurve == "bright" && !exposure.ManualOverrideEnabled && !exposure.CustomCurveEnabled, "explicit clear returns to unchanged preset");

// Defaults instances and persistence helpers must never change runtime configuration indirectly.
var currentSettings = Mod.Setting;
var defaults = new Setting(mod);
defaults.SetDefaults();
Tick();
Check(ReferenceEquals(currentSettings, Mod.Setting) && Mod.Setting.ExposureCurve == Preset.Bright && exposure.ActiveCurve == "bright", "throwaway defaults do not affect runtime");
var saveCount = Mod.Setting.SaveCount;
Mod.Setting.Update(s => s.GroundFillLight = 0.6f);
Tick();
Check(Mod.Setting.SaveCount == saveCount + 1 && Close(ground.value.r, 0.6f), "debug Update applies / saves same settings");

var atmosphereWidgets = AtmosphereDebugTab.Build();
var groundControl = atmosphereWidgets.OfType<DebugUI.FloatField>().Single();
var colorControl = atmosphereWidgets.OfType<DebugUI.BoolField>().First();
var widgetsSaveCount = Mod.Setting.SaveCount;
groundControl.setter(0.45f); colorControl.setter(true); Tick();
Check(Close(Mod.Setting.GroundFillLight, 0.45f) && Close(ground.value.r, 0.45f) && colorControl.getter() && atmosphere.VolumeActive,
    "real atmosphere widgets modify formal settings and rendering");
Check(Mod.Setting.SaveCount == widgetsSaveCount + 2, "real atmosphere widget edits save settings");
Write(nameof(Setting.GroundFillLight), 0.8f); Write(nameof(Setting.ColorCorrection), false); Tick();
Check(Close(groundControl.getter(), 0.8f) && !colorControl.getter(), "real widget getters observe external restore");
var exposureWidgets = ExposureDebugSystem.Instance.BuildWidgets();
var enableControl = exposureWidgets.OfType<DebugUI.BoolField>().Single();
exposure.SetCustomCurve(custom); exposure.SetManualOverride(2, 3);
exposureWidgets.OfType<DebugUI.Button>().First(w => w.displayName == "Use bright curve").action(); Tick();
Check(Mod.Setting.ExposureCurve == Preset.Bright && !exposure.CustomCurveEnabled && !exposure.ManualOverrideEnabled,
    "reselecting current preset through real widget clears experiments");
enableControl.setter(false); Tick();
Check(Mod.Setting.ExposureCurve == Preset.None && !enableControl.getter() && !ExposureVolume().enabled,
    "real exposure toggle disables formal preset");
enableControl.setter(true); Tick();
Check(Mod.Setting.ExposureCurve == Preset.Realistic && enableControl.getter() && exposure.GetSnapshot().Ready,
    "real exposure toggle enables realistic from None");
var sunWidgets = SunLightingDebugTab.Build();
sunWidgets.OfType<DebugUI.EnumField>().Single().setter(5); Tick();
Check(!Mod.Setting.ColorCorrection && RemapLightingSystem.SunTintOverrideActive,
    "real sun tint widget is explicitly temporary");
sunWidgets.OfType<DebugUI.Button>().First(w => w.displayName == "Clear temporary sun tint preset").action(); Tick();
Check(!RemapLightingSystem.SunTintOverrideActive && light.color.Equals(Color.white), "real clear tint widget restores formal baseline");

var oldAtmosphere = GameObject.Find(AtmosphereSystem.VolumeName);
atmosphere.Preload(); exposure.Preload();
Write(nameof(Setting.ColorCorrection), true);
Write(nameof(Setting.ExposureCurve), Preset.Realistic);
Write(nameof(Setting.GroundFillLight), 0.7f);
atmosphere.Loaded(); exposure.Loaded();
Tick();
Check(oldAtmosphere == null && atmosphere.VolumeActive && exposure.ActiveCurve == "realistic" && Close(ground.value.r, 0.7f), "city recreation uses latest setting values");
remap.Enabled = false; exposure.Enabled = false; atmosphere.Enabled = false;
Write(nameof(Setting.GroundFillLight), 0.3f);
remap.Enabled = true; exposure.Enabled = true; atmosphere.Enabled = true;
Tick();
Check(atmosphere.VolumeActive && exposure.GetSnapshot().Ready && Close(ground.value.r, 0.3f), "systems resume with latest settings");
Check(GameObject.Find("SunGlasses Dynamic Exposure").GetComponent<Volume>().priority == 1500 && GameObject.Find(AtmosphereSystem.VolumeName).GetComponent<Volume>().priority == 1500, "exposure / atmosphere retain Photo Mode precedence");
climate.fog.overrideState = true; Tick();
Check(atmosphere.FogOverridesSuspended && !GameObject.Find(AtmosphereSystem.VolumeName).GetComponent<Volume>().sharedProfile.components.OfType<Fog>().Single().tint.overrideState, "custom fog suspends tint override");
climate.fog.overrideState = false; Tick();
Check(!atmosphere.FogOverridesSuspended && atmosphere.VolumeActive, "custom fog release restores atmosphere");

Write(nameof(Setting.ExposureCurve), (Preset)99); Tick();
var errorCount = ((Colossal.Logging.Log)Mod.Log).ErrorCount;
for (var i = 0; i < 20; i++) Tick();
Check(errorCount == 1 && ((Colossal.Logging.Log)Mod.Log).ErrorCount == errorCount && !ExposureVolume().enabled, "failed preset disables override without log spam");
Write(nameof(Setting.ExposureCurve), Preset.Bright); Tick();
Check(exposure.GetSnapshot().Ready, "valid preset recovers after failure");

exposure.SetCustomCurve(custom); exposure.SetManualOverride(2, 3); RemapLightingSystem.SunTintPreset = 5;
var resetSaveCount = Mod.Setting.SaveCount;
Mod.Setting.ResetModSettings = true; Tick();
Check(!exposure.CustomCurveEnabled && !exposure.ManualOverrideEnabled && !RemapLightingSystem.SunTintOverrideActive, "reset defaults clears temporary overrides");
Check(!atmosphere.VolumeActive && !ExposureVolume().enabled && Close(ground.value.r, 1) && Mod.Setting.SaveCount == resetSaveCount + 1, "reset defaults applies and saves default values");

mod.OnDispose();
Check(Mod.Setting == null && GameObject.Find("RemapLightVolume") == null && GameObject.Find(AtmosphereSystem.VolumeName) == null, "dispose releases volumes and settings");
remap.Enabled = true; exposure.Enabled = true; atmosphere.Enabled = true; Tick();
Check(GameObject.Find("RemapLightVolume") == null && GameObject.Find(AtmosphereSystem.VolumeName) == null && GameObject.Find("SunGlasses Dynamic Exposure") == null, "no settings means no rendering overrides");
AssetDatabase.global.Load = null;
mod.OnLoad(new Game.UpdateSystem()); Tick();
Check(!atmosphere.VolumeActive && !exposure.GetSnapshot().Ready && Close(ground.value.r, 1), "same-world mod reload starts from new settings instance");
mod.OnDispose();
Console.WriteLine($"PASS: {assertions} settings / runtime assertions (production sources, engine stubs)");

void Tick() { remap.Tick(); exposure.Tick(); atmosphere.Tick(); }
void Write(string name, object value) => typeof(Setting).GetProperty(name).SetValue(Mod.Setting, value);
void Check(bool value, string message) { Require(value, message); assertions++; }
static bool Close(float x, float y) => Math.Abs(x - y) < 0.00001f;
static void Require(bool value, string message) { if (!value) throw new Exception(message); }
static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
static Volume SceneVolume(string name)
{
    var result = new GameObject(name).AddComponent<Volume>();
    result.sharedProfile = new VolumeProfile(); return result;
}
Volume ExposureVolume() => GameObject.Find("SunGlasses Dynamic Exposure").GetComponent<Volume>();
