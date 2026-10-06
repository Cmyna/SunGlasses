using Colossal.Serialization.Entities;
using Game;
using Game.SceneFlow;
using Game.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    public partial class AtmosphereSystem : GameSystemBase
    {
        public const string VolumeName = "SunGlasses Atmosphere Volume";
        public const float VolumePriority = DynamicExposureSystem.VolumePriority;

        private PlanetarySystem _planetary;
        private ClimateSystem _climate;
        private GameObject _host;
        private GameObject _lighting;
        private Volume _volume;
        private VolumeProfile _profile;
        private PhysicallyBasedSky _sky;
        private Fog _fog;
        private CloudLayer _clouds;
        private bool _dirty = true;
        private bool _loading;

        public AtmosphereParameters Parameters { get; private set; } = AtmosphereParameters.Defaults;
        public bool OverridesEnabled { get; set; } = true;
        public bool VolumeActive => _volume != null && _volume.enabled;
        public bool FogOverridesSuspended { get; private set; }
        public float HorizonTintElevation { get; private set; }
        public float HorizonTintWhiteWeight { get; private set; }
        public Color ActiveHorizonTint { get; private set; } = AtmosphereParameters.Defaults.HorizonTint;
        public string Status { get; private set; } = "Waiting for lighting scene";

        protected override void OnCreate()
        {
            base.OnCreate();
            _planetary = World.GetOrCreateSystemManaged<PlanetarySystem>();
            _climate = World.GetOrCreateSystemManaged<ClimateSystem>();
        }

        public bool TrySetParameters(AtmosphereParameters parameters)
        {
            if (!parameters.IsValid()) return false;
            Parameters = parameters;
            _dirty = true;
            return true;
        }

        protected override void OnUpdate()
        {
            if (_loading || !_planetary.SunLight.isValid || _planetary.SunLight.transform == null)
            {
                SetDormant("Waiting for lighting scene");
                return;
            }
            if (_lighting == null) _lighting = GameObject.Find(RemapLightingSystem.kLightingPostprocessVolume);
            if (_lighting == null)
            {
                SetDormant("Waiting for lighting Volume");
                return;
            }
            if (_host == null) CreateVolume();
            _host.layer = _lighting.layer;
            if (_dirty) ApplyParameters();
            UpdateHorizonTint();
            // Respect custom fog from the game Debug menu or mods using its native API.
            // Disable our flags instead of writing into the game's climate Volume.
            FogOverridesSuspended = _climate.fog.overrideState;
            _fog.tint.overrideState = !FogOverridesSuspended;
            _volume.enabled = OverridesEnabled;
            Status = !OverridesEnabled ? "Off: game / Photo Mode controls atmosphere"
                : FogOverridesSuspended ? "Atmosphere overrides active; custom fog controls fog"
                : "Atmosphere overrides active";
        }

        private void CreateVolume()
        {
            // A scene may destroy the host while the system and its profile survive.
            var lighting = _lighting;
            ReleaseResources();
            _lighting = lighting;
            _host = new GameObject(VolumeName);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SunGlasses Atmosphere Profile";
            _sky = _profile.Add<PhysicallyBasedSky>(false);
            _fog = _profile.Add<Fog>(false);
            _clouds = _profile.Add<CloudLayer>(false);
            _volume = _host.AddComponent<Volume>();
            _volume.enabled = false;
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;
            _dirty = true;
        }

        private void ApplyParameters()
        {
            var p = Parameters;
            _sky.aerosolDensity.Override(p.AerosolDensity);
            _sky.aerosolTint.Override(p.AerosolTint);
            _sky.colorSaturation.Override(p.ColorSaturation);
            // Fog distance, enable, color mode, height and all other cloud controls stay with the game.
            _fog.tint.Override(p.FogTint);
            _clouds.layerA.ambientProbeDimmer.Override(p.CloudAmbientDimmer);
            _dirty = false;
        }

        private void UpdateHorizonTint()
        {
            var p = Parameters;
            // Match sun tint's elevation convention, using this frame's actual sun direction.
            HorizonTintElevation = Mathf.Asin(Mathf.Clamp(-_planetary.SunLight.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            HorizonTintWhiteWeight = p.HorizonTintBlendByElevation
                ? Mathf.Clamp01(HorizonTintElevation / p.HorizonTintWhiteAngle) : 0f;
            ActiveHorizonTint = Color.Lerp(p.HorizonTint, Color.white, HorizonTintWhiteWeight);
            _sky.horizonTint.Override(ActiveHorizonTint);
        }

        private void SetDormant(string status)
        {
            if (_volume != null) _volume.enabled = false;
            FogOverridesSuspended = false;
            Status = status;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            _loading = true;
            ReleaseResources();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            _loading = false;
        }

        private void ReleaseResources()
        {
            if (_volume != null) _volume.enabled = false;
            if (_host != null) Object.Destroy(_host);
            if (_profile != null)
            {
                foreach (var component in _profile.components) Object.Destroy(component);
                Object.Destroy(_profile);
            }
            _host = null;
            _lighting = null;
            _volume = null;
            _profile = null;
            _sky = null;
            _fog = null;
            _clouds = null;
            _dirty = true;
            FogOverridesSuspended = false;
            Status = "Waiting for lighting scene";
        }

        public void Shutdown() { ReleaseResources(); }
        protected override void OnStopRunning() { Shutdown(); base.OnStopRunning(); }
        protected override void OnDestroy() { Shutdown(); base.OnDestroy(); }
    }
}
