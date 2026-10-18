using System;
using System.Collections.Generic;
using System.Globalization;
using Game;
using Colossal.Serialization.Entities;
using Game.SceneFlow;
using SunGlasses.Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    public partial class ExposureDebugSystem : GameSystemBase
    {
        public static ExposureDebugSystem Instance { get; private set; }
        private DynamicExposureSystem _runtime;
        private string _status = "Ready";
        partial void InitializeSampling();
        partial void ShutdownSampling();
        partial void DisposeSampling();
        partial void IncludeExposureOverride(ref bool exposureEnabled);
        protected override void OnCreate()
        {
            base.OnCreate();
            _runtime = World.GetOrCreateSystemManaged<DynamicExposureSystem>();
            InitializeSampling();
            Instance = this;
        }
        protected override void OnUpdate() { }
        public void UsePreset(Setting.ExposureCurvePreset preset)
        {
            try
            {
                _runtime.ClearTemporaryOverrides();
                Mod.Setting?.Update(setting => setting.ExposureCurve = preset);
                _status = "Using " + preset;
            }
            catch (Exception ex)
            {
                _status = "Preset switch failed: " + ex.Message;
                Mod.Log.Error(_status);
            }
        }

        internal List<DebugUI.Widget> BuildWidgets()
        {
            return new List<DebugUI.Widget>
            {
                new DebugUI.Button { displayName = "Use bright curve", action = () => UsePreset(Setting.ExposureCurvePreset.Bright) },
                new DebugUI.Button { displayName = "Use realistic curve", action = () => UsePreset(Setting.ExposureCurvePreset.Realistic) },
                new DebugUI.BoolField
                {
                    displayName = "Enable dynamic exposure",
                    getter = () => Mod.Setting != null && Mod.Setting.ExposureCurve != Setting.ExposureCurvePreset.None,
                    setter = value =>
                    {
                        if (!value) UsePreset(Setting.ExposureCurvePreset.None);
                        else if (Mod.Setting?.ExposureCurve == Setting.ExposureCurvePreset.None)
                            UsePreset(Setting.ExposureCurvePreset.Realistic);
                    }
                },
                new DebugUI.Value { displayName = "Saved exposure preset", getter = () => _runtime.SelectedPreset },
                new DebugUI.Value { displayName = "Exposure source", getter = () => _runtime.ExposureSource },
                new DebugUI.Button { displayName = "Clear temporary exposure overrides", action = _runtime.ClearTemporaryOverrides },
                new DebugUI.Value { displayName = "Runtime exposure curve", getter = () => _runtime.ActiveCurve },
                new DebugUI.Value { displayName = "Solar elevation", getter = SolarElevation },
                new DebugUI.Value { displayName = "Solar arc angle", getter = SolarArcAngleText },
                new DebugUI.Value { displayName = "Active interpolation samples", getter = () => _runtime.InterpolationDescription },
                new DebugUI.Value { displayName = "Applied exposure bounds", getter = AppliedRange },
                new DebugUI.Value { displayName = "Effective camera bounds", getter = EffectiveRange },
                new DebugUI.Value { displayName = "Exposure check", getter = ExposureCheck },
                new DebugUI.Value { displayName = "Status", getter = () => _status }
            };
        }

        public string SolarElevation()
        {
            var s = _runtime.GetSnapshot();
            return s.Ready ? s.SolarElevationDegrees.ToString("F3", CultureInfo.InvariantCulture) + "°" : "Waiting for sun / exposure";
        }
        public string SolarArcAngleText()
        {
            var s = _runtime.GetSnapshot();
            return s.Ready ? ExposureCurve.SolarArcAngle(s.SolarElevationDegrees, s.TimeOfDayHours).ToString("F3", CultureInfo.InvariantCulture) + "°" : "Waiting for sun / exposure";
        }
        public string AppliedRange()
        {
            var s = _runtime.GetSnapshot();
            return s.Ready ? string.Format(CultureInfo.InvariantCulture, "{0:F3} .. {1:F3} EV100", s.MinimumEv100, s.MaximumEv100) : "Waiting for sun / exposure";
        }
        public string EffectiveRange()
        {
            var camera = Camera.main;
            if (camera == null) return "No main camera";
            var exposure = HDCamera.GetOrCreate(camera, 0).volumeStack?.GetComponent<Exposure>();
            return exposure == null ? "Waiting for camera stack" :
                string.Format(CultureInfo.InvariantCulture, "{0:F3} .. {1:F3} EV100 ({2})", exposure.limitMin.value, exposure.limitMax.value, exposure.mode.value);
        }
        public string ExposureCheck()
        {
            var exposureEnabled = _runtime.DynamicEnabled;
            IncludeExposureOverride(ref exposureEnabled);
            if (!exposureEnabled) return "Off: game volumes control exposure";
            var snapshot = _runtime.GetSnapshot();
            if (!snapshot.Ready) return "Waiting for sun or lighting volume";
            var camera = Camera.main;
            if (camera == null) return "Waiting for main camera";
            var effective = HDCamera.GetOrCreate(camera, 0).volumeStack?.GetComponent<Exposure>();
            if (effective == null) return "Waiting for camera stack";
            if (effective.mode.value != ExposureMode.Automatic && effective.mode.value != ExposureMode.AutomaticHistogram)
                return "Effective exposure mode is " + effective.mode.value;
            return Mathf.Abs(effective.limitMin.value - snapshot.MinimumEv100) < 0.01f &&
                Mathf.Abs(effective.limitMax.value - snapshot.MaximumEv100) < 0.01f
                ? "Applied to main camera" : "Mismatch: check camera layer mask or competing Volume";
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode) { Shutdown(); }
        public void Shutdown()
        {
            ShutdownSampling();
        }
        protected override void OnStopRunning() { Shutdown(); base.OnStopRunning(); }
        protected override void OnDestroy()
        {
            Shutdown();
            DisposeSampling();
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
