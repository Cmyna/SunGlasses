using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Debug;
using UnityEngine.Rendering;

namespace SunGlasses.Debugging
{
    [DebugContainer]
    internal static class ExposureCalibrationDebugTab
    {
        [DebugTab("SunGlasses Exposure", 100)]
        private static List<DebugUI.Widget> Build()
        {
            var controller = ExposureCalibrationController.Instance;
            if (controller == null) return null;

            var widgets = new List<DebugUI.Widget>
            {
                new DebugUI.Value { displayName = "Active exposure curve", getter = () => controller.ActiveCurve },
                new DebugUI.Button { displayName = "Use bright curve", action = () => controller.SwitchCurve("bright") },
                new DebugUI.Button { displayName = "Use realistic curve", action = () => controller.SwitchCurve("realistic") },
                new DebugUI.Button { displayName = "Use custom samples", action = () => controller.SwitchCurve("Custom") },
                new DebugUI.BoolField
                {
                    displayName = "Preview exposure bounds",
                    getter = () => controller.PreviewEnabled,
                    setter = value => controller.PreviewEnabled = value
                },
                new DebugUI.BoolField
                {
                    displayName = "Interpolate samples by solar angle",
                    getter = () => controller.InterpolationEnabled,
                    setter = value => controller.InterpolationEnabled = value
                },
                new DebugUI.TextField
                {
                    displayName = "Time (decimal hours, e.g. 5.85)",
                    getter = () => controller.TimeText,
                    setter = value => controller.TimeText = value
                },
                new DebugUI.Button { displayName = "Release time control", action = controller.ReleaseTime },
                new DebugUI.TextField
                {
                    displayName = "Center (EV100)",
                    getter = () => controller.CenterText,
                    setter = value => controller.CenterText = value
                },
                new DebugUI.TextField
                {
                    displayName = "Range width (EV100)",
                    getter = () => controller.WidthText,
                    setter = value => controller.WidthText = value
                },
                new DebugUI.Value { displayName = "Solar elevation", getter = controller.SolarElevation },
                new DebugUI.Value { displayName = "Solar arc angle", getter = controller.SolarArcAngleText },
                new DebugUI.Value { displayName = "Active interpolation samples", getter = controller.ActiveInterpolationSamples },
                new DebugUI.Value { displayName = "Interpolated bounds", getter = controller.InterpolatedRange },
                new DebugUI.Value { displayName = "Effective camera bounds", getter = controller.EffectiveRange },
                new DebugUI.Value { displayName = "Average relative luminance", getter = controller.AverageScreenBrightness },
                new DebugUI.Value { displayName = "Relative luminance P50", getter = controller.MedianScreenBrightness },
                new DebugUI.Value { displayName = "Preview check", getter = controller.PreviewCheck },
                new DebugUI.Button { displayName = "Record sample", action = controller.Record },
                new DebugUI.Value { displayName = "Parameter file", getter = () => controller.FilePath },
                new DebugUI.Value { displayName = "Status", getter = () => controller.Status }
            };

            var samples = controller.Samples;
            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                var number = i + 1;
                widgets.Add(new DebugUI.Value
                {
                    displayName = "Sample " + number.ToString(CultureInfo.InvariantCulture),
                    getter = () => string.Format(CultureInfo.InvariantCulture,
                        "{0:F3}°  {1:F3} ± {2:F3} EV  {3:F4}h  {4}",
                        sample.solarElevationDegrees, sample.centerEv100, sample.widthEv100 / 2f,
                        sample.timeOfDayHours, sample.recordedAtUtc)
                });
                widgets.Add(new DebugUI.Button
                {
                    displayName = "Remove sample " + number.ToString(CultureInfo.InvariantCulture),
                    action = () => controller.RemoveSample(sample)
                });
            }
            return widgets;
        }

        internal static void Rebuild()
        {
            try { DebugSystem.Rebuild((Func<List<DebugUI.Widget>>)Build); }
            catch (Exception ex) { Mod.Log.Warn("Could not refresh exposure sample list: " + ex.Message); }
        }
    }
}
