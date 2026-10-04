using Game;
using Colossal.Serialization.Entities;
using Game.SceneFlow;
using Game.Simulation;
using SunGlasses.Systems;

namespace SunGlasses.Debugging
{
    public partial class ExposureDebugSystem : GameSystemBase
    {
        public static ExposureDebugSystem Instance { get; private set; }
        public ExposureCalibrationController Controller { get; private set; }
        private PlanetarySystem _planetary;
        private bool _timeOwned, _previousOverride;
        private float _previousTime, _lastTime;
        public float CurrentTime => _planetary == null ? 0 : _planetary.time;
        protected override void OnCreate()
        {
            base.OnCreate();
            _planetary = World.GetOrCreateSystemManaged<PlanetarySystem>();
            Controller = new ExposureCalibrationController(World.GetOrCreateSystemManaged<DynamicExposureSystem>(), this);
            Controller.SamplesChanged += ExposureCalibrationDebugTab.Rebuild;
            Instance = this;
        }
        protected override void OnUpdate() { }
        public void SetTime(float hours)
        {
            if (float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0 || hours >= 24)
                throw new System.ArgumentOutOfRangeException(nameof(hours));
            if (!_timeOwned) { _previousOverride = _planetary.overrideTime; _previousTime = _planetary.time; _timeOwned = true; }
            _planetary.overrideTime = true; _planetary.time = hours; _lastTime = hours;
        }
        public void ReleaseTime()
        {
            if (!_timeOwned) return;
            // Do not overwrite a time override subsequently changed by another caller.
            if (_planetary.overrideTime && UnityEngine.Mathf.Approximately(_planetary.time, _lastTime))
            { _planetary.time = _previousTime; _planetary.overrideTime = _previousOverride; }
            _timeOwned = false;
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { ReleaseTime(); Controller?.ResetPreview(); }
        public void Shutdown() { ReleaseTime(); Controller?.ResetPreview(); }
        protected override void OnStopRunning() { Shutdown(); base.OnStopRunning(); }
        protected override void OnDestroy()
        {
            Shutdown();
            if (Controller != null) Controller.SamplesChanged -= ExposureCalibrationDebugTab.Rebuild;
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
