using Game.Simulation;

namespace SunGlasses.Debugging
{
    public partial class ExposureDebugSystem
    {
        public ExposureSamplingController SamplingController { get; private set; }

        partial void InitializeSampling()
        {
            SamplingController = new ExposureSamplingController(_runtime, World.GetOrCreateSystemManaged<PlanetarySystem>());
            SamplingController.SamplesChanged += ExposureSamplingDebugTab.Rebuild;
        }

        partial void ShutdownSampling() { SamplingController?.Shutdown(); }

        partial void DisposeSampling()
        {
            if (SamplingController != null) SamplingController.SamplesChanged -= ExposureSamplingDebugTab.Rebuild;
        }

        partial void IncludeExposureOverride(ref bool exposureEnabled)
        {
            exposureEnabled |= _runtime.ManualOverrideEnabled;
        }
    }
}
