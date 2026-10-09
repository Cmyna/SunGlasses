using System.Collections.Generic;
using UnityEngine.Rendering;

namespace SunGlasses.Debugging
{
    internal static partial class SunGlassesDebugTab
    {
        static partial void AddSamplingSection(List<DebugUI.Widget> widgets)
        {
            AddSection(widgets, "Exposure calibration", ExposureSamplingDebugTab.Build());
        }
    }
}
