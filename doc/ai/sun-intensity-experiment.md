# Sun intensity adjustment experiment

Removed on 2026-10-04 following the user's in-game evaluation.

The purpose of the sun intensity multiplier was to test whether adjusting the
sun's intensity could reduce the excessively yellow lighting from HDRP's
Physically Based Sky when the sun approached the horizon. The experiment proved
ineffective for this purpose, so the intensity adjustment was removed.

The removed implementation was `RemapLightingSystem.SunIntensity.cs`, which
multiplied the vanilla sun's `Light.intensity`, tracked its baseline to avoid
compounding the multiplier, and restored the baseline when released. Its update
and cleanup calls, debug multiplier control, before/after readout, and reset
button were also removed.

This result records the outcome of the tested adjustment; it does not establish
the internal cause of HDRP's near-horizon yellow lighting.

Separately, the specular-only duplicate sun now overrides `angularDiameter` to
zero after `HDAdditionalLightData.CopyTo` on every update. This ablates the finite
angular size for the duplicate without changing the original sun's disk size.
The visual effect of this ablation still requires in-game evaluation.
