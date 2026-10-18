using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Systems
{
    public partial class RemapLightingSystem
    {
        private const string GroundTintVolumeName = "Render Settings";
        private readonly GroundTintOverride groundTint = new GroundTintOverride();
        private float groundTintElevation;
        private float groundTintAppliedWeight;

        internal string GroundTintStatus => groundTint.IsBound
            ? $"Elevation {groundTintElevation:F2} deg; original weight {groundTintAppliedWeight:P0}; original {groundTint.Original}; applied {groundTint.Applied}"
            : "Waiting for Render Settings";

        private void UpdateGroundTint()
        {
            var sun = planetarySystem.SunLight;
            if (!sun.isValid || sun.light == null)
            {
                groundTint.Release();
                return;
            }
            groundTintElevation = Mathf.Asin(Mathf.Clamp(-sun.light.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            var weight = Mod.Setting.GroundFillLight;
            if (float.IsNaN(weight) || float.IsInfinity(weight)) weight = 1f;
            groundTintAppliedWeight = GroundTintOverride.WeightForElevation(groundTintElevation, Mathf.Clamp01(weight));
            var go = GameObject.Find(GroundTintVolumeName);
            var volume = go != null ? go.GetComponent<Volume>() : null;
            // The game uses an instantiated profile; editing sharedProfile has no live effect.
            if (volume != null && volume.profile.TryGet<PhysicallyBasedSky>(out var sky))
                groundTint.Apply(sky.groundTint, groundTintAppliedWeight);
            else
                groundTint.Release();
        }

        internal void ReleaseGroundTint() => groundTint.Release();

        protected override void OnStopRunning()
        {
            Shutdown();
            base.OnStopRunning();
        }
    }
}
