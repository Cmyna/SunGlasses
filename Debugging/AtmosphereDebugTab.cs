using System.Collections.Generic;
using System.Globalization;
using SunGlasses.Systems;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    // Debug controls and diagnostics; atmosphere rendering is independent of this panel.
    internal static class AtmosphereDebugTab
    {
        internal static List<DebugUI.Widget> Build()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var system = world != null && world.IsCreated ? world.GetExistingSystemManaged<AtmosphereSystem>() : null;
            if (system == null) return null;
            return new List<DebugUI.Widget>
            {
                new DebugUI.BoolField
                {
                    displayName = "Enable atmosphere overrides",
                    getter = () => system.OverridesEnabled,
                    setter = value => system.OverridesEnabled = value
                },
                new DebugUI.Value { displayName = "Atmosphere status", getter = () => system.Status },
                new DebugUI.Value { displayName = "Atmosphere Volume active", getter = () => system.VolumeActive },
                new DebugUI.Value { displayName = "Fog overrides suspended for custom fog", getter = () => system.FogOverridesSuspended },
                new DebugUI.BoolField
                {
                    displayName = "Blend horizon tint by elevation",
                    getter = () => system.Parameters.HorizonTintBlendByElevation,
                    setter = value =>
                    {
                        var p = system.Parameters;
                        p.HorizonTintBlendByElevation = value;
                        system.TrySetParameters(p);
                    }
                },
                new DebugUI.TextField
                {
                    displayName = "Horizon white tint threshold (degrees, 0.01-90)",
                    getter = () => system.Parameters.HorizonTintWhiteAngle.ToString("G", CultureInfo.InvariantCulture),
                    setter = value =>
                    {
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        {
                            var p = system.Parameters;
                            p.HorizonTintWhiteAngle = parsed;
                            system.TrySetParameters(p);
                        }
                    }
                },
                new DebugUI.Value
                {
                    displayName = "Horizon elevation / white weight / active tint",
                    getter = () => string.Format(CultureInfo.InvariantCulture,
                        "{0:F2} deg / {1:P0} white / RGB {2}",
                        system.HorizonTintElevation, system.HorizonTintWhiteWeight, Rgb(system.ActiveHorizonTint))
                },
                new DebugUI.Value { displayName = "Configured sky", getter = () => SkyText(system.Parameters) },
                new DebugUI.Value { displayName = "Configured fog / clouds", getter = () => FogText(system.Parameters) },
                new DebugUI.Value { displayName = "Effective camera sky", getter = CameraSkyText },
                new DebugUI.Value { displayName = "Effective camera fog / clouds", getter = CameraFogText },
                new DebugUI.Value { displayName = "Atmosphere camera check", getter = () => CameraCheck(system) }
            };
        }

        private static string SkyText(AtmosphereParameters p)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Density {0:G5}; anisotropy {1:G5}; aerosol {2}; saturation {3:G5}; horizon {4}",
                p.AerosolDensity, p.AerosolAnisotropy, Rgb(p.AerosolTint), p.ColorSaturation, Rgb(p.HorizonTint));
        }

        private static string FogText(AtmosphereParameters p)
        {
            return string.Format(CultureInfo.InvariantCulture, "Fog {0}; distance {1:G5} m; cloud A ambient {2:G5}",
                Rgb(p.FogTint), p.FogAttenuationDistance, p.CloudAmbientDimmer);
        }

        private static VolumeStack CameraStack()
        {
            var camera = Camera.main;
            return camera == null ? null : HDCamera.GetOrCreate(camera, 0).volumeStack;
        }

        private static string CameraSkyText()
        {
            var sky = CameraStack()?.GetComponent<PhysicallyBasedSky>();
            if (sky == null) return "Waiting for camera sky";
            return SkyText(new AtmosphereParameters
            {
                AerosolDensity = sky.aerosolDensity.value, AerosolAnisotropy = sky.aerosolAnisotropy.value,
                AerosolTint = sky.aerosolTint.value, ColorSaturation = sky.colorSaturation.value, HorizonTint = sky.horizonTint.value
            });
        }

        private static string CameraFogText()
        {
            var stack = CameraStack();
            var fog = stack?.GetComponent<Fog>();
            var clouds = stack?.GetComponent<CloudLayer>();
            if (fog == null || clouds == null) return "Waiting for camera fog / clouds";
            return FogText(new AtmosphereParameters
            {
                FogTint = fog.tint.value, FogAttenuationDistance = fog.meanFreePath.value,
                CloudAmbientDimmer = clouds.layerA.ambientProbeDimmer.value
            });
        }

        internal static string CameraCheck(AtmosphereSystem system)
        {
            if (!system.Enabled || !system.OverridesEnabled) return "Off: game / Photo Mode controls atmosphere";
            if (!system.VolumeActive) return "Waiting for lighting scene";
            var stack = CameraStack();
            var sky = stack?.GetComponent<PhysicallyBasedSky>();
            var fog = stack?.GetComponent<Fog>();
            var clouds = stack?.GetComponent<CloudLayer>();
            if (sky == null || fog == null || clouds == null) return "Waiting for camera stack";
            var p = system.Parameters;
            bool matches = Close(sky.aerosolDensity.value, p.AerosolDensity) && Close(sky.aerosolAnisotropy.value, p.AerosolAnisotropy)
                && Close(sky.colorSaturation.value, p.ColorSaturation) && Close(sky.aerosolTint.value, p.AerosolTint)
                && Close(sky.horizonTint.value, system.ActiveHorizonTint)
                && (system.FogOverridesSuspended || (Close(fog.tint.value, p.FogTint)
                    && Close(fog.meanFreePath.value, p.FogAttenuationDistance)))
                && Close(clouds.layerA.ambientProbeDimmer.value, p.CloudAmbientDimmer);
            return matches ? (system.FogOverridesSuspended
                ? "Applied to main camera; fog delegated to custom fog" : "Applied to main camera")
                : "Mismatch: check camera mask or competing Volume";
        }

        private static bool Close(float actual, float expected) => Mathf.Abs(actual - expected) <= Mathf.Max(0.00001f, Mathf.Abs(expected) * 0.00001f);
        private static bool Close(Color actual, Color expected) => Close(actual.r, expected.r) && Close(actual.g, expected.g) && Close(actual.b, expected.b);
        private static string Rgb(Color color) => string.Format(CultureInfo.InvariantCulture, "({0:G5}, {1:G5}, {2:G5})", color.r, color.g, color.b);
    }
}
