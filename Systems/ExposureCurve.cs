using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace SunGlasses.Systems
{
    public sealed class ExposureCurve
    {
        [DataContract]
        public sealed class Point
        {
            [DataMember] public float timeOfDayHours;
            [DataMember] public float solarElevationDegrees;
            [DataMember] public float centerEv100;
            [DataMember] public float widthEv100;
        }
        [DataContract]
        private sealed class FileData
        {
            [DataMember] public int version = 1;
            [DataMember] public Point[] samples = Array.Empty<Point>();
        }
        private readonly Point[] _points;
        public int Count => _points.Length;
        public ExposureCurve(Point[] points)
        {
            if (points == null || points.Length == 0) throw new InvalidDataException("Exposure curve has no samples");
            _points = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (p == null || !Finite(p.timeOfDayHours) || p.timeOfDayHours < 0 || p.timeOfDayHours >= 24 ||
                    !Finite(p.solarElevationDegrees) || p.solarElevationDegrees < -90 || p.solarElevationDegrees > 90 ||
                    !Finite(p.centerEv100) || !Finite(p.widthEv100) || p.widthEv100 < 0 ||
                    p.centerEv100 - p.widthEv100 / 2 < -10 || p.centerEv100 + p.widthEv100 / 2 > 20)
                    throw new InvalidDataException("Invalid exposure curve point");
                _points[i] = new Point { timeOfDayHours = p.timeOfDayHours, solarElevationDegrees = p.solarElevationDegrees,
                    centerEv100 = p.centerEv100, widthEv100 = p.widthEv100 };
            }
        }
        public static ExposureCurve LoadBundled(string name)
        {
            if (name != "bright" && name != "realistic") throw new ArgumentException("Unknown bundled curve");
            using (var stream = typeof(ExposureCurve).Assembly.GetManifestResourceStream("SunGlasses.ExposureCurves." + name + ".json"))
            {
                if (stream == null) throw new FileNotFoundException("Bundled curve missing");
                var data = (FileData)new DataContractJsonSerializer(typeof(FileData)).ReadObject(stream);
                if (data == null || data.version != 1) throw new InvalidDataException("Unsupported exposure curve");
                return new ExposureCurve(data.samples);
            }
        }
        public bool Evaluate(float elevation, float time, out float minimum, out float maximum, out string description)
        {
            minimum = maximum = 0; description = "Waiting for sun";
            if (!Finite(elevation) || !Finite(time)) return false;
            float angle = SolarArcAngle(elevation, time);
            Point before = null, after = null, first = null, last = null;
            float beforeAngle = float.NegativeInfinity, afterAngle = float.PositiveInfinity;
            float firstAngle = float.PositiveInfinity, lastAngle = float.NegativeInfinity;
            int beforeIndex = 0, afterIndex = 0, firstIndex = 0, lastIndex = 0;
            for (int i = 0; i < _points.Length; i++)
            {
                var p = _points[i];
                float recorded = SolarArcAngle(p.solarElevationDegrees, p.timeOfDayHours);
                for (int side = 0; side < 2; side++)
                {
                    float node = side == 0 ? recorded : MirrorAngle(recorded);
                    if (node < firstAngle) { firstAngle = node; first = p; firstIndex = i; }
                    if (node > lastAngle) { lastAngle = node; last = p; lastIndex = i; }
                    if (node <= angle && node >= beforeAngle) { beforeAngle = node; before = p; beforeIndex = i; }
                    if (node >= angle && node <= afterAngle) { afterAngle = node; after = p; afterIndex = i; }
                }
            }
            if (before == null) { before = last; beforeAngle = lastAngle; beforeIndex = lastIndex; }
            if (after == null) { after = first; afterAngle = firstAngle; afterIndex = firstIndex; }
            float span = Mathf.Repeat(afterAngle - beforeAngle, 360f);
            float fraction = span > 0 ? Mathf.Repeat(angle - beforeAngle, 360f) / span : 0;
            minimum = Mathf.Lerp(before.centerEv100 - before.widthEv100 / 2, after.centerEv100 - after.widthEv100 / 2, fraction);
            maximum = Mathf.Lerp(before.centerEv100 + before.widthEv100 / 2, after.centerEv100 + after.widthEv100 / 2, fraction);
            description = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Sample {0} ({1:F3}°{2}) -> Sample {3} ({4:F3}°{5})  t={6:F3}",
                beforeIndex + 1, beforeAngle, Mirrored(before, beforeAngle), afterIndex + 1, afterAngle, Mirrored(after, afterAngle), fraction);
            return true;
        }
        private static string Mirrored(Point p, float angle) => Mathf.Approximately(angle, SolarArcAngle(p.solarElevationDegrees, p.timeOfDayHours)) ? "" : ", mirrored";
        public static float SolarArcAngle(float elevation, float time) => Mathf.Repeat(time, 24f) < 12f ? elevation : MirrorAngle(elevation);
        private static float MirrorAngle(float angle) => angle >= 0 ? 180f - angle : -180f - angle;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
