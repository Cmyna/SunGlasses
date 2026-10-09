using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Rendering;

namespace SunGlasses.Debugging
{
    internal static class ExposureSamplingDebugTab
    {
        internal static List<DebugUI.Widget> Build()
        {
            var controller = ExposureDebugSystem.Instance?.SamplingController;
            if (controller == null) return null;

            var widgets = new List<DebugUI.Widget>
            {
                new DebugUI.BoolField
                {
                    displayName = "Enable custom curve",
                    getter = () => controller.CustomEnabled,
                    setter = value => controller.CustomEnabled = value
                },
                new DebugUI.BoolField
                {
                    displayName = "Manual calibration preview",
                    getter = () => controller.PreviewEnabled,
                    setter = value => controller.PreviewEnabled = value
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
                new DebugUI.Button { displayName = "Apply custom curve", action = controller.ApplyCustomCurve },
                new DebugUI.Button { displayName = "Reload custom samples", action = controller.ReloadSamples },
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
            SunGlassesDebugTab.Rebuild();
        }
    }
}
