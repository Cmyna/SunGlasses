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
        public const float VolumePriority = 3100f;

        private PlanetarySystem _planetary;
        private GameObject _host;
        private GameObject _lighting;
        private Volume _volume;
        private VolumeProfile _profile;
        private PhysicallyBasedSky _sky;
        private Fog _fog;
        private CloudLayer _clouds;
        private float _minimumFogDistance;
        private bool _dirty = true;
        private bool _loading;

        public AtmosphereParameters Parameters { get; private set; } = AtmosphereParameters.Defaults;
        public bool OverridesEnabled { get; set; } = true;
        public bool VolumeActive => _volume != null && _volume.enabled;
        public string Status { get; private set; } = "Waiting for lighting scene";

        protected override void OnCreate()
        {
            base.OnCreate();
            _planetary = World.GetOrCreateSystemManaged<PlanetarySystem>();
            // Read HDRP's actual constraint, including before any scene Volume exists.
            var fog = ScriptableObject.CreateInstance<Fog>();
            _minimumFogDistance = fog.meanFreePath.min;
            Object.Destroy(fog);
        }

        public bool TrySetParameters(AtmosphereParameters parameters)
        {
            if (!parameters.IsValid(_minimumFogDistance)) return false;
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
            _volume.enabled = OverridesEnabled;
            Status = OverridesEnabled ? "Atmosphere overrides active" : "Off: game / Photo Mode controls atmosphere";
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
            _sky.aerosolAnisotropy.Override(p.AerosolAnisotropy);
            _sky.aerosolTint.Override(p.AerosolTint);
            _sky.colorSaturation.Override(p.ColorSaturation);
            _sky.horizonTint.Override(p.HorizonTint);
            // Fog enable, color mode, height and all other cloud controls stay with the game.
            _fog.tint.Override(p.FogTint);
            _fog.meanFreePath.Override(p.FogAttenuationDistance);
            _clouds.layerA.ambientProbeDimmer.Override(p.CloudAmbientDimmer);
            _dirty = false;
        }

        private void SetDormant(string status)
        {
            if (_volume != null) _volume.enabled = false;
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
            Status = "Waiting for lighting scene";
        }

        public void Shutdown() { ReleaseResources(); }
        protected override void OnStopRunning() { Shutdown(); base.OnStopRunning(); }
        protected override void OnDestroy() { Shutdown(); base.OnDestroy(); }
    }
}
