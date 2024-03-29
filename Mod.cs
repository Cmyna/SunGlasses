// CopyRight (c) CMyna. All Rights Preserved.
// file "Mod.cs".
// Licensed under MIT License.

using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using SunGlasses.Systems;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace SunGlasses
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger($"{nameof(SunGlasses)}").SetShowsErrorsInUI(true);
        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) Log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new SimpleLocale(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(SunGlasses), m_Setting, new Setting(this));

            // update system
            updateSystem.UpdateBefore<RemapLightingSystem>(SystemUpdatePhase.Rendering);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
