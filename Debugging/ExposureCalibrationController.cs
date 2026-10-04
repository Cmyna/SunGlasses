using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using SunGlasses.Systems;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    // Calibration samples are deliberately separate from the mod's normal settings.
    public sealed class ExposureCalibrationController
    {
        private const float MinimumEv = -10f;
        private const float MaximumEv = 20f;
        private const string FileName = "exposure-calibration.json";

        [Serializable]
        [DataContract]
        public sealed class Sample
        {
            [DataMember]
            public string recordedAtUtc;
            [DataMember]
            public float timeOfDayHours;
            [DataMember]
            public float solarElevationDegrees;
            [DataMember]
            public float centerEv100;
            [DataMember]
            public float widthEv100;
        }

        [DataContract]
        private sealed class SampleFile
        {
            [DataMember]
            public int version = 1;
            [DataMember]
            public Sample[] samples = Array.Empty<Sample>();
        }


        private readonly List<Sample> _samples = new List<Sample>();
        private readonly DynamicExposureSystem _runtime;
        private readonly ExposureDebugSystem _debug;
        public event Action SamplesChanged;
        private bool _previewEnabled;
        private bool _samplesLoadFailed;
        public string ActiveCurve { get; private set; } = "Custom";
        private float _center = 11.5f;
        private float _width = 1f;
        private string _timeText = "12";
        private string _centerText = "11.5";
        private string _widthText = "1";
        private string _status = "Ready";
        public IReadOnlyList<Sample> Samples => _samples;
        public bool PreviewEnabled
        {
            get => _previewEnabled;
            set { _previewEnabled = value; ApplyPreview(); }
        }
        public bool DynamicEnabled
        {
            get => _runtime.DynamicEnabled;
            set
            {
                _runtime.DynamicEnabled = value;
                _status = value ? "Dynamic exposure enabled" : "Dynamic exposure disabled";
            }
        }
        public string CenterText
        {
            get => _centerText;
            set
            {
                _centerText = value ?? string.Empty;
                if (!TryParseNumber(_centerText, out var center) ||
                    center < MinimumEv + _width * 0.5f || center > MaximumEv - _width * 0.5f)
                {
                    _status = "Invalid center for current width";
                    return;
                }
                _center = center;
                _previewEnabled = true;
                ApplyPreview();
                _status = "Exposure bounds applied";
            }
        }
        public string WidthText
        {
            get => _widthText;
            set
            {
                _widthText = value ?? string.Empty;
                if (!TryParseNumber(_widthText, out var width) || width < 0f ||
                    width > 2f * Mathf.Min(_center - MinimumEv, MaximumEv - _center))
                {
                    _status = "Invalid width for current center";
                    return;
                }
                _width = width;
                _previewEnabled = true;
                ApplyPreview();
                _status = "Exposure bounds applied";
            }
        }
        public string TimeText
        {
            get => _timeText;
            set
            {
                _timeText = value ?? string.Empty;
                if (!TryParseNumber(_timeText, out var hours) || hours < 0f || hours >= 24f)
                {
                    _status = "Enter decimal hours from 0 to less than 24";
                    return;
                }
                _debug.SetTime(hours);
                _status = "Time applied";
            }
        }
        public string Status => _status;
        private string CustomFilePath => Path.Combine(Application.persistentDataPath, "SunGlasses", FileName);
        public string FilePath => ActiveCurve == "Custom" ? CustomFilePath : "Resources/exposure-curve/" + ActiveCurve + ".json (bundled)";

        public void SwitchCurve(string curve)
        {
            if (curve != "bright" && curve != "realistic" && curve != "Custom") return;
            try
            {
                List<Sample> loaded;
                if (curve == "Custom" && !File.Exists(CustomFilePath)) loaded = new List<Sample>();
                else
                {
                    using (var input = curve == "Custom" ? File.OpenRead(CustomFilePath) :
                        typeof(ExposureCalibrationController).Assembly.GetManifestResourceStream("SunGlasses.ExposureCurves." + curve + ".json"))
                    {
                        if (input == null) throw new FileNotFoundException("Bundled exposure curve not found: " + curve);
                        loaded = ReadSamples(input);
                    }
                }
                if (curve != "Custom" && loaded.Count == 0) throw new InvalidDataException("Exposure curve has no samples");
                var runtimeCurve = curve == "Custom" ? null : ExposureCurve.LoadBundled(curve);
                _samples.Clear();
                _samples.AddRange(loaded);
                ActiveCurve = curve;
                if (curve == "Custom") _samplesLoadFailed = false;
                if (curve != "Custom") _runtime.SetCurve(runtimeCurve, curve);
                _previewEnabled = false;
                ApplyPreview();
                _status = "Using " + curve + ": " + loaded.Count + " samples";
                SamplesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _status = "Curve switch failed; previous curve retained: " + ex.Message;
                Mod.Log.Error(_status);
            }
        }

        public ExposureCalibrationController(DynamicExposureSystem runtime, ExposureDebugSystem debug)
        {
            _runtime = runtime; _debug = debug;
            LoadSamples();
            SwitchCurve("realistic");
        }
        private void ApplyPreview()
        {
            if (_previewEnabled) _runtime.SetManualOverride(_center - _width / 2, _center + _width / 2);
            else _runtime.ClearManualOverride();
        }
        public void ResetPreview() { _previewEnabled = false; _runtime.ClearManualOverride(); }
        public void ApplyCustomCurve()
        {
            if (ActiveCurve != "Custom" || _samples.Count == 0) { _status = "Select Custom and record samples first"; return; }
            var points = new ExposureCurve.Point[_samples.Count];
            for (int i = 0; i < points.Length; i++)
            {
                var p = _samples[i];
                points[i] = new ExposureCurve.Point { timeOfDayHours = p.timeOfDayHours, solarElevationDegrees = p.solarElevationDegrees,
                    centerEv100 = p.centerEv100, widthEv100 = p.widthEv100 };
            }
            try { _runtime.SetCurve(new ExposureCurve(points), "Custom"); ResetPreview(); _status = "Custom curve applied"; }
            catch (Exception ex) { _status = "Custom curve rejected: " + ex.Message; }
        }
        public string SolarElevation() { var s = _runtime.GetSnapshot(); return s.Ready ? s.SolarElevationDegrees.ToString("F3", CultureInfo.InvariantCulture) + "°" : "Waiting for sun / exposure"; }
        public string SolarArcAngleText() { var s = _runtime.GetSnapshot(); return s.Ready ? ExposureCurve.SolarArcAngle(s.SolarElevationDegrees, s.TimeOfDayHours).ToString("F3", CultureInfo.InvariantCulture) + "°" : "Waiting for sun / exposure"; }
        public string AppliedRange() { var s = _runtime.GetSnapshot(); return s.Ready ? string.Format(CultureInfo.InvariantCulture, "{0:F3} .. {1:F3} EV100", s.MinimumEv100, s.MaximumEv100) : "Waiting for sun / exposure"; }
        public string ActiveInterpolationSamples() => _runtime.InterpolationDescription;
        public string RuntimeCurve => _runtime.ActiveCurve;
        public string EffectiveRange()
        {
            var camera = Camera.main;
            if (camera == null) return "No main camera";
            var exposure = HDCamera.GetOrCreate(camera, 0).volumeStack?.GetComponent<Exposure>();
            return exposure == null ? "Waiting for camera stack" :
                string.Format(CultureInfo.InvariantCulture, "{0:F3} .. {1:F3} EV100 ({2})", exposure.limitMin.value, exposure.limitMax.value, exposure.mode.value);
        }
        public string PreviewCheck()
        {
            if (!_runtime.ManualOverrideEnabled && !_runtime.DynamicEnabled) return "Off: game volumes control exposure";
            var snapshot = _runtime.GetSnapshot();
            if (!snapshot.Ready) return "Waiting for sun or lighting volume";
            var camera = Camera.main;
            if (camera == null) return "Waiting for main camera";
            var effective = HDCamera.GetOrCreate(camera, 0).volumeStack?.GetComponent<Exposure>();
            if (effective == null) return "Waiting for camera stack";
            if (effective.mode.value != ExposureMode.Automatic && effective.mode.value != ExposureMode.AutomaticHistogram)
                return "Effective exposure mode is " + effective.mode.value;
            return Mathf.Abs(effective.limitMin.value - snapshot.MinimumEv100) < 0.01f &&
                Mathf.Abs(effective.limitMax.value - snapshot.MaximumEv100) < 0.01f
                ? "Applied to main camera" : "Mismatch: check camera layer mask or competing Volume";
        }
        public void ReleaseTime() { _debug.ReleaseTime(); _timeText = FormatTime(_debug.CurrentTime); _status = "Time control released"; }
        public void Record()
        {
            if (ActiveCurve != "Custom") { _status = "Switch to Custom before editing samples"; return; }
            var s = _runtime.GetSnapshot();
            if (!s.Ready) { _status = "Cannot record: no sun or active exposure"; return; }
            _samples.Add(new Sample { recordedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                timeOfDayHours = s.TimeOfDayHours, solarElevationDegrees = s.SolarElevationDegrees,
                centerEv100 = (s.MinimumEv100 + s.MaximumEv100) / 2, widthEv100 = s.MaximumEv100 - s.MinimumEv100 });
            SaveSamples(); SamplesChanged?.Invoke();
        }
        public void RemoveSample(Sample sample)
        {
            if (ActiveCurve != "Custom") { _status = "Switch to Custom before editing samples"; return; }
            if (!_samples.Remove(sample)) return;
            SaveSamples(); SamplesChanged?.Invoke();
        }
        private void LoadSamples()
        {
            try
            {
                if (!File.Exists(CustomFilePath)) return;
                using (var input = File.OpenRead(CustomFilePath)) _samples.AddRange(ReadSamples(input));
                _status = "Loaded " + _samples.Count + " samples";
            }
            catch (Exception ex)
            {
                _samplesLoadFailed = true;
                _status = "Load failed; sample file preserved: " + ex.Message;
                Mod.Log.Error(_status);
            }
        }

        private static List<Sample> ReadSamples(Stream input)
        {
            var file = (SampleFile)new DataContractJsonSerializer(typeof(SampleFile)).ReadObject(input);
            if (file == null || file.version != 1) throw new InvalidDataException("Unsupported sample file");
            var records = file.samples ?? Array.Empty<Sample>();
            var loaded = new List<Sample>(records.Length);
            foreach (var sample in records)
            {
                if (sample == null || !Finite(sample.timeOfDayHours) || sample.timeOfDayHours < 0f || sample.timeOfDayHours >= 24f ||
                    !Finite(sample.solarElevationDegrees) || sample.solarElevationDegrees < -90f || sample.solarElevationDegrees > 90f || !Finite(sample.centerEv100) || !Finite(sample.widthEv100) || sample.widthEv100 < 0f ||
                    sample.centerEv100 - sample.widthEv100 * 0.5f < MinimumEv || sample.centerEv100 + sample.widthEv100 * 0.5f > MaximumEv)
                    throw new InvalidDataException("Sample file contains invalid records");
                loaded.Add(sample);
            }
            return loaded;
        }

        private void SaveSamples()
        {
            if (ActiveCurve != "Custom") return;
            if (_samplesLoadFailed)
            {
                _status = "Save skipped: existing sample file could not be loaded";
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var data = new SampleFile { samples = _samples.ToArray() };
                var serializer = new DataContractJsonSerializer(typeof(SampleFile));
                byte[] bytes;
                SampleFile check;
                using (var buffer = new MemoryStream())
                {
                    serializer.WriteObject(buffer, data);
                    bytes = buffer.ToArray();
                    buffer.Position = 0;
                    check = (SampleFile)serializer.ReadObject(buffer);
                }
                if (check?.samples == null || check.samples.Length != _samples.Count)
                    throw new InvalidDataException("Sample serialization did not preserve the list");
                for (var i = 0; i < _samples.Count; i++)
                {
                    var original = _samples[i];
                    var saved = check.samples[i];
                    if (saved == null || saved.recordedAtUtc != original.recordedAtUtc ||
                        Mathf.Abs(saved.timeOfDayHours - original.timeOfDayHours) > 0.0001f ||
                        Mathf.Abs(saved.solarElevationDegrees - original.solarElevationDegrees) > 0.0001f ||
                        Mathf.Abs(saved.centerEv100 - original.centerEv100) > 0.0001f ||
                        Mathf.Abs(saved.widthEv100 - original.widthEv100) > 0.0001f)
                        throw new InvalidDataException("Sample serialization changed a record");
                }
                var temporary = FilePath + ".tmp";
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                _status = "Saved " + _samples.Count + " samples";
            }
            catch (Exception ex) { _status = "Save failed: " + ex.Message; Mod.Log.Error(_status); }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string FormatTime(float hours)
        {
            return Mathf.Repeat(hours, 24f).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static bool TryParseNumber(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value);
        }
    }
}
