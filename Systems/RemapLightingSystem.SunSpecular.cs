using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    public partial class RemapLightingSystem
    {
        public const string SunSpecularLightName = "SunGlassesSunSpecularLight";

        // A session-only debug override; independent of the vanilla sun's dimmer.
        internal const float DefaultSunSpecularLightDimmer = 0.01f;
        internal static float SunSpecularLightDimmer = DefaultSunSpecularLightDimmer;

        private GameObject sunSpecularObject;
        private Light sunSpecularLight;
        private HDAdditionalLightData sunSpecularData;
        private HDAdditionalLightData originalSunData;
        private bool originalSunAffectSpecular;

        private void UpdateSunSpecularLight()
        {
            var sun = planetarySystem.SunLight;
            if (!sun.isValid || sun.transform == null)
            {
                // Do not leave the last scene's specular light shining during a load.
                if (sunSpecularLight != null) sunSpecularLight.enabled = false;
                return;
            }

            var sourceData = sun.additionalData;
            if (originalSunData != sourceData)
            {
                RestoreSunSpecular();
                originalSunData = sourceData;
                originalSunAffectSpecular = sourceData.affectSpecular;
            }

            if (sunSpecularObject == null)
            {
                // Only create lighting components, avoiding cloned lens flares or scripts.
                sunSpecularObject = new GameObject(SunSpecularLightName);
                Object.DontDestroyOnLoad(sunSpecularObject);
                sunSpecularLight = sunSpecularObject.AddComponent<Light>();
                sunSpecularLight.enabled = false;
                sunSpecularLight.type = LightType.Directional;
                sunSpecularData = sunSpecularObject.AddComponent<HDAdditionalLightData>();
            }

            var sourceLight = sun.light;
            sunSpecularObject.layer = sourceLight.gameObject.layer;
            var targetTransform = sunSpecularObject.transform;
            targetTransform.SetPositionAndRotation(sun.transform.position, sun.transform.rotation);
            targetTransform.localScale = sun.transform.lossyScale;

            CopySunLight(sourceLight, sunSpecularLight);
            // HDRP's own copy includes intensity, disk, cookies, layers and shadow settings.
            sourceData.CopyTo(sunSpecularData);
            // Ablate the finite angular size on the specular-only sun.
            // CopyTo restores the source value every frame, so override it here.
            sunSpecularData.angularDiameter = 0f;
            // HDRP supports only one cascaded directional shadow map. The vanilla
            // sun owns it; the specular-only duplicate must never request another.
            sunSpecularData.EnableShadows(false);
            sunSpecularData.affectDiffuse = false;
            sunSpecularData.affectSpecular = true;
            sunSpecularData.affectsVolumetric = false;
            sunSpecularData.interactsWithSky = false;
            sunSpecularData.lightDimmer = SunSpecularLightDimmer;
            sunSpecularData.enabled = sourceData.enabled;
            sunSpecularLight.enabled = sourceLight.enabled && sourceLight.gameObject.activeInHierarchy;
            sourceData.affectSpecular = false;
        }

        private static void CopySunLight(Light source, Light target)
        {
            // Native Light state is not copied by HDAdditionalLightData.CopyTo.
            target.type = source.type;
            target.shape = source.shape;
            target.spotAngle = source.spotAngle;
            target.innerSpotAngle = source.innerSpotAngle;
            target.color = source.color;
            target.colorTemperature = source.colorTemperature;
            target.useColorTemperature = source.useColorTemperature;
            target.intensity = source.intensity;
            target.bounceIntensity = source.bounceIntensity;
            target.range = source.range;
            target.cookie = source.cookie;
            target.cookieSize = source.cookieSize;
            target.flare = source.flare;
            target.cullingMask = source.cullingMask;
            target.renderingLayerMask = source.renderingLayerMask;
            target.renderMode = source.renderMode;
            target.bakingOutput = source.bakingOutput;
            target.lightShadowCasterMode = source.lightShadowCasterMode;
            target.shadows = source.shadows;
            target.shadowStrength = source.shadowStrength;
            target.shadowResolution = source.shadowResolution;
            target.shadowCustomResolution = source.shadowCustomResolution;
            target.shadowBias = source.shadowBias;
            target.shadowNormalBias = source.shadowNormalBias;
            target.shadowNearPlane = source.shadowNearPlane;
            target.layerShadowCullDistances = source.layerShadowCullDistances;
            target.useBoundingSphereOverride = source.useBoundingSphereOverride;
            target.boundingSphereOverride = source.boundingSphereOverride;
            target.useViewFrustumForShadowCasterCull = source.useViewFrustumForShadowCasterCull;
            target.useShadowMatrixOverride = source.useShadowMatrixOverride;
            target.shadowMatrixOverride = source.shadowMatrixOverride;
        }

        private void RestoreSunSpecular()
        {
            if (originalSunData != null)
                originalSunData.affectSpecular = originalSunAffectSpecular;
            originalSunData = null;
        }

        internal void ReleaseSunSpecularLight()
        {
            RestoreSunSpecular();
            if (sunSpecularLight != null) sunSpecularLight.enabled = false;
            if (sunSpecularObject != null) Object.Destroy(sunSpecularObject);
            sunSpecularObject = null;
            sunSpecularLight = null;
            sunSpecularData = null;
        }

        protected override void OnDestroy()
        {
            ReleaseGroundTint();
            ReleaseSunTint();
            ReleaseSunSpecularLight();
            base.OnDestroy();
        }
    }
}
