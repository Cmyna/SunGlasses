using System;
using Game;
using Colossal.Serialization.Entities;
using Game.Simulation;
using Game.SceneFlow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    public struct ExposureSnapshot
    {
        public bool Ready;
        public float TimeOfDayHours, SolarElevationDegrees, MinimumEv100, MaximumEv100;
    }
    public partial class DynamicExposureSystem : GameSystemBase
    {
        // Vanilla lighting is 1000; Photo Mode's CinematicControlVolume is 2000.
        public const float VolumePriority = 1500f;
        private PlanetarySystem _planetary;
        private GameObject _host;
        private Volume _volume;
        private VolumeProfile _profile;
        private Exposure _exposure;
        private ExposureCurve _curve;
        private GameObject _originalVolume;
        private ExposureSnapshot _snapshot;
        partial void ResetExposureOverride();
        partial void ApplyExposureOverride(ref ExposureSnapshot snapshot, ref string description);
        public bool DynamicEnabled { get; set; } = true;
        public Setting.ExposureCurvePreset SelectedPreset { get; private set; } = Setting.ExposureCurvePreset.Realistic;
        public string ActiveCurve { get; private set; } = "realistic";
        public string InterpolationDescription { get; private set; } = "Waiting for sun";
        protected override void OnCreate()
        {
            base.OnCreate();
            _planetary = World.GetOrCreateSystemManaged<PlanetarySystem>();
            try { SetCurve(ExposureCurve.LoadBundled("realistic"), "realistic"); }
            catch (Exception ex) { Mod.Log.Error("Could not load default exposure curve: " + ex.Message); }
        }
        protected override void OnUpdate() { EnsureVolume(); ApplyExposure(); }
        public void SetCurve(ExposureCurve curve, string name)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            _curve = curve; ActiveCurve = name; DynamicEnabled = true;
        }
        public void ApplyPreset(Setting.ExposureCurvePreset preset)
        {
            if (preset == Setting.ExposureCurvePreset.None)
            {
                ResetExposureOverride();
                _curve = null;
                ActiveCurve = "None";
                SelectedPreset = preset;
                DynamicEnabled = false;
                return;
            }
            var name = preset == Setting.ExposureCurvePreset.Bright ? "bright" : "realistic";
            if (_curve == null || ActiveCurve != name) SetCurve(ExposureCurve.LoadBundled(name), name);
            ResetExposureOverride();
            SelectedPreset = preset;
            DynamicEnabled = true;
        }
        public ExposureSnapshot GetSnapshot() => _snapshot;
        private ExposureSnapshot CalculateSnapshot()
        {
            var snapshot = new ExposureSnapshot();
            if (_planetary == null || !_planetary.SunLight.isValid || _planetary.SunLight.transform == null ||
                float.IsNaN(_planetary.time) || float.IsInfinity(_planetary.time)) return snapshot;
            snapshot.TimeOfDayHours = _planetary.time;
            snapshot.SolarElevationDegrees = Mathf.Asin(Mathf.Clamp(-_planetary.SunLight.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            var description = string.Empty;
            ApplyExposureOverride(ref snapshot, ref description);
            if (!snapshot.Ready)
            {
                if (!DynamicEnabled || _curve == null || !_curve.Evaluate(snapshot.SolarElevationDegrees, snapshot.TimeOfDayHours,
                    out var minimum, out var maximum, out description)) return snapshot;
                snapshot.MinimumEv100 = minimum;
                snapshot.MaximumEv100 = maximum;
                snapshot.Ready = true;
            }
            InterpolationDescription = description;
            return snapshot;
        }
        private void EnsureVolume()
        {
            if (_originalVolume == null) _originalVolume = GameObject.Find(RemapLightingSystem.kLightingPostprocessVolume);
            if (_originalVolume == null) { if (_volume != null) _volume.enabled = false; return; }
            if (_host == null)
            {
                var original = _originalVolume;
                ReleaseVolume();
                _originalVolume = original;
                _host = new GameObject("SunGlasses Dynamic Exposure");
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _exposure = _profile.Add<Exposure>(false);
                _exposure.limitMin.overrideState = _exposure.limitMax.overrideState = true;
                _volume = _host.AddComponent<Volume>();
                _volume.isGlobal = true; _volume.priority = VolumePriority; _volume.weight = 1f;
                _volume.sharedProfile = _profile;
            }
            _host.layer = _originalVolume.layer;
        }
        private void ApplyExposure()
        {
            _snapshot = default;
            if (_volume == null || _originalVolume == null)
            {
                InterpolationDescription = "Waiting for lighting volume";
                return;
            }
            var snapshot = CalculateSnapshot();
            _volume.enabled = snapshot.Ready;
            if (!snapshot.Ready) { InterpolationDescription = "Waiting for sun or exposure disabled"; return; }
            _exposure.limitMin.value = snapshot.MinimumEv100; _exposure.limitMax.value = snapshot.MaximumEv100;
            _snapshot = snapshot;
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { Shutdown(); }
        private void ReleaseVolume()
        {
            if (_volume != null) _volume.enabled = false;
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_profile != null) UnityEngine.Object.Destroy(_profile);
            _host = null; _volume = null; _profile = null; _exposure = null;
            _originalVolume = null; _snapshot = default;
        }
        public void Shutdown()
        {
            ResetExposureOverride();
            ReleaseVolume();
        }
        protected override void OnStopRunning() { Shutdown(); base.OnStopRunning(); }
        protected override void OnDestroy() { Shutdown(); base.OnDestroy(); }
    }
}
