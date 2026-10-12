// CopyRight (c) CMyna. All Rights Preserved.
// file "Setting.cs".
// Licensed under MIT License.

using Colossal;
using Colossal.IO.AssetDatabase;
using SunGlasses.Systems;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace SunGlasses
{

    

    [FileLocation(nameof(SunGlasses))]
    [SettingsUIGroupOrder(kgSun, kgSky, kgExposure, kgMisc)]
    [SettingsUIShowGroupName(kgSun, kgSky, kgExposure, kgMisc)]
    public class Setting : ModSetting
    {

        /**
         * NOTE FOR APPS HUNGARIAN PREFIX: 
         * + kg: key for an UI group
         * + ks: key for an UI section
         */

        public const string ksMain = "Main";
        public const string kgMain = "MainGroup";
        public const string kgSun = "SunSettings";
        public const string kgSky = "SkySettings";
        public const string kgExposure = "ExposureSettings";
        public const string kgMisc = "MiscSettings";

        public const float SunSizeMin = 0.05f;
        public const float SunSizeMax = 10f;
        public const float SunBloomMin = 0f;
        public const float SunBloomMax = 5f;
        public const float SkyExposureMin = 0f;
        public const float SkyExposureMax = 5f;

        public const float DefaultMultiplier = 1f;

        private readonly DynamicExposureSystem _dynamicExposureSystem;
        private readonly AtmosphereSystem _atmosphereSystem;

        public Setting(IMod mod, DynamicExposureSystem dynamicExposureSystem, AtmosphereSystem atmosphereSystem) : base(mod)
        {
            _dynamicExposureSystem = dynamicExposureSystem;
            _atmosphereSystem = atmosphereSystem;
            SetDefaults();
        }

        
        [SettingsUISlider(min = SunSizeMin, max = SunSizeMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSun)]
        public float SunSize { get; set; }



        [SettingsUISlider(min = 0, max = 130000, step = 100f, scaleDragVolume = true, scalarMultiplier = 10, unit = Unit.kInteger)]
        [SettingsUISection(ksMain, kgSun)]
        [SettingsUIHidden]
        public float SunLightIntensity { get; set; }


        [SettingsUISection(ksMain, kgSun)]
        public bool LensFlare { get; set; }


        [SettingsUISlider(min = SunBloomMin, max = SunBloomMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSun)]
        public float SunBloom { get; set; }


        [SettingsUISlider(min = SkyExposureMin, max = SkyExposureMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSky)]
        public float SkyExposure { get; set; }


        [SettingsUISlider(min = 1, max = 4, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSky)]
        public float IndirectDiffuseSunLighting { get; set; }

        [SettingsUISlider(min = 0, max = 1, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSky)]
        public float GroundFillLight { get; set; }


        [SettingsUISection(ksMain, kgExposure)]
        public ExposureCurvePreset ExposureCurve { get; set; }

        [SettingsUISection(ksMain, kgExposure)]
        public bool ColorCorrection { get; set; }

        public enum ExposureCurvePreset
        {
            None, Realistic, Bright
        }


        [SettingsUIButton]
        [SettingsUISection(ksMain, kgMisc)]
        [SettingsUIConfirmation]
        public bool ResetModSettings
        {
            set
            {
                SetDefaults(); // Apply defaults.
                EnsureUpdate();
                Apply();
            }
        }


        /// <summary>
        /// set hidden setting value not default to force game update settings to storage(when others under default)
        /// </summary>
        private void EnsureUpdate()
        {
            Contra = false;
        }

        /// <summary>
        /// snippets from https://github.com/algernon-A/PlopTheGrowables/tree/master/Code <br/>
        /// <copyright file="ModSettings.cs" company="algernon (K. Algernon A. Sheppard)">
        /// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
        /// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
        /// See .\OPEN_LICENSES\PlopTheGrowables_LICENSE file in the project.
        /// </copyright><br/>
        /// Gets or sets a value indicating whether, well, nothing really.
        /// to ensure the the JSON contains at least one non-default value.
        /// This is to workaround a bug where the settings file isn't overwritten when there are no non-default settings.
        /// </summary>
        [SettingsUIHidden]
        public bool Contra { get; set; } = true;

        public override void SetDefaults()
        {
            SkyExposure = RemapLightingSystem.VanillaSkyExposure;
            SunSize = DefaultMultiplier;
            SunBloom = DefaultMultiplier;
            ExposureCurve = ExposureCurvePreset.Realistic;
            ColorCorrection = true;
            IndirectDiffuseSunLighting = RemapLightingSystem.VanillaIndirectDiffuseLighting;
            GroundFillLight = 0f;
            LensFlare = true;
            RemapLightingSystem.InternalVolumePriority = 2500;
        }

        public override void Apply()
        {
            _dynamicExposureSystem.ApplyPreset(ExposureCurve);
            _atmosphereSystem.OverridesEnabled = ColorCorrection;
            RemapLightingSystem.SunTintPreset = ColorCorrection
                ? RemapLightingSystem.GentleWarmthSunTintPreset
                : RemapLightingSystem.OriginalSunTintPreset;
            RemapLightingSystem.EnableLensFlare = LensFlare;
            RemapLightingSystem.IndirectLightingMultipilier = IndirectDiffuseSunLighting;
            RemapLightingSystem.GroundTintWeight = GroundFillLight;
            RemapLightingSystem.InternalVolumePriority = 2500;
            RemapLightingSystem.SkyExposure = SkyExposure;
            RemapLightingSystem.SunBloomMultiplier = SunBloom;
            RemapLightingSystem.SunLightIntensity = SunLightIntensity;
            RemapLightingSystem.SunSizeMultiplier = SunSize;
            base.Apply();
        }



    }

    public class SimpleLocale : IDictionarySource
    {
        private readonly Setting m_Setting;
        public SimpleLocale(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Sun Glasses" },
                { m_Setting.GetOptionTabLocaleID(Setting.ksMain), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kgSun), "Sun" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kgSky), "Sky" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kgExposure), "Exposure" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kgMisc), "MISC" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SkyExposure) ), "Sky Exposure" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SunSize) ), "Sun Disk Size" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SunBloom) ), "Sun Bloom Strength" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.LensFlare)), "Lens Flare" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SunLightIntensity)), "Sun Light Intensity" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IndirectDiffuseSunLighting)), "Indirect Lighting" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GroundFillLight)), "Ground Fill Light" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GroundFillLight)), "Control daytime ground tint: 0 removes ground fill light, 1 retains the original. Nighttime ground lighting is preserved." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExposureCurve)), "Exposure Curve" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExposureCurve)), "Select automatic exposure bounds: None, Realistic or Bright." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ColorCorrection)), "Sunlight Color Correction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ColorCorrection)), "Correct the severe yellow color cast in vanilla lighting." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetModSettings)), "Reset To Default" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ResetModSettings)), "Are you sure setting everything to default?" },

                { m_Setting.GetEnumValueLocaleID(Setting.ExposureCurvePreset.None), "None" },
                { m_Setting.GetEnumValueLocaleID(Setting.ExposureCurvePreset.Realistic), "Realistic" },
                { m_Setting.GetEnumValueLocaleID(Setting.ExposureCurvePreset.Bright), "Bright" },
            };
        }

        public void Unload()
        {

        }
    }
}
