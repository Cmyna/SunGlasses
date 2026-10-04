using System;

namespace SunGlasses.Debugging
{
    internal static class ScreenBrightnessStatistics
    {
        // The post-process buffer is linear RGB. SDR reference white is 1.
        internal static float RelativeLuminance(float red, float green, float blue)
        {
            if (!Finite(red) || !Finite(green) || !Finite(blue)) return float.NaN;
            return 0.2126f * Clamp(red) + 0.7152f * Clamp(green) + 0.0722f * Clamp(blue);
        }

        internal static bool TryCalculate(float[] luminances, int count, out float mean, out float median)
        {
            mean = median = 0f;
            var validCount = 0;
            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                var value = luminances[i];
                if (!Finite(value)) continue;
                luminances[validCount++] = value;
                sum += value;
            }
            if (validCount == 0) return false;
            Array.Sort(luminances, 0, validCount);
            mean = (float)(sum / validCount);
            var middle = validCount / 2;
            median = validCount % 2 == 0
                ? (luminances[middle - 1] + luminances[middle]) * 0.5f
                : luminances[middle];
            return true;
        }

        private static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
