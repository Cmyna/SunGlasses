// CopyRight (c) CMyna. All Rights Preserved.
// file "RemapLightingSystem.cs".
// Licensed under MIT License.

using Game;
using Game.Rendering;
using Game.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    // TODO: enable/disable lensflare function
    partial class RemapLightingSystem : GameSystemBase
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

        internal static int BrightenDarknessLevel = 0;

        internal static float SunLightIntensity = DefaultPbSunIntensity;

        internal static float IndirectLightingMultipilier = VanillaIndirectDiffuseLighting;

        internal static bool EnableLensFlare = true;

        internal float[,] AutoHistgramLevels = {
            {40, 90},
            {30, 60},
            {10, 40},
            {0, 20}
        };



        private Volume remapVolume;

        private Bloom globalBloom;

        private Exposure lightExposure;

        private PlanetarySystem planetarySystem;

        private VanillaLighting vanillaLighting = default;

        private LensFlareComponentSRP lensFlareComponent;


        public struct VanillaLighting
        {
            public Volume volume;
            public VolumeProfile profile;
            public PhysicallyBasedSky sky;
            public IndirectLightingController indirectLightingController;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            remapVolume = VolumeHelper.CreateVolume("RemapLightVolume", InternalVolumePriority);
            VolumeHelper.GetOrCreateVolumeComponent(remapVolume, ref globalBloom);
            VolumeHelper.GetOrCreateVolumeComponent(remapVolume, ref lightExposure);
            lightExposure.mode.Override(ExposureMode.AutomaticHistogram);
            planetarySystem = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

        protected override void OnUpdate()
        {
            UpdatePriority();
            TryGetVanillaLightingVolume();

            // sun properties override
            OverrideSunSize(SunSizeMultiplier);
            OverrideSunBloom(SunBloomMultiplier);

            // override vanilla lighting system
            TryUpdateVanillaLighting();

            BrightenDarkExposure(BrightenDarknessLevel);

            TryUpdateLensFlareComponent();
        }


        public void OverrideSunSize(float weight)
        {
            var sunLight = planetarySystem.SunLight;
            if (!sunLight.isValid) return;
            var additionalData = sunLight.additionalData;
            additionalData.angularDiameter = VanillaAngularDiameter * weight;
            additionalData.flareSize = VanillaFlareSize * weight;
        }


        public void OverrideSunBloom(float weight)
        {
            // override the global bloom to toggle sun bloom
            globalBloom.intensity.Override(VanillaBloom * weight);
        }

        public void BrightenDarkExposure(int level)
        {
            if (lightExposure == null) return;
            if (level > 3 || level < 0) return;
            lightExposure.histogramPercentages.overrideState = true;
            var x = AutoHistgramLevels[level, 0];
            var y = AutoHistgramLevels[level, 1];
            // should use Override method to make it work
            lightExposure.histogramPercentages.Override(new Vector2 { x = x, y = y });
        }


        /// <summary>
        /// try get Physically Base Sky 
        /// </summary>
        private void TryUpdateVanillaLighting()
        {
            if (vanillaLighting.sky == null || vanillaLighting.indirectLightingController == null) return;
            vanillaLighting.sky.exposure.Override(SkyExposure);
            vanillaLighting.indirectLightingController.indirectDiffuseLightingMultiplier.Override(IndirectLightingMultipilier);
        }

        private void UpdatePriority()
        {
            var priority = InternalVolumePriority;
            if (VolumePriority.overrideState) priority = VolumePriority.value;
            if (remapVolume.priority != priority) remapVolume.priority = priority;
        }
        
        private void TryGetVanillaLightingVolume()
        {
            if (vanillaLighting.volume == null)
            {
                vanillaLighting.volume = GameObject.Find(kLightingPostprocessVolume).GetComponent<Volume>();
            } else
            {
                var profile = vanillaLighting.volume.profile;
                if (vanillaLighting.sky == null) profile.TryGet(out vanillaLighting.sky);
                if (vanillaLighting.indirectLightingController == null) profile.TryGet(out vanillaLighting.indirectLightingController);
            }
            
        }

        private void TryUpdateLensFlareComponent()
        {
            // the LensFlareComponent under same object with HDAdditionalData
            if (lensFlareComponent == null && planetarySystem.SunLight.isValid )
            {
                var sunLightGo = planetarySystem.SunLight.additionalData?.gameObject;
                sunLightGo?.TryGetComponent(out lensFlareComponent);
            }
            if (lensFlareComponent != null && (EnableLensFlare != lensFlareComponent.enabled))
            {
                lensFlareComponent.enabled = EnableLensFlare;
            }
        }

    }
}
