using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Debug;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    public sealed class AtmosphereDebugController : MonoBehaviour
    {
        public const string VolumeName = "SunGlasses Atmosphere Volume";
        public static AtmosphereDebugController Instance { get; private set; }
        private Volume _volume;
        private VolumeProfile _profile;
        private PhysicallyBasedSky _sky;
        private Fog _fog;
        private CloudLayer _clouds;
        private bool _overridesEnabled = true;
        private string _densityText = "0.024";
        private string _anisotropyText = "0.8";
        private string _saturationText = "0.7";
        private string _cloudAmbientText = "1";
        private string _fogAttenuationDistanceText = "20000";
        private readonly string[] _tintText = { "1", "1", "1" };
        private readonly string[] _horizonText = { "0.5", "0.65", "1" };
        private readonly string[] _fogTintText = { "0.8", "0.8", "1" };
        public string Status { get; private set; } = "Ready";
        public bool OverridesEnabled
        {
            get => _overridesEnabled;
            set { _overridesEnabled = value; if (_volume != null) _volume.enabled = value; }
        }
        public string DensityText
        {
            get => _densityText;
            set { _densityText = value ?? ""; if (Parse(value, 0f, 1f, out var parsed)) _sky.aerosolDensity.value = parsed; }
        }
        public string CloudAmbientText
        {
            get => _cloudAmbientText;
            set
            {
                _cloudAmbientText = value ?? "";
                if (Parse(value, 0f, 1f, out var parsed))
                    _clouds.layerA.ambientProbeDimmer.value = parsed;
            }
        }
        public string AnisotropyText
        {
            get => _anisotropyText;
            set { _anisotropyText = value ?? ""; if (Parse(value, -1f, 1f, out var parsed)) _sky.aerosolAnisotropy.value = parsed; }
        }
        public string TintText(int channel) => _tintText[channel];
        public string FogTintText(int channel) => _fogTintText[channel];
        public string FogAttenuationDistanceText
        {
            get => _fogAttenuationDistanceText;
            set
            {
                _fogAttenuationDistanceText = value ?? "";
                if (Parse(value, _fog.meanFreePath.min, float.MaxValue, out var parsed))
                    _fog.meanFreePath.value = parsed;
            }
        }
        public void SetFogTintText(int channel, string text)
        {
            _fogTintText[channel] = text ?? "";
            if (!Parse(text, 0f, 1f, out var parsed)) return;
            var color = _fog.tint.value;
            color[channel] = parsed;
            _fog.tint.value = color;
        }
        public string SaturationText
        {
            get => _saturationText;
            set { _saturationText = value ?? ""; if (Parse(value, 0f, 1f, out var parsed)) _sky.colorSaturation.value = parsed; }
        }
        public string HorizonText(int channel) => _horizonText[channel];
        public void SetHorizonText(int channel, string text)
        {
            _horizonText[channel] = text ?? "";
            if (!Parse(text, 0f, 1f, out var parsed)) return;
            var color = _sky.horizonTint.value;
            color[channel] = parsed;
            _sky.horizonTint.value = color;
        }
        public void SetTintText(int channel, string text)
        {
            _tintText[channel] = text ?? "";
            if (!Parse(text, 0f, 1f, out var parsed)) return;
            var color = _sky.aerosolTint.value;
            color[channel] = parsed;
            _sky.aerosolTint.value = color;
        }
        private bool Parse(string text, float min, float max, out float value)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
            {
                Status = $"Enter a finite number from {min} to {max}; previous value retained";
                return false;
            }
            Status = "Applied";
            return true;
        }
        public static AtmosphereDebugController Install()
        {
            if (Instance != null) return Instance;
            var host = new GameObject(VolumeName);
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<AtmosphereDebugController>();
            DebugSystem.Rebuild((Func<List<DebugUI.Widget>>)AtmosphereDebugTab.Build);
            return Instance;
        }
        public static void Uninstall()
        {
            if (Instance == null) return;
            Instance.OverridesEnabled = false;
            Destroy(Instance.gameObject);
            Instance = null;
        }
        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SunGlasses Atmosphere Profile";
            // Only the selected Photo Mode atmosphere parameters participate.
            _sky = _profile.Add<PhysicallyBasedSky>(false);
            _sky.aerosolDensity.Override(0.012f);
            _sky.aerosolAnisotropy.Override(0.8f);
            _sky.aerosolTint.Override(Color.white);
            _sky.colorSaturation.Override(0.7f);
            _sky.horizonTint.Override(new Color(0.55f, 0.59499943f, 1f, 1f));
            // Leave fog enable, color mode and height to the game's Volumes.
            _fog = _profile.Add<Fog>(false);
            _fog.tint.Override(new Color(0.7f, 0.795f, 1f, 1f));
            _fog.meanFreePath.Override(20000f);
            // Only override ambient illumination of the first distant cloud layer.
            _clouds = _profile.Add<CloudLayer>(false);
            _clouds.layerA.ambientProbeDimmer.Override(1f);
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 3100f; // Above Photo Mode's 2000 and the normal lighting Volume.
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;
            _volume.enabled = _overridesEnabled;
            MatchLightingLayer();
        }
        private void LateUpdate() { MatchLightingLayer(); }
        private void MatchLightingLayer()
        {
            var lighting = GameObject.Find("LightingPostProcessVolume");
            if (lighting != null) gameObject.layer = lighting.layer;
        }
        private void OnDestroy()
        {
            if (_volume != null) _volume.enabled = false;
            if (_profile != null)
            {
                foreach (var component in _profile.components) Destroy(component);
                Destroy(_profile);
            }
            if (Instance == this) Instance = null;
        }
    }

    [DebugContainer]
    internal static class AtmosphereDebugTab
    {
        [DebugTab("SunGlasses Atmosphere", 103)]
        internal static List<DebugUI.Widget> Build()
        {
            var c = AtmosphereDebugController.Instance;
            if (c == null) return null;
            return new List<DebugUI.Widget>
            {
                new DebugUI.BoolField { displayName = "Enable atmosphere overrides", getter = () => c.OverridesEnabled, setter = value => c.OverridesEnabled = value },
                new DebugUI.TextField { displayName = "Aerosol density (0 - 1)", getter = () => c.DensityText, setter = value => c.DensityText = value },
                new DebugUI.TextField { displayName = "Aerosol anisotropy (-1 - 1)", getter = () => c.AnisotropyText, setter = value => c.AnisotropyText = value },
                new DebugUI.TextField { displayName = "Aerosol tint R (0 - 1)", getter = () => c.TintText(0), setter = value => c.SetTintText(0, value) },
                new DebugUI.TextField { displayName = "Aerosol tint G (0 - 1)", getter = () => c.TintText(1), setter = value => c.SetTintText(1, value) },
                new DebugUI.TextField { displayName = "Aerosol tint B (0 - 1)", getter = () => c.TintText(2), setter = value => c.SetTintText(2, value) },
                new DebugUI.TextField { displayName = "Atmosphere color saturation (0 - 1)", getter = () => c.SaturationText, setter = value => c.SaturationText = value },
                new DebugUI.TextField { displayName = "Horizon tint R (0 - 1)", getter = () => c.HorizonText(0), setter = value => c.SetHorizonText(0, value) },
                new DebugUI.TextField { displayName = "Horizon tint G (0 - 1)", getter = () => c.HorizonText(1), setter = value => c.SetHorizonText(1, value) },
                new DebugUI.TextField { displayName = "Horizon tint B (0 - 1)", getter = () => c.HorizonText(2), setter = value => c.SetHorizonText(2, value) },
                new DebugUI.TextField { displayName = "Fog tint R (0 - 1)", getter = () => c.FogTintText(0), setter = value => c.SetFogTintText(0, value) },
                new DebugUI.TextField { displayName = "Fog tint G (0 - 1)", getter = () => c.FogTintText(1), setter = value => c.SetFogTintText(1, value) },
                new DebugUI.TextField { displayName = "Fog tint B (0 - 1)", getter = () => c.FogTintText(2), setter = value => c.SetFogTintText(2, value) },
                new DebugUI.TextField { displayName = "Fog attenuation distance (meters)", getter = () => c.FogAttenuationDistanceText, setter = value => c.FogAttenuationDistanceText = value },
                new DebugUI.TextField { displayName = "Cloud layer A ambientProbeDimmer (0 - 1)", getter = () => c.CloudAmbientText, setter = value => c.CloudAmbientText = value },
                new DebugUI.Value { displayName = "Input status", getter = () => c.Status }
            };
        }
    }
}
