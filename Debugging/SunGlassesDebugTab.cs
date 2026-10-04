using System;
using System.Collections.Generic;
using Game.Debug;
using UnityEngine.Rendering;

namespace SunGlasses.Debugging
{
    [DebugContainer]
    internal static class SunGlassesDebugTab
    {
        [DebugTab("SunGlasses", 100)]
        internal static List<DebugUI.Widget> Build()
        {
            var widgets = new List<DebugUI.Widget>();
            AddSection(widgets, "Exposure", ExposureCalibrationDebugTab.Build());
            AddSection(widgets, "Sun", SunLightingDebugTab.Build());
            AddSection(widgets, "Atmosphere", AtmosphereDebugTab.Build());
            return widgets;
        }

        private static void AddSection(List<DebugUI.Widget> widgets, string name, List<DebugUI.Widget> children)
        {
            if (children == null) return;
            var section = new DebugUI.Foldout { displayName = name, opened = true };
            foreach (var child in children) section.children.Add(child);
            widgets.Add(section);
        }

        internal static void Rebuild()
        {
            try { DebugSystem.Rebuild((Func<List<DebugUI.Widget>>)Build); }
            catch (Exception ex) { Mod.Log.Warn("Could not refresh SunGlasses debug tab: " + ex.Message); }
        }
    }
}
