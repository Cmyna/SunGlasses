// Minimal host for the linked production sources. This does not emulate HDRP camera blending,
// native Unity lifetime timing, disk persistence, or the specular sun implementation.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        internal bool Destroyed;
        public static void Destroy(Object value)
        {
            if (ReferenceEquals(value, null)) return;
            value.Destroyed = true;
            if (value is GameObject go) foreach (var component in go.Components.Values) component.Destroyed = true;
        }
        public static bool operator ==(Object left, Object right) =>
            (ReferenceEquals(left, null) || left.Destroyed) && (ReferenceEquals(right, null) || right.Destroyed)
            || ReferenceEquals(left, right);
        public static bool operator !=(Object left, Object right) => !(left == right);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
    }
    public class Transform { public Vector3 forward = new Vector3 { y = -0.5f }; }
    public struct Vector3 { public float y; }
    public class GameObject : Object
    {
        private static readonly List<GameObject> Objects = new();
        internal readonly Dictionary<Type, Component> Components = new();
        public Transform transform = new();
        public int layer;
        public GameObject(string name) { this.name = name; Objects.Add(this); }
        public static GameObject Find(string name) => Objects.LastOrDefault(go => go != null && go.name == name);
        public T AddComponent<T>() where T : Component, new()
        {
            var value = new T { gameObject = this }; Components[typeof(T)] = value; return value;
        }
        public T GetComponent<T>() where T : Component => Components.TryGetValue(typeof(T), out var value) ? (T)value : null;
        public bool TryGetComponent<T>(out T value) where T : Component { value = GetComponent<T>(); return value != null; }
    }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject, new() => new T(); }
    public class Light : Component { public Color color = Color.white; }
    public class Camera : Component { public static Camera main; }
    public class GUIContent { public GUIContent(string text) { } }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new(1, 1, 1);
        public static Color Lerp(Color x, Color y, float t) => new(Mathf.Lerp(x.r, y.r, t), Mathf.Lerp(x.g, y.g, t), Mathf.Lerp(x.b, y.b, t), Mathf.Lerp(x.a, y.a, t));
    }
    public static class Mathf
    {
        public const float Rad2Deg = 180f / MathF.PI;
        public static float Clamp(float x, float min, float max) => Math.Clamp(x, min, max);
        public static float Clamp01(float x) => Clamp(x, 0, 1);
        public static float Lerp(float x, float y, float t) => x + (y - x) * Clamp01(t);
        public static float Asin(float x) => MathF.Asin(x);
        public static float Abs(float x) => MathF.Abs(x);
        public static float Max(float x, float y) => MathF.Max(x, y);
        public static float Repeat(float x, float length) => x - MathF.Floor(x / length) * length;
        public static bool Approximately(float x, float y) => MathF.Abs(x - y) < 0.00001f;
    }
}
namespace UnityEngine.Rendering
{
    public class Parameter<T> { public T value; public bool overrideState; public void Override(T x) { value = x; overrideState = true; } }
    public class FloatParameter : Parameter<float> { }
    public class ColorParameter : Parameter<UnityEngine.Color> { public ColorParameter() { value = UnityEngine.Color.white; } }
    public class IntParameter : Parameter<int> { public IntParameter(int value, bool state) { this.value = value; overrideState = state; } }
    public class VolumeComponent : UnityEngine.ScriptableObject { }
    public class VolumeProfile : UnityEngine.ScriptableObject
    {
        public readonly List<VolumeComponent> components = new();
        public T Add<T>(bool state) where T : VolumeComponent, new() { var result = new T(); components.Add(result); return result; }
        public bool TryGet<T>(out T result) where T : VolumeComponent { result = components.OfType<T>().FirstOrDefault(); return result != null; }
    }
    public class Volume : UnityEngine.Component
    {
        public bool enabled = true, isGlobal;
        public float priority, weight;
        public VolumeProfile sharedProfile;
        public VolumeProfile profile => sharedProfile;
    }
    public class LensFlareComponentSRP : UnityEngine.Component { public bool enabled = true; }
    public class VolumeStack { public T GetComponent<T>() where T : VolumeComponent, new() => new T(); }
    public static class DebugUI
    {
        public class Widget { public string displayName; }
        public class Value : Widget { public Func<object> getter; }
        public class Button : Widget { public Action action; }
        public class BoolField : Widget { public Func<bool> getter; public Action<bool> setter; }
        public class FloatField : Widget { public Func<float> getter, min, max; public Action<float> setter; public float incStep; }
        public class TextField : Widget { public Func<string> getter; public Action<string> setter; }
        public class EnumField : Widget
        {
            public UnityEngine.GUIContent[] enumNames;
            public int[] enumValues;
            public Func<int> getter, getIndex;
            public Action<int> setter, setIndex;
        }
    }
}
namespace UnityEngine.Rendering.HighDefinition
{
    public class HDAdditionalLightData : UnityEngine.Component { public float angularDiameter, flareSize; }
    public class PhysicallyBasedSky : VolumeComponent
    {
        public FloatParameter exposure = new(), aerosolDensity = new(), colorSaturation = new();
        public ColorParameter aerosolTint = new(), horizonTint = new(), groundTint = new();
    }
    public class Fog : VolumeComponent { public ColorParameter tint = new(); public FloatParameter meanFreePath = new(); }
    public class CloudLayer : VolumeComponent { public Layer layerA = new(); public class Layer { public FloatParameter ambientProbeDimmer = new(); } }
    public enum ExposureMode { Automatic, AutomaticHistogram }
    public class Exposure : VolumeComponent { public FloatParameter limitMin = new(), limitMax = new(); public Parameter<ExposureMode> mode = new(); }
    public class HDCamera { public VolumeStack volumeStack = new(); public static HDCamera GetOrCreate(UnityEngine.Camera camera, int index) => new(); }
    public class Bloom : VolumeComponent { public FloatParameter intensity = new(); }
    public class IndirectLightingController : VolumeComponent { public FloatParameter indirectDiffuseLightingMultiplier = new(); }
}
namespace Unity.Entities
{
    public class World
    {
        public static World DefaultGameObjectInjectionWorld = new();
        public bool IsCreated = true;
        private readonly Dictionary<Type, Game.GameSystemBase> _systems = new();
        public T GetOrCreateSystemManaged<T>() where T : Game.GameSystemBase, new()
        {
            if (_systems.TryGetValue(typeof(T), out var existing)) return (T)existing;
            var system = new T { World = this }; _systems[typeof(T)] = system; system.Create(); return system;
        }
        public T GetExistingSystemManaged<T>() where T : Game.GameSystemBase => _systems.TryGetValue(typeof(T), out var value) ? (T)value : null;
    }
}
namespace Colossal.Serialization.Entities { public enum Purpose { LoadGame } }
namespace Game.SceneFlow
{
    public enum GameMode { Game }
    public class GameManager
    {
        public static GameManager instance = new();
        public ModManager modManager = new();
        public LocalizationManager localizationManager = new();
    }
    public class ModManager { public bool TryGetExecutableAsset(object mod, out Asset asset) { asset = null; return false; } }
    public class Asset { public string path; }
    public class LocalizationManager { public void AddSource(string locale, object source) { } }
}
namespace Game
{
    public class GameSystemBase
    {
        public Unity.Entities.World World;
        private bool _enabled = true;
        public bool Enabled { get => _enabled; set { if (_enabled && !value) OnStopRunning(); _enabled = value; } }
        protected virtual void OnCreate() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnStopRunning() { }
        protected virtual void OnDestroy() { }
        protected virtual void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, SceneFlow.GameMode mode) { }
        protected virtual void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, SceneFlow.GameMode mode) { }
        public void Create() => OnCreate();
        public void Tick() { if (Enabled) OnUpdate(); }
        public void Preload() => OnGamePreload(default, default);
        public void Loaded() => OnGameLoadingComplete(default, default);
    }
    public enum SystemUpdatePhase { PreCulling, MainLoop }
    public class UpdateSystem
    {
        public void UpdateAfter<T, U>(SystemUpdatePhase phase) { }
        public void UpdateAt<T>(SystemUpdatePhase phase) { }
    }
}
namespace Game.Rendering
{
    public class LightingSystem { }
    public static class VolumeHelper
    {
        public static Volume CreateVolume(string name, int priority)
        {
            var result = new UnityEngine.GameObject(name).AddComponent<Volume>();
            result.sharedProfile = new VolumeProfile(); result.priority = priority; return result;
        }
        public static void GetOrCreateVolumeComponent<T>(Volume volume, ref T component) where T : VolumeComponent, new() => component = volume.sharedProfile.Add<T>(false);
    }
}
namespace Game.Simulation
{
    public class SunData
    {
        public bool isValid = true;
        public UnityEngine.Light light;
        public UnityEngine.Transform transform => light?.transform;
        public UnityEngine.Rendering.HighDefinition.HDAdditionalLightData additionalData;
    }
    public class PlanetarySystem : Game.GameSystemBase { public SunData SunLight = new(); public float time = 10; }
    public class ClimateSystem : Game.GameSystemBase { public Parameter<float> fog = new(); }
}
namespace Game.Modding
{
    public interface IMod { void OnLoad(Game.UpdateSystem system); void OnDispose(); }
    public abstract class ModSetting
    {
        public int ApplyCount, SaveCount;
        protected ModSetting(IMod mod) { }
        public abstract void SetDefaults();
        public virtual void Apply() => ApplyCount++;
        public void ApplyAndSave() { Apply(); SaveCount++; }
        public void RegisterInOptionsUI() { }
        public void UnregisterInOptionsUI() { }
        public string GetSettingsLocaleID() => "settings";
        public string GetOptionTabLocaleID(string x) => "tab" + x;
        public string GetOptionGroupLocaleID(string x) => "group" + x;
        public string GetOptionLabelLocaleID(string x) => "label" + x;
        public string GetOptionDescLocaleID(string x) => "desc" + x;
        public string GetOptionWarningLocaleID(string x) => "warning" + x;
        public string GetEnumValueLocaleID(object x) => "enum" + x;
    }
}
namespace Colossal
{
    public interface IDictionaryEntryError { }
    public interface IDictionarySource
    {
        IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts);
        void Unload();
    }
}
namespace Colossal.Logging
{
    public interface ILog { ILog SetShowsErrorsInUI(bool value); void Info(string text); void Error(string text); }
    public class Log : ILog
    {
        public int ErrorCount;
        public ILog SetShowsErrorsInUI(bool value) => this;
        public void Info(string text) { }
        public void Error(string text) { ErrorCount++; }
    }
    public static class LogManager { public static ILog GetLogger(string name) => new Log(); }
}
namespace Colossal.IO.AssetDatabase
{
    public class FileLocationAttribute : Attribute { public FileLocationAttribute(string name) { } }
    public class AssetDatabase
    {
        public static AssetDatabase global = new();
        public Action<SunGlasses.Setting> Load;
        public void LoadSettings(string name, SunGlasses.Setting setting, SunGlasses.Setting defaults) => Load?.Invoke(setting);
    }
}
namespace Game.Settings
{
    public class SettingsUIGroupOrderAttribute : Attribute { public SettingsUIGroupOrderAttribute(params string[] names) { } }
    public class SettingsUIShowGroupNameAttribute : Attribute { public SettingsUIShowGroupNameAttribute(params string[] names) { } }
    public class SettingsUISectionAttribute : Attribute { public SettingsUISectionAttribute(params string[] names) { } }
    public class SettingsUIHiddenAttribute : Attribute { }
    public class SettingsUIButtonAttribute : Attribute { }
    public class SettingsUIConfirmationAttribute : Attribute { }
    public class SettingsUISliderAttribute : Attribute { public float min, max, step, scalarMultiplier; public bool scaleDragVolume; public Game.UI.Unit unit; }
}
namespace Game.UI { public enum Unit { kFloatTwoFractions, kInteger } }
namespace SunGlasses.Systems
{
    // Specular lighting is unchanged by the settings refactor; native lighting is tested in-game.
    public partial class RemapLightingSystem
    {
        internal static bool EnableSunSpecularLight;
        internal const float DefaultSunSpecularLightDimmer = 0.01f, DefaultSunSpecularZenithDimmer = 0.35f;
        internal static float SunSpecularLightDimmer, SunSpecularZenithDimmer;
        internal static float ActiveSunSpecularLightDimmer = 0f;
        private void UpdateSunSpecularLight() { }
        internal void ReleaseSunSpecularLight() { }
    }
}
