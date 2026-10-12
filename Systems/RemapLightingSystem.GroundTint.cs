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
        private float groundTintElevation;
        private float groundTintAppliedWeight;

        // Options persist the daytime weight; Debug can temporarily override it.
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
            groundTintAppliedWeight = GroundTintOverride.WeightForElevation(groundTintElevation, GroundTintWeight);
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
            ReleaseGroundTint();
            base.OnStopRunning();
        }
    }
}
