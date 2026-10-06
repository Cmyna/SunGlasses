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


        internal static int InternalVolumePriority = 2500;

        internal static float SunSizeMultiplier = 1f;

        internal static float SunBloomMultiplier = 1f;

        internal static float SkyExposure = VanillaSkyExposure;

        internal static float SunLightIntensity = DefaultPbSunIntensity;

        internal static float IndirectLightingMultipilier = VanillaIndirectDiffuseLighting;

        internal static bool EnableLensFlare = true;

        private Volume remapVolume;

        private Bloom globalBloom;

        private PlanetarySystem planetarySystem;


        protected override void OnCreate()
        {
            base.OnCreate();
            remapVolume = VolumeHelper.CreateVolume("RemapLightVolume", InternalVolumePriority);
            VolumeHelper.GetOrCreateVolumeComponent(remapVolume, ref globalBloom);
            planetarySystem = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

        protected override void OnUpdate()
        {
            UpdatePriority();

            // override vanilla lighting system
            TryUpdateVanillaLighting();
            UpdateGroundTint();
            TryUpdateLensFlareComponent();

            OverrideSunSize(SunSizeMultiplier);
            OverrideSunBloom(SunBloomMultiplier);
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
        private void TryUpdateVanillaLighting()
        {
            if (!TryGetVanillaLighting(out var volume, out var sky, out var controller))
            {
                return;
            }
            sky.exposure.Override(SkyExposure);
            controller.indirectDiffuseLightingMultiplier.Override(IndirectLightingMultipilier);
        }

        private void UpdatePriority()
        {
            var priority = InternalVolumePriority;
            if (VolumePriority.overrideState) priority = VolumePriority.value;
            if (remapVolume.priority != priority) remapVolume.priority = priority;
        }

        private void TryUpdateLensFlareComponent()
        {
            if (!TryGetLensFlareComponent(out var comp))
            {
                return;
            }
            comp.enabled = EnableLensFlare;
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
