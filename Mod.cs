// CopyRight (c) CMyna. All Rights Preserved.
// file "Mod.cs".
// Licensed under MIT License.

using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using SunGlasses.Systems;
using SunGlasses.Debugging;
using Game;
using Game.Modding;
using Game.Rendering;
using Game.SceneFlow;
using Unity.Entities;

namespace SunGlasses
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger($"{nameof(SunGlasses)}").SetShowsErrorsInUI(true);
        private Setting m_Setting;

        private RemapLightingSystem _remapLightingSystem;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) Log.Info($"Current mod asset at {asset.path}");

            _remapLightingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<RemapLightingSystem>();
            _remapLightingSystem.Enabled = true;
            ExposureCalibrationController.Install();
            AtmosphereDebugController.Install();

            m_Setting = new Setting(this, _remapLightingSystem);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new SimpleLocale(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(SunGlasses), m_Setting, new Setting(this, _remapLightingSystem));
            // Loading persisted values does not push them into the rendering system.
            m_Setting.Apply();

            // update system
            // Copy this frame's sun after vanilla updates its intensity and color.
            updateSystem.UpdateAfter<RemapLightingSystem, LightingSystem>(SystemUpdatePhase.PreCulling);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            ExposureCalibrationController.Uninstall();
            AtmosphereDebugController.Uninstall();
            if (_remapLightingSystem != null)
            {
                _remapLightingSystem.Enabled = false;
                _remapLightingSystem.ReleaseSunTint();
                _remapLightingSystem.ReleaseSunIntensity();
                _remapLightingSystem.ReleaseSunSpecularLight();
            }
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
