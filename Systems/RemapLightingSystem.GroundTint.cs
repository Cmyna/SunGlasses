using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    public partial class RemapLightingSystem
    {
        private const string GroundTintVolumeName = "Render Settings";
        private static float groundTintWeight = 0f;
        private readonly GroundTintOverride groundTint = new GroundTintOverride();

        // Session-only debug control. No options toggle disables this correction.
        internal static float GroundTintWeight
        {
            get => groundTintWeight;
            set
            {
                if (!float.IsNaN(value) && !float.IsInfinity(value))
                    groundTintWeight = Mathf.Clamp01(value);
            }
        }

        internal string GroundTintStatus => groundTint.IsBound
            ? $"Original {groundTint.Original}; applied {groundTint.Applied}"
            : "Waiting for Render Settings";

        private void UpdateGroundTint()
        {
            var go = GameObject.Find(GroundTintVolumeName);
            var volume = go != null ? go.GetComponent<Volume>() : null;
            // The game uses an instantiated profile; editing sharedProfile has no live effect.
            if (volume != null && volume.profile.TryGet<PhysicallyBasedSky>(out var sky))
                groundTint.Apply(sky.groundTint, GroundTintWeight);
            else
                groundTint.Release();
        }

        internal void ReleaseGroundTint() => groundTint.Release();

        protected override void OnStopRunning()
        {
            ReleaseGroundTint();
            base.OnStopRunning();
        }
    }
}
