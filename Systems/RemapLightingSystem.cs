// CopyRight (c) CMyna. All Rights Preserved.
// file "RemapLightingSystem.cs".
// Licensed under MIT License.

using Game;
using Game.Rendering;
using Game.Simulation;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    // TODO: enable/disable lensflare function
    public partial class RemapLightingSystem : GameSystemBase
    {

        public const string kLightingPostprocessVolume = "LightingPostProcessVolume";

        public const float VanillaAngularDiameter = 1.5f;

        public const float VanillaFlareSize = 2f;

        public const float VanillaBloom = 0.2f;

        public const float VanillaSkyExposure = 1f;

        public const float VanillaIndirectDiffuseLighting = 1f;

        /// <summary>
        /// this value is actually the unity suggest intensity setting
        /// from https://forum.unity.com/threads/physically-based-sky.765962/page-2#post-7477868,
        /// In practice, it takes extremely small effect on sun/sky lighting, only 0-1000 will have visible change.
        /// And also, setting it to 130000 lux doesn't affect night lighting
        /// </summary>
        public const float DefaultPbSunIntensity = 130000;


        /// <summary>
        /// For outsider to override system holded volume's priority
        /// </summary>
        public IntParameter VolumePriority = new IntParameter(2500, false);


        internal const int DefaultVolumePriority = 2500;

        private Volume remapVolume;

        private Bloom globalBloom;

        private PlanetarySystem planetarySystem;


        protected override void OnCreate()
        {
            base.OnCreate();
            remapVolume = VolumeHelper.CreateVolume("RemapLightVolume", DefaultVolumePriority);
            VolumeHelper.GetOrCreateVolumeComponent(remapVolume, ref globalBloom);
            planetarySystem = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

        protected override void OnUpdate()
        {
            var settings = Mod.Setting;
            if (settings == null) { Shutdown(); return; }
            if (remapVolume == null)
            {
                remapVolume = VolumeHelper.CreateVolume("RemapLightVolume", DefaultVolumePriority);
                VolumeHelper.GetOrCreateVolumeComponent(remapVolume, ref globalBloom);
            }
            UpdatePriority();

            // override vanilla lighting system
            TryUpdateVanillaLighting(settings);
            UpdateGroundTint();
            TryUpdateLensFlareComponent(settings.LensFlare);

            OverrideSunSize(settings.SunSize);
            OverrideSunBloom(settings.SunBloom);
            UpdateSunTint();
            UpdateSunSpecularLight();

        }


        public void OverrideSunSize(float weight)
        {
            var sunLight = planetarySystem.SunLight;
            if (!sunLight.isValid)
            {
                return;
            }
            var additionalData = sunLight.additionalData;
            additionalData.angularDiameter = VanillaAngularDiameter * weight;
            additionalData.flareSize = VanillaFlareSize * weight;
        }


        public void OverrideSunBloom(float weight)
        {
            // override the global bloom to toggle sun bloom
            globalBloom.intensity.Override(VanillaBloom * weight);
        }

        /// <summary>
        /// try get Physically Base Sky 
        /// </summary>
        private void TryUpdateVanillaLighting(Setting settings)
        {
            if (!TryGetVanillaLighting(out var volume, out var sky, out var controller))
            {
                return;
            }
            sky.exposure.Override(settings.SkyExposure);
            controller.indirectDiffuseLightingMultiplier.Override(settings.IndirectDiffuseSunLighting);
        }

        private void UpdatePriority()
        {
            var priority = DefaultVolumePriority;
            if (VolumePriority.overrideState) priority = VolumePriority.value;
            if (remapVolume.priority != priority) remapVolume.priority = priority;
        }

        private void TryUpdateLensFlareComponent(bool enabled)
        {
            if (!TryGetLensFlareComponent(out var comp))
            {
                return;
            }
            comp.enabled = enabled;
        }

        internal void ResetTemporaryOverrides()
        {
            ClearSunTintOverride();
            SunTintBlendByElevation = true;
            SunTintWhiteAngle = 10f;
            EnableSunSpecularLight = false;
            SunSpecularLightDimmer = DefaultSunSpecularLightDimmer;
            SunSpecularZenithDimmer = DefaultSunSpecularZenithDimmer;
            VolumePriority.overrideState = false;
        }

        internal void Shutdown()
        {
            ReleaseGroundTint();
            ReleaseSunTint();
            ReleaseSunSpecularLight();
            if (remapVolume != null)
            {
                var profile = remapVolume.sharedProfile;
                if (profile != null)
                {
                    foreach (var component in profile.components) UnityEngine.Object.Destroy(component);
                    UnityEngine.Object.Destroy(profile);
                }
                UnityEngine.Object.Destroy(remapVolume.gameObject);
            }
            remapVolume = null;
            globalBloom = null;
        }


        private bool TryGetLensFlareComponent(out LensFlareComponentSRP component)
        {
            component = null;
            if (!planetarySystem.SunLight.isValid) return false;
            var sunLightGo = planetarySystem.SunLight.additionalData?.gameObject;
            sunLightGo?.TryGetComponent(out component);
            return component != null;
        }

        private bool TryGetVanillaLighting(
            out Volume volume, 
            out PhysicallyBasedSky sky, 
            out IndirectLightingController controller
        ) {
            sky = null; controller = null;
            var lightingObject = GameObject.Find(kLightingPostprocessVolume);
            volume = lightingObject != null ? lightingObject.GetComponent<Volume>() : null;
            if (volume == null) return false;
            var profile = volume.profile;
            return profile.TryGet(out sky) && profile.TryGet(out controller);
        }
    }
}
