using UnityEngine;
using UnityEngine.Rendering;

namespace SunGlasses.Systems
{
    // Keep the source color separate from our output so repeated frames never compound it.
    internal sealed class GroundTintOverride
    {
        private ColorParameter parameter;
        private Color original;
        private bool originalOverride;
        private Color applied;

        internal Color Original => original;
        internal Color Applied => applied;
        internal bool IsBound => parameter != null;

        internal static float WeightForElevation(float elevation, float daytimeWeight)
        {
            // Below the horizon retain the original ground lighting; fade it out by 5 degrees.
            return Mathf.Lerp(1f, Mathf.Clamp01(daytimeWeight), Mathf.Clamp01(elevation / 5f));
        }

        internal void Apply(ColorParameter target, float weight)
        {
            if (!ReferenceEquals(parameter, target))
            {
                Release();
                parameter = target;
                if (parameter == null) return;
                CaptureOriginal();
            }
            else if (parameter == null) return;
            else if (!parameter.value.Equals(applied) || !parameter.overrideState)
            {
                // Retain a new source color written by the game or another caller.
                original = parameter.value;
                // A color edit alone must not capture the override flag we enabled ourselves.
                if (!parameter.overrideState) originalOverride = false;
            }

            applied = Color.Lerp(new Color(0f, 0f, 0f, original.a), original, weight);
            parameter.Override(applied);
        }

        private void CaptureOriginal()
        {
            original = parameter.value;
            originalOverride = parameter.overrideState;
        }

        internal void Release()
        {
            if (parameter != null && parameter.overrideState && parameter.value.Equals(applied))
            {
                parameter.value = original;
                parameter.overrideState = originalOverride;
            }
            parameter = null;
        }
    }
}
