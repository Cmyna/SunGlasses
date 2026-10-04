using UnityEngine;

namespace SunGlasses.Systems
{
    public partial class RemapLightingSystem
    {
        internal static float SunIntensityMultiplier = 1f;
        internal static float SunIntensityBeforeMultiplier;
        internal static float SunIntensityAfterMultiplier;

        private Light intensitySunLight;
        private float baselineSunIntensity;
        private float appliedSunIntensity;
        private bool hasAppliedSunIntensity;

        private void UpdateSunIntensityMultiplier()
        {
            var sun = planetarySystem.SunLight;
            if (!sun.isValid || sun.light == null)
            {
                ReleaseSunIntensity();
                SunIntensityBeforeMultiplier = 0f;
                SunIntensityAfterMultiplier = 0f;
                return;
            }

            if (intensitySunLight != sun.light)
            {
                ReleaseSunIntensity();
                intensitySunLight = sun.light;
            }

            var current = intensitySunLight.intensity;
            // If vanilla did not refresh the light (e.g. a paused frame), retain
            // the baseline instead of scaling our own previous output again.
            if (!hasAppliedSunIntensity || current != appliedSunIntensity)
                baselineSunIntensity = current;

            var scaled = baselineSunIntensity * SunIntensityMultiplier;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled)) return;
            appliedSunIntensity = scaled;
            intensitySunLight.intensity = appliedSunIntensity;
            hasAppliedSunIntensity = true;
            SunIntensityBeforeMultiplier = baselineSunIntensity;
            SunIntensityAfterMultiplier = appliedSunIntensity;
        }

        internal void ReleaseSunIntensity()
        {
            if (intensitySunLight != null && hasAppliedSunIntensity &&
                intensitySunLight.intensity == appliedSunIntensity)
                intensitySunLight.intensity = baselineSunIntensity;
            intensitySunLight = null;
            hasAppliedSunIntensity = false;
        }
    }
}
