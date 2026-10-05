using UnityEngine;

namespace SunGlasses.Systems
{
    // Value-type configuration: callers cannot mutate the system's stored copy.
    public struct AtmosphereParameters
    {
        public float AerosolDensity;
        public float AerosolAnisotropy;
        public Color AerosolTint;
        public float ColorSaturation;
        public Color HorizonTint;
        public bool HorizonTintBlendByElevation;
        public float HorizonTintWhiteAngle;
        public Color FogTint;
        public float FogAttenuationDistance;
        public float CloudAmbientDimmer;

        public static AtmosphereParameters Defaults => new AtmosphereParameters
        {
            AerosolDensity = 0.024f,
            AerosolAnisotropy = 0.8f,
            AerosolTint = Color.white,
            ColorSaturation = 0.7f,
            HorizonTint = new Color(0.5f, 0.65f, 1f, 1f),
            HorizonTintBlendByElevation = true,
            HorizonTintWhiteAngle = 90f,
            FogTint = new Color(0.8f, 0.8f, 1f, 1f),
            FogAttenuationDistance = 20000f,
            CloudAmbientDimmer = 1f
        };

        public bool IsValid(float minimumFogDistance)
        {
            return InRange(AerosolDensity, 0f, 1f) && InRange(AerosolAnisotropy, -1f, 1f)
                && InRange(ColorSaturation, 0f, 1f) && InRange(CloudAmbientDimmer, 0f, 1f)
                && ValidColor(AerosolTint) && ValidColor(HorizonTint) && ValidColor(FogTint)
                && InRange(HorizonTintWhiteAngle, 0.01f, 90f)
                && InRange(FogAttenuationDistance, minimumFogDistance, float.MaxValue);
        }

        private static bool ValidColor(Color color)
        {
            return InRange(color.r, 0f, 1f) && InRange(color.g, 0f, 1f)
                && InRange(color.b, 0f, 1f) && InRange(color.a, 0f, 1f);
        }

        private static bool InRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
