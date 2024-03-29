// CopyRight (c) CMyna. All Rights Preserved.
// file "Setting.cs".
// Licensed under MIT License.

using Colossal;
using Colossal.IO.AssetDatabase;
using SunGlasses.Systems;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
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

        // it seems that if missing properties related to members in class, the game will not save those properties on disk
        private float _sunSize = DefaultMultiplier;
        private float _sunBloom = DefaultMultiplier;
        private float _sunLightIntensity = RemapLightingSystem.DefaultPbSunIntensity;
        private bool _lensFlare = true;
        private float _skyExposure = RemapLightingSystem.VanillaSkyExposure;
        private float _indirectDiffuseSunLighting = RemapLightingSystem.VanillaIndirectDiffuseLighting;
        private BrightenDarknessLevel _brightenLevel = BrightenDarknessLevel.None;



        public Setting(IMod mod) : base(mod)
        {
        }

        
        [SettingsUISlider(min = SunSizeMin, max = SunSizeMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSun)]
        public float SunSize
        {
            get => _sunSize;
            set { _sunSize = value; RemapLightingSystem.SunSizeMultiplier = _sunSize; }
        }


        
        [SettingsUISlider(min = 0, max = 130000, step = 100f, scaleDragVolume = true, scalarMultiplier = 10, unit = Unit.kInteger)]
        [SettingsUISection(ksMain, kgSun)]
        [SettingsUIHidden]
        public float SunLightIntensity
        {
            get => _sunLightIntensity;
            set { _sunLightIntensity = value; RemapLightingSystem.SunLightIntensity = _sunLightIntensity; }
        }

        [SettingsUISection(ksMain, kgSun)]
        public bool LensFlare
        {
            get => _lensFlare;
            set { _lensFlare = value; RemapLightingSystem.EnableLensFlare = _lensFlare; }
        }

        
        [SettingsUISlider(min = SunBloomMin, max = SunBloomMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSun)]
        public float SunBloom 
        {
            get => _sunBloom;
            set { _sunBloom = value; RemapLightingSystem.SunBloomMultiplier = _sunBloom; }
        }


        [SettingsUISlider(min = SkyExposureMin, max = SkyExposureMax, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSky)]
        public float SkyExposure
        {
            get => _skyExposure;
            set { _skyExposure = value; RemapLightingSystem.SkyExposure = _skyExposure; }
        }


        [SettingsUISlider(min = 1, max = 4, step = 0.05f, scaleDragVolume = true, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgSky)]
        public float IndirectDiffuseSunLighting
        {
            get => _indirectDiffuseSunLighting;
            set { _indirectDiffuseSunLighting = value; RemapLightingSystem.IndirectLightingMultipilier = _indirectDiffuseSunLighting; }
        }

        /*
        [SettingsUISlider(min = 0, max = 15, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        [SettingsUISection(ksMain, kgMain)]
        public float GroundDiffuseLight { get; set; }*/


        [SettingsUISection(ksMain, kgExposure)]
        //[SettingsUIHidden]
        public BrightenDarknessLevel BrightenLevel
        {
            get => _brightenLevel;
            set { _brightenLevel = value; RemapLightingSystem.BrightenDarknessLevel = (int)_brightenLevel; }
        }

        public DropdownItem<int>[] GetIntDropdownItems()
        {
            var items = new List<DropdownItem<int>>();

            for (var i = 0; i < 3; i += 1)
            {
                items.Add(new DropdownItem<int>()
                {
                    value = i,
                    displayName = i.ToString(),
                });
            }

            return items.ToArray();
        }

        public enum BrightenDarknessLevel
        {
            None, Level1, Level2, Level3
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
            BrightenLevel = BrightenDarknessLevel.None;
            IndirectDiffuseSunLighting = RemapLightingSystem.VanillaIndirectDiffuseLighting;
            LensFlare = true;
            RemapLightingSystem.InternalVolumePriority = 2500;
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

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.GroundDiffuseLight) ), "Ground Diffuse Light Strength" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.GroundDiffuseLight) ), "Ground Diffuse Light Strength" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BrightenLevel) ), "EnLight Darkness" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetModSettings)), "Reset To Default" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ResetModSettings)), "Are you sure setting everything to default?" },

                { m_Setting.GetEnumValueLocaleID(Setting.BrightenDarknessLevel.None), "None" },
                { m_Setting.GetEnumValueLocaleID(Setting.BrightenDarknessLevel.Level1), "Weak" },
                { m_Setting.GetEnumValueLocaleID(Setting.BrightenDarknessLevel.Level2), "Medium" },
                { m_Setting.GetEnumValueLocaleID(Setting.BrightenDarknessLevel.Level3), "Strong" },
            };
        }

        public void Unload()
        {

        }
    }
}
