using UnityEngine;

namespace SunGlasses.Systems
{
    public partial class RemapLightingSystem
    {
        internal const int OriginalSunTintPreset = 0;
        internal const int GentleWarmthSunTintPreset = 4;
        private static int? _temporarySunTintPreset;
        private static bool? _observedColorCorrection;
        internal static int BaseSunTintPreset => Mod.Setting != null && Mod.Setting.ColorCorrection
            ? GentleWarmthSunTintPreset : OriginalSunTintPreset;
        internal static bool SunTintOverrideActive
        {
            get { ObserveColorCorrection(); return _temporarySunTintPreset.HasValue; }
        }
        internal static int SunTintPreset
        {
            get { ObserveColorCorrection(); return _temporarySunTintPreset ?? BaseSunTintPreset; }
            set
            {
                ObserveColorCorrection();
                if (value >= 0 && value < SunTintColors.Length) _temporarySunTintPreset = value;
            }
        }
        private static void ObserveColorCorrection()
        {
            var enabled = Mod.Setting?.ColorCorrection;
            if (_observedColorCorrection != enabled)
            {
                _temporarySunTintPreset = null;
                _observedColorCorrection = enabled;
            }
        }
        internal static void ClearSunTintOverride()
        {
            _temporarySunTintPreset = null;
            _observedColorCorrection = Mod.Setting?.ColorCorrection;
        }
        internal static bool SunTintBlendByElevation = true;
        internal static float SunTintWhiteAngle = 10f;
        internal static float SunTintElevation;
        internal static float SunTintWhiteWeight;
        internal static Color ActiveSunTint = Color.white;
        internal static readonly Color[] SunTintColors =
        {
            Color.white,
            new Color(0.78f, 0.88f, 1f, 1f),
            new Color(0.70f, 0.83f, 1f, 1f),
            new Color(0.65f, 0.80f, 1f, 1f),
            new Color(0.76f, 0.85f, 1f, 1f),
            new Color(1f, 0.86f, 0.72f, 1f)
        };
        internal static readonly string[] SunTintNames =
        {
            "Original / no tint", "Mild yellow correction", "Neutral correction",
            "Cool correction", "Gentle warmth", "Golden red"
        };

        private Light tintSunLight;
        private Color originalSunTint;
        private Color appliedSunTint;

        private void UpdateSunTint()
        {
            var sun = planetarySystem.SunLight;
            if (!sun.isValid || sun.light == null)
            {
                ReleaseSunTint();
                return;
            }

            if (tintSunLight != sun.light)
            {
                ReleaseSunTint();
                tintSunLight = sun.light;
                originalSunTint = tintSunLight.color;
            }

            // Directional light forward points toward the ground; negate Y to
            // measure the sun's elevation above the horizon, independent of Euler angles.
            SunTintElevation = Mathf.Asin(Mathf.Clamp(-tintSunLight.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            SunTintWhiteWeight = SunTintBlendByElevation
                ? Mathf.Clamp01(SunTintElevation / SunTintWhiteAngle) : 0f;
            appliedSunTint = Color.Lerp(SunTintColors[SunTintPreset], Color.white, SunTintWhiteWeight);
            ActiveSunTint = appliedSunTint;
            tintSunLight.color = appliedSunTint;
        }

        internal void ReleaseSunTint()
        {
            // Leave a newer external change alone when relinquishing the light.
            if (tintSunLight != null && tintSunLight.color.Equals(appliedSunTint))
                tintSunLight.color = originalSunTint;
            tintSunLight = null;
        }
    }
}
