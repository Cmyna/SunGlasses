using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunGlasses.Debugging
{
    // Read-only pass. No changes to exposure, the camera output or other mods' AOV requests.
    public sealed class ScreenBrightnessMonitor : MonoBehaviour
    {
        private const float SamplingInterval = 0.5f;
        private const float DemandTimeout = 2f;
        private BrightnessPass _pass;
        private CustomPassVolume _passVolume;
        private float _lastDemand = float.NegativeInfinity;
        private float _lastSample = float.NegativeInfinity;
        private float _mean;
        private float _median;
        private string _status = "Waiting for post-process frame";
        private bool _supported;

        private void Awake()
        {
            _supported = SystemInfo.supportsAsyncGPUReadback &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);
            if (!_supported)
            {
                _status = "GPU readback / float texture unsupported";
                return;
            }
            _pass = new BrightnessPass(this)
            {
                name = "SunGlasses Screen Brightness",
                targetColorBuffer = CustomPass.TargetBuffer.None,
                targetDepthBuffer = CustomPass.TargetBuffer.None
            };
            _passVolume = gameObject.AddComponent<CustomPassVolume>();
            _passVolume.isGlobal = true;
            _passVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            // Observe other custom passes at this point before sampling.
            _passVolume.priority = -10000f;
            _passVolume.customPasses.Add(_pass);
        }

        public string AverageText() => ReadText(_mean);
        public string MedianText() => ReadText(_median);

        private string ReadText(float value)
        {
            _lastDemand = Time.realtimeSinceStartup;
            if (!_supported || float.IsNegativeInfinity(_lastSample)) return _status;
            var age = Time.realtimeSinceStartup - _lastSample;
            if (age > DemandTimeout) return "Waiting for fresh post-process frame: " + _status;
            return string.Format(CultureInfo.InvariantCulture, "{0:F2}% (SDR, scene only)", value * 100f);
        }

        private bool SamplingRequested => Time.realtimeSinceStartup - _lastDemand <= DemandTimeout;

        private void OnDestroy()
        {
            if (_passVolume != null) _passVolume.enabled = false;
            _pass?.Stop();
        }

        [Serializable]
        private sealed class BrightnessPass : CustomPass
        {
            // Uniform low-resolution samples approximate the full-frame statistics.
            private const int Width = 256;
            private const int Height = 144;
            private readonly ScreenBrightnessMonitor _owner;
            private readonly float[] _luminances = new float[Width * Height];
            private RenderTexture _samples;
            private bool _pending;
            private bool _stopped;
            private float _nextSample;

            internal BrightnessPass(ScreenBrightnessMonitor owner) { _owner = owner; }
            protected override bool executeInSceneView => false;

            protected override void Execute(CustomPassContext ctx)
            {
                if (_stopped || _owner == null || !_owner.SamplingRequested || _pending ||
                    Time.realtimeSinceStartup < _nextSample || ctx.hdCamera.camera != Camera.main) return;
                _nextSample = Time.realtimeSinceStartup + SamplingInterval;
                var source = ctx.cameraColorBuffer;
                if (source?.rt == null || source.rt.dimension != TextureDimension.Tex2D)
                {
                    _owner._status = "Post-process color buffer unavailable";
                    return;
                }
                if (_samples == null)
                {
                    _samples = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat,
                        RenderTextureReadWrite.Linear)
                    {
                        name = "SunGlasses Brightness Samples",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (!_samples.Create())
                    {
                        _owner._status = "Could not allocate brightness sample texture";
                        ReleaseTexture();
                        return;
                    }
                }
                // Use the post-process viewport, excluding unused RTHandle allocation space.
                var viewport = ctx.hdCamera.postProcessScreenSize;
                var scale = new Vector2(Mathf.Clamp01(viewport.x / source.rt.width),
                    Mathf.Clamp01(viewport.y / source.rt.height));
                ctx.cmd.Blit(source.nameID, new RenderTargetIdentifier(_samples),
                    scale, Vector2.zero);
                _pending = true;
                ctx.cmd.RequestAsyncReadback(_samples, 0, TextureFormat.RGBAFloat, OnReadback);
            }

            private void OnReadback(AsyncGPUReadbackRequest request)
            {
                _pending = false;
                if (_stopped || _owner == null)
                {
                    ReleaseTexture();
                    return;
                }
                if (request.hasError)
                {
                    _owner._status = "Brightness GPU readback failed";
                    _owner._lastSample = float.NegativeInfinity;
                    return;
                }
                var pixels = request.GetData<Color>();
                var count = Math.Min(pixels.Length, _luminances.Length);
                for (var i = 0; i < count; i++)
                {
                    var color = pixels[i];
                    _luminances[i] = ScreenBrightnessStatistics.RelativeLuminance(color.r, color.g, color.b);
                }
                if (!ScreenBrightnessStatistics.TryCalculate(_luminances, count,
                    out _owner._mean, out _owner._median))
                {
                    _owner._status = "No valid brightness samples";
                    _owner._lastSample = float.NegativeInfinity;
                    return;
                }
                _owner._lastSample = Time.realtimeSinceStartup;
                _owner._status = "Sampling after post-processing";
            }

            internal void Stop()
            {
                _stopped = true;
                // Keep the resource alive until an outstanding GPU request completes.
                if (!_pending) ReleaseTexture();
            }

            protected override void Cleanup() { Stop(); }

            private void ReleaseTexture()
            {
                if (_samples == null) return;
                _samples.Release();
                UnityEngine.Object.Destroy(_samples);
                _samples = null;
            }
        }
    }
}
