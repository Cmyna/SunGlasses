using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using SunGlasses.Systems;
using Game.Simulation;
using UnityEngine;

namespace SunGlasses.Debugging
{
    // Calibration samples are deliberately separate from the mod's normal settings.
    public sealed class ExposureSamplingController
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
        private readonly PlanetarySystem _planetary;
        private bool _timeOwned, _previousOverride;
        private float _previousTime, _lastTime;
        public event Action SamplesChanged;
        private bool _previewEnabled;
        private bool _samplesLoadFailed;
        private float _center = 11.5f;
        private float _width = 1f;
        private string _timeText = "12";
        private string _centerText = "11.5";
        private string _widthText = "1";
        private string _status = "Ready";
        public IReadOnlyList<Sample> Samples => _samples;
        public bool PreviewEnabled
        {
            get => _previewEnabled && _runtime.ManualOverrideEnabled;
            set { _previewEnabled = value; ApplyPreview(); }
        }
        public bool CustomEnabled
        {
            get => _runtime.CustomCurveEnabled;
            set
            {
                if (value) ApplyCustomCurve();
                else
                {
                    try
                    {
                        _runtime.ClearTemporaryOverrides();
                        ResetPreview();
                        _status = "Custom curve disabled; preset restored";
                    }
                    catch (Exception ex) { _status = "Could not restore preset: " + ex.Message; }
                }
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
                SetTime(hours);
                _status = "Time applied";
            }
        }
        public string Status => _status;
        public string FilePath => Path.Combine(Application.persistentDataPath, "SunGlasses", FileName);

        public ExposureSamplingController(DynamicExposureSystem runtime, PlanetarySystem planetary)
        {
            _runtime = runtime; _planetary = planetary;
            _timeText = FormatTime(_planetary.time);
            ReloadSamples();
        }
        private void ApplyPreview()
        {
            if (_previewEnabled) _runtime.SetManualOverride(_center - _width / 2, _center + _width / 2);
            else _runtime.ClearManualOverride();
        }
        public void ResetPreview() { _previewEnabled = false; _runtime.ClearManualOverride(); }
        public void ApplyCustomCurve()
        {
            if (_samples.Count == 0) { _status = "Record custom samples first"; return; }
            var points = new ExposureCurve.Point[_samples.Count];
            for (int i = 0; i < points.Length; i++)
            {
                var p = _samples[i];
                points[i] = new ExposureCurve.Point { timeOfDayHours = p.timeOfDayHours, solarElevationDegrees = p.solarElevationDegrees,
                    centerEv100 = p.centerEv100, widthEv100 = p.widthEv100 };
            }
            try { _runtime.SetCustomCurve(new ExposureCurve(points)); ResetPreview(); _status = "Custom curve applied (temporary)"; }
            catch (Exception ex) { _status = "Custom curve rejected: " + ex.Message; }
        }
        private void SetTime(float hours)
        {
            if (!_timeOwned)
            {
                _previousOverride = _planetary.overrideTime;
                _previousTime = _planetary.time;
                _timeOwned = true;
            }
            _planetary.overrideTime = true;
            _planetary.time = hours;
            _lastTime = hours;
        }
        public void ReleaseTime()
        {
            // Relinquish ownership without overwriting a subsequent caller's override.
            if (_timeOwned && _planetary.overrideTime && Mathf.Approximately(_planetary.time, _lastTime))
            {
                _planetary.time = _previousTime;
                _planetary.overrideTime = _previousOverride;
            }
            _timeOwned = false;
            _timeText = FormatTime(_planetary.time);
            _status = "Time control released";
        }
        public void Shutdown() { ReleaseTime(); ResetPreview(); }
        public void Record()
        {
            var s = _runtime.GetSnapshot();
            if (!s.Ready) { _status = "Cannot record: no sun or active exposure"; return; }
            _samples.Add(new Sample { recordedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                timeOfDayHours = s.TimeOfDayHours, solarElevationDegrees = s.SolarElevationDegrees,
                centerEv100 = (s.MinimumEv100 + s.MaximumEv100) / 2, widthEv100 = s.MaximumEv100 - s.MinimumEv100 });
            SaveSamples(); SamplesChanged?.Invoke();
        }
        public void RemoveSample(Sample sample)
        {
            if (!_samples.Remove(sample)) return;
            SaveSamples(); SamplesChanged?.Invoke();
        }
        public void ReloadSamples()
        {
            try
            {
                var loaded = new List<Sample>();
                if (File.Exists(FilePath))
                    using (var input = File.OpenRead(FilePath)) loaded = ReadSamples(input);
                _samples.Clear();
                _samples.AddRange(loaded);
                _samplesLoadFailed = false;
                _status = "Loaded " + _samples.Count + " samples";
                SamplesChanged?.Invoke();
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
