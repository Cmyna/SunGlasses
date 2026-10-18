using System;

namespace SunGlasses.Systems
{
    public partial class DynamicExposureSystem
    {
        private float _manualMinimum, _manualMaximum;
        public bool ManualOverrideEnabled { get; private set; }

        public void SetManualOverride(float minimum, float maximum)
        {
            if (float.IsNaN(minimum) || float.IsInfinity(minimum) || float.IsNaN(maximum) || float.IsInfinity(maximum) ||
                minimum < -10 || maximum > 20 || minimum > maximum)
                throw new ArgumentOutOfRangeException(nameof(minimum));
            RefreshPreset();
            _manualMinimum = minimum;
            _manualMaximum = maximum;
            ManualOverrideEnabled = true;
        }

        public void ClearManualOverride() { ManualOverrideEnabled = false; }

        partial void ResetExposureOverride() { ClearManualOverride(); }
        partial void DescribeExposureOverride(ref string source)
        {
            if (ManualOverrideEnabled) source = "Temporary manual EV preview";
        }

        partial void ApplyExposureOverride(ref ExposureSnapshot snapshot, ref string description)
        {
            if (!ManualOverrideEnabled) return;
            snapshot.MinimumEv100 = _manualMinimum;
            snapshot.MaximumEv100 = _manualMaximum;
            snapshot.Ready = true;
            description = "Off (manual override)";
        }
    }
}
