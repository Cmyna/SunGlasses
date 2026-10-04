# Temporary sky/direct sun split experiment

Tested 2026-10-03 in the running development player. **Unsuccessful.**
The temporary helper was removed. A subsequent first-stage mod implementation
was built and deployed, retaining the new light's surface influence to isolate
sky interaction. The user tested it and requested rollback because its appearance
was strange. All sky-split code and Debug controls have been removed; the earlier
sun tint comparison and specular-only sun remain.

The requested configuration was a neutral sky-only directional light, following
SunLight's transform and intensity, with the existing SunLight retaining warm
direct lighting and its shadow map. The existing temporary tint preview was
kept independent.

Observed results:

- Disabling both diffuse and specular influence on the neutral light produced
  a very dark sky and abnormal contrast. The exact HDRP light filtering path
  has not been proven; do not assume affectDiffuse=false supports a sky-only sun.
- Keeping diffuse enabled and using LightLayerEnum.Nothing restored sky energy
  but also added unwanted ground illumination. Live checks showed both
  pipeline supportLightLayers and the main camera LightLayers frame setting false.
- Enabling supportLightLayers by assigning currentPlatformRenderPipelineSettings
  repeatedly from beginCameraRendering caused rendering exceptions. An
  UpdateCurrentSky breakpoint allowed uninstalling this version and restoring
  the original sun, camera settings and pipeline support flag. A screenshot and
  the Gameface endpoint confirmed recovery at that point.
- A subsequent guarded assignment attempted during rendering threw
  InvalidOperationException: "A shared texture can only be release outside of
  render graph execution." The player then crashed and the process exited.

All injected assemblies and scene objects were session-only; no settings or save
files were written. Restart clears the injection. Do not change HDRP asset
settings from a rendering callback or a debugger call during render execution.
Any further attempt needs a verified rendering lifecycle and an isolation route
that does not rely on unavailable Light Layers. Sky/cloud illumination and
ambient probe refresh must be tested separately from surface direct illumination.
