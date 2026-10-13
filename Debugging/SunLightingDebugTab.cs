using System.Collections.Generic;
using System.Globalization;
using SunGlasses.Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunGlasses.Debugging
{
    internal static class SunLightingDebugTab
    {
        internal static List<DebugUI.Widget> Build()
        {
            var names = new GUIContent[RemapLightingSystem.SunTintNames.Length];
            var values = new int[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = new GUIContent(RemapLightingSystem.SunTintNames[i]);
                values[i] = i;
            }
            return new List<DebugUI.Widget>
            {
                new DebugUI.EnumField
                {
                    displayName = "Sun tint preset",
                    enumNames = names,
                    enumValues = values,
                    getter = () => RemapLightingSystem.SunTintPreset,
                    setter = SetTintPreset,
                    getIndex = () => RemapLightingSystem.SunTintPreset,
                    setIndex = SetTintPreset
                },
                new DebugUI.Value
                {
                    displayName = "Selected sun tint",
                    getter = () => RemapLightingSystem.SunTintNames[RemapLightingSystem.SunTintPreset]
                        + " " + RemapLightingSystem.SunTintColors[RemapLightingSystem.SunTintPreset]
                },
                new DebugUI.BoolField
                {
                    displayName = "Blend sun tint by elevation",
                    getter = () => RemapLightingSystem.SunTintBlendByElevation,
                    setter = value => RemapLightingSystem.SunTintBlendByElevation = value
                },
                new DebugUI.TextField
                {
                    displayName = "White tint threshold (degrees, 0.01-90)",
                    getter = () => RemapLightingSystem.SunTintWhiteAngle.ToString("G", CultureInfo.InvariantCulture),
                    setter = value =>
                    {
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                            && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= 0.01f && parsed <= 90f)
                            RemapLightingSystem.SunTintWhiteAngle = parsed;
                    }
                },
                new DebugUI.Value
                {
                    displayName = "Elevation / white weight / active tint",
                    getter = () => string.Format(CultureInfo.InvariantCulture,
                        "{0:F2} deg / {1:P0} white / RGB ({2:F3}, {3:F3}, {4:F3})",
                        RemapLightingSystem.SunTintElevation, RemapLightingSystem.SunTintWhiteWeight,
                        RemapLightingSystem.ActiveSunTint.r, RemapLightingSystem.ActiveSunTint.g,
                        RemapLightingSystem.ActiveSunTint.b)
                },
                new DebugUI.BoolField
                {
                    displayName = "Enable independent specular sun",
                    getter = () => RemapLightingSystem.EnableSunSpecularLight,
                    setter = value => RemapLightingSystem.EnableSunSpecularLight = value
                },
                new DebugUI.FloatField
                {
                    displayName = "Specular sun lightDimmer",
                    getter = () => RemapLightingSystem.SunSpecularLightDimmer,
                    setter = value =>
                    {
                        if (!float.IsNaN(value) && !float.IsInfinity(value))
                            RemapLightingSystem.SunSpecularLightDimmer = Mathf.Clamp01(value);
                    },
                    min = () => 0f,
                    max = () => 1f,
                    incStep = 0.01f
                },
                new DebugUI.Button
                {
                    displayName = "Reset specular sun lightDimmer",
                    action = () => RemapLightingSystem.SunSpecularLightDimmer = RemapLightingSystem.DefaultSunSpecularLightDimmer
                },
                new DebugUI.FloatField
                {
                    displayName = "Specular sun lightDimmer at 90 degrees",
                    getter = () => RemapLightingSystem.SunSpecularZenithDimmer,
                    setter = value =>
                    {
                        if (!float.IsNaN(value) && !float.IsInfinity(value))
                            RemapLightingSystem.SunSpecularZenithDimmer = Mathf.Clamp01(value);
                    },
                    min = () => 0f,
                    max = () => 1f,
                    incStep = 0.01f
                },
                new DebugUI.Button
                {
                    displayName = "Reset specular sun lightDimmer at 90 degrees",
                    action = () => RemapLightingSystem.SunSpecularZenithDimmer = RemapLightingSystem.DefaultSunSpecularZenithDimmer
                },
                new DebugUI.Value
                {
                    displayName = "Active specular sun lightDimmer",
                    getter = () => RemapLightingSystem.ActiveSunSpecularLightDimmer
                }
            };
        }

        private static void SetTintPreset(int value)
        {
            if (value >= 0 && value < RemapLightingSystem.SunTintColors.Length)
                RemapLightingSystem.SunTintPreset = value;
        }
    }
}
