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
        private DynamicExposureSystem _dynamicExposureSystem;
        private ExposureDebugSystem _exposureDebugSystem;
        private AtmosphereSystem _atmosphereSystem;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) Log.Info($"Current mod asset at {asset.path}");

            _remapLightingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<RemapLightingSystem>();
            _remapLightingSystem.Enabled = true;
            _dynamicExposureSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DynamicExposureSystem>();
            _exposureDebugSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ExposureDebugSystem>();
            _atmosphereSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AtmosphereSystem>();

            m_Setting = new Setting(this, _dynamicExposureSystem, _atmosphereSystem);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new SimpleLocale(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(SunGlasses), m_Setting, new Setting(this, _dynamicExposureSystem, _atmosphereSystem));
            // Loading persisted values does not push them into the rendering system.
            m_Setting.Apply();

            // update system
            // Copy this frame's sun after vanilla updates its intensity and color.
            updateSystem.UpdateAfter<RemapLightingSystem, LightingSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAfter<DynamicExposureSystem, RemapLightingSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAfter<AtmosphereSystem, DynamicExposureSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAt<ExposureDebugSystem>(SystemUpdatePhase.MainLoop);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            if (_exposureDebugSystem != null) { _exposureDebugSystem.Enabled = false; _exposureDebugSystem.Shutdown(); }
            if (_dynamicExposureSystem != null) { _dynamicExposureSystem.Enabled = false; _dynamicExposureSystem.Shutdown(); }
            if (_atmosphereSystem != null) { _atmosphereSystem.Enabled = false; _atmosphereSystem.Shutdown(); }
            if (_remapLightingSystem != null)
            {
                _remapLightingSystem.Enabled = false;
                _remapLightingSystem.ReleaseSunTint();
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
