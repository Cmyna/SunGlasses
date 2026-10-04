using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Game.Rendering;
using Game.Simulation;
using SunGlasses.Systems;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    // Calibration samples are deliberately separate from the mod's normal settings.
    public sealed class ExposureCalibrationController : MonoBehaviour
    {
        private const float MinimumEv = -10f;
        private const float MaximumEv = 20f;
        private const float PreviewPriority = 3000f;
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

        public static ExposureCalibrationController Instance { get; private set; }

        private readonly List<Sample> _samples = new List<Sample>();
        private Volume _volume;
        private VolumeProfile _profile;
        private Exposure _exposure;
        private PlanetarySystem _planetary;
        private bool _previewEnabled;
        private bool _interpolationEnabled;
        private bool _samplesLoadFailed;
        public string ActiveCurve { get; private set; } = "Custom";
        private bool _timeOverrideOwned;
        private bool _previousTimeOverride;
        private float _previousTime;
        private float _center = 11.5f;
        private float _width = 1f;
        private string _timeText = "12";
        private string _centerText = "11.5";
        private string _widthText = "1";
        private string _status = "Ready";
        private ScreenBrightnessMonitor _brightness;

        public string AverageScreenBrightness() => _brightness != null ? _brightness.AverageText() : "Waiting for brightness monitor";
        public string MedianScreenBrightness() => _brightness != null ? _brightness.MedianText() : "Waiting for brightness monitor";

        public IReadOnlyList<Sample> Samples => _samples;
        public bool PreviewEnabled
        {
            get => _previewEnabled;
            set { _previewEnabled = value; ApplyPreview(); }
        }
        public bool InterpolationEnabled
        {
            get => _interpolationEnabled;
            set
            {
                if (value && _samples.Count == 0)
                {
                    _status = "Record a sample before enabling interpolation";
                    return;
                }
                _interpolationEnabled = value;
                if (value) _previewEnabled = true;
                ApplyPreview();
                _status = value ? "Sample interpolation enabled" : "Manual exposure bounds enabled";
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
                _interpolationEnabled = false;
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
                _interpolationEnabled = false;
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
                BindPlanetary();
                if (_planetary == null) { _status = "Waiting for planetary system"; return; }
                if (!_timeOverrideOwned)
                {
                    _previousTimeOverride = _planetary.overrideTime;
                    _previousTime = _planetary.time;
                    _timeOverrideOwned = true;
                }
                _planetary.overrideTime = true;
                _planetary.time = hours;
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
                _samples.Clear();
                _samples.AddRange(loaded);
                ActiveCurve = curve;
                if (curve == "Custom") _samplesLoadFailed = false;
                _interpolationEnabled = loaded.Count > 0;
                _previewEnabled = _interpolationEnabled;
                ApplyPreview();
                _status = "Using " + curve + ": " + loaded.Count + " samples";
                ExposureCalibrationDebugTab.Rebuild();
            }
            catch (Exception ex)
            {
                _status = "Curve switch failed; previous curve retained: " + ex.Message;
                Mod.Log.Error(_status);
            }
        }

        public static ExposureCalibrationController Install()
        {
            if (Instance != null) return Instance;
            var host = new GameObject("SunGlasses Exposure Calibration");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<ExposureCalibrationController>();
            Instance.SwitchCurve("realistic");
            return Instance;
        }

        public static void Uninstall()
        {
            if (Instance == null) return;
            Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake()
        {
            _brightness = gameObject.AddComponent<ScreenBrightnessMonitor>();
            LoadSamples();
            BindPlanetary();
            if (_planetary != null) _timeText = FormatTime(_planetary.time);
        }

        private void LateUpdate()
        {
            BindPlanetary();
            if (_volume == null) CreateVolume();
            if (_volume != null && _previewEnabled) ApplyPreview();
        }

        private void BindPlanetary()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var current = world != null && world.IsCreated ? world.GetExistingSystemManaged<PlanetarySystem>() : null;
            if (current == _planetary) return;
            RestoreTime();
            _planetary = current;
        }

        private void CreateVolume()
        {
            var original = GameObject.Find(RemapLightingSystem.kLightingPostprocessVolume);
            if (original == null) return;
            gameObject.layer = original.layer;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SunGlasses Exposure Calibration Profile";
            // Add(false) leaves every Exposure parameter uninvolved until explicitly enabled.
            // Add(true) overrides mode as Fixed at 0 EV, making the scene white regardless of bounds.
            _exposure = _profile.Add<Exposure>(false);
            _exposure.limitMin.overrideState = true;
            _exposure.limitMax.overrideState = true;
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = PreviewPriority;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;
            ApplyPreview();
        }

        private void ApplyPreview()
        {
            if (_volume == null || _exposure == null) return;
            if (!_previewEnabled)
            {
                _volume.enabled = false;
                return;
            }
            if (_interpolationEnabled && !TryInterpolatedBounds(out _, out _))
            {
                _volume.enabled = false;
                return;
            }
            GetPreviewBounds(out var minimum, out var maximum);
            _exposure.limitMin.value = minimum;
            _exposure.limitMax.value = maximum;
            _volume.enabled = _previewEnabled;
        }

        private void GetPreviewBounds(out float minimum, out float maximum)
        {
            if (_interpolationEnabled && TryInterpolatedBounds(out minimum, out maximum)) return;
            minimum = _center - _width * 0.5f;
            maximum = _center + _width * 0.5f;
        }

        private bool TryInterpolatedBounds(out float minimum, out float maximum)
        {
            return TryInterpolatedBounds(out minimum, out maximum, out _, out _, out _, out _, out _);
        }

        private bool TryInterpolatedBounds(out float minimum, out float maximum,
            out Sample before, out Sample after, out float beforeAngle, out float afterAngle,
            out float fraction)
        {
            minimum = maximum = 0f;
            before = after = null;
            beforeAngle = float.NegativeInfinity;
            afterAngle = float.PositiveInfinity;
            fraction = 0f;
            if (!HasReadySun() || _samples.Count == 0) return false;

            var currentAngle = CurrentSolarArcAngle();
            Sample first = null;
            Sample last = null;
            var firstAngle = float.PositiveInfinity;
            var lastAngle = float.NegativeInfinity;

            foreach (var sample in _samples)
            {
                var recordedAngle = SolarArcAngle(sample.solarElevationDegrees, sample.timeOfDayHours);
                for (var side = 0; side < 2; side++)
                {
                    var angle = side == 0 ? recordedAngle : MirrorAngle(recordedAngle);
                    if (angle < firstAngle) { firstAngle = angle; first = sample; }
                    if (angle > lastAngle) { lastAngle = angle; last = sample; }
                    if (angle <= currentAngle && angle >= beforeAngle) { beforeAngle = angle; before = sample; }
                    if (angle >= currentAngle && angle <= afterAngle) { afterAngle = angle; after = sample; }
                }
            }
            if (before == null) { before = last; beforeAngle = lastAngle; }
            if (after == null) { after = first; afterAngle = firstAngle; }
            var span = Mathf.Repeat(afterAngle - beforeAngle, 360f);
            fraction = span > 0f ? Mathf.Repeat(currentAngle - beforeAngle, 360f) / span : 0f;
            minimum = Mathf.Lerp(before.centerEv100 - before.widthEv100 * 0.5f,
                after.centerEv100 - after.widthEv100 * 0.5f, fraction);
            maximum = Mathf.Lerp(before.centerEv100 + before.widthEv100 * 0.5f,
                after.centerEv100 + after.widthEv100 * 0.5f, fraction);
            return true;
        }

        private bool HasReadySun()
        {
            // SunLight can be valid before its scene Transform exists (main menu
            // and scene transitions). Keep the override dormant until both are ready.
            return _planetary != null && _planetary.SunLight.isValid
                && _planetary.SunLight.transform != null && Finite(_planetary.time);
        }

        private float CurrentSolarArcAngle()
        {
            var y = Mathf.Clamp(-_planetary.SunLight.transform.forward.y, -1f, 1f);
            return SolarArcAngle(Mathf.Asin(y) * Mathf.Rad2Deg, _planetary.time);
        }

        private static float SolarArcAngle(float elevation, float timeOfDayHours)
        {
            // Time only selects which half of the arc the sun is on. Interpolation
            // distances come exclusively from solar angles.
            return Mathf.Repeat(timeOfDayHours, 24f) < 12f ? elevation : MirrorAngle(elevation);
        }

        private static float MirrorAngle(float angle)
        {
            return angle >= 0f ? 180f - angle : -180f - angle;
        }

        public string SolarArcAngleText()
        {
            if (!HasReadySun()) return "Waiting for sun";
            return CurrentSolarArcAngle().ToString("F3", CultureInfo.InvariantCulture) + "°";
        }

        public string InterpolatedRange()
        {
            if (!_interpolationEnabled) return "Off";
            if (!TryInterpolatedBounds(out var minimum, out var maximum)) return "Waiting for sun or current game time";
            return string.Format(CultureInfo.InvariantCulture, "{0:F3} .. {1:F3} EV100", minimum, maximum);
        }

        public string ActiveInterpolationSamples()
        {
            if (!_interpolationEnabled) return "Off (manual bounds)";
            if (!_previewEnabled) return "Off (preview disabled)";
            if (!TryInterpolatedBounds(out _, out _, out var before, out var after,
                out var beforeAngle, out var afterAngle, out var fraction))
                return "Waiting for samples, sun or current game time";

            return string.Format(CultureInfo.InvariantCulture, "{0} -> {1}  t={2:F3}",
                InterpolationSampleLabel(before, beforeAngle),
                InterpolationSampleLabel(after, afterAngle), fraction);
        }

        private string InterpolationSampleLabel(Sample sample, float angle)
        {
            var recordedAngle = SolarArcAngle(sample.solarElevationDegrees, sample.timeOfDayHours);
            return string.Format(CultureInfo.InvariantCulture, "Sample {0} ({1:F3}°{2})",
                _samples.IndexOf(sample) + 1, angle,
                Mathf.Approximately(angle, recordedAngle) ? string.Empty : ", mirrored");
        }

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
            if (!_previewEnabled) return "Off: game volumes control exposure";
            if (_volume == null) return "Waiting for lighting volume";
            if (_interpolationEnabled && !_volume.enabled) return "Waiting for sun or current game time";
            for (var i = 0; i < _exposure.parameters.Count; i++)
            {
                var parameter = _exposure.parameters[i];
                if (parameter != _exposure.limitMin && parameter != _exposure.limitMax && parameter.overrideState)
                    return "Unexpected Exposure override (mode or other setting)";
            }
            var camera = Camera.main;
            if (camera == null) return "Waiting for main camera";
            var effective = HDCamera.GetOrCreate(camera, 0).volumeStack?.GetComponent<Exposure>();
            if (effective == null) return "Waiting for camera stack";
            if (effective.mode.value != ExposureMode.Automatic && effective.mode.value != ExposureMode.AutomaticHistogram)
                return "Bounds match, but effective exposure mode is " + effective.mode.value;
            return Mathf.Abs(effective.limitMin.value - _exposure.limitMin.value) < 0.01f &&
                   Mathf.Abs(effective.limitMax.value - _exposure.limitMax.value) < 0.01f
                ? "Applied to main camera"
                : "Mismatch: check camera layer mask or competing Volume";
        }

        public string SolarElevation()
        {
            if (!HasReadySun()) return "Waiting for sun";
            var y = Mathf.Clamp(-_planetary.SunLight.transform.forward.y, -1f, 1f);
            return (Mathf.Asin(y) * Mathf.Rad2Deg).ToString("F3", CultureInfo.InvariantCulture) + "°";
        }

        public void ReleaseTime()
        {
            RestoreTime();
            if (_planetary != null) _timeText = FormatTime(_planetary.time);
            _status = "Time control released";
        }

        private void RestoreTime()
        {
            if (!_timeOverrideOwned || _planetary == null) return;
            _planetary.time = _previousTime;
            _planetary.overrideTime = _previousTimeOverride;
            _timeOverrideOwned = false;
        }

        public void Record()
        {
            if (ActiveCurve != "Custom") { _status = "Switch to Custom before editing samples"; return; }
            if (!HasReadySun())
            {
                _status = "Cannot record: no sun";
                return;
            }
            var y = Mathf.Clamp(-_planetary.SunLight.transform.forward.y, -1f, 1f);
            GetPreviewBounds(out var minimum, out var maximum);
            _samples.Add(new Sample
            {
                recordedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                timeOfDayHours = _planetary.time,
                solarElevationDegrees = Mathf.Asin(y) * Mathf.Rad2Deg,
                centerEv100 = (minimum + maximum) * 0.5f,
                widthEv100 = maximum - minimum
            });
            SaveSamples();
            ExposureCalibrationDebugTab.Rebuild();
        }

        public void RemoveSample(Sample sample)
        {
            if (ActiveCurve != "Custom") { _status = "Switch to Custom before editing samples"; return; }
            if (!_samples.Remove(sample)) return;
            if (_samples.Count == 0 && _interpolationEnabled)
            {
                _interpolationEnabled = false;
                _previewEnabled = false;
            }
            SaveSamples();
            ApplyPreview();
            ExposureCalibrationDebugTab.Rebuild();
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
                    !Finite(sample.solarElevationDegrees) || !Finite(sample.centerEv100) || !Finite(sample.widthEv100) || sample.widthEv100 < 0f ||
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

        private void OnDestroy()
        {
            RestoreTime();
            if (_volume != null) Destroy(_volume);
            if (_profile != null) Destroy(_profile);
            if (Instance == this) Instance = null;
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
