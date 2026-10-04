# Atmosphere Override System

## Feature boundaries

Atmosphere overrides are a production runtime feature managed by `AtmosphereSystem : GameSystemBase`, independent of the developer menu and custom MonoBehaviours. The system starts enabled with a fixed default configuration. This stage does not extend Setting, add configuration files, or write parameters to city saves.

`AtmosphereParameters` is the single source of numeric configuration, used for both initialization and scene resource recreation. The system exposes TrySetParameters to accept a validated configuration copy, providing an entry point for a future settings page. The Debug Atmosphere section retains the Enable atmosphere overrides checkbox, which directly reads and writes the system's OverridesEnabled property. All other status and parameter fields are read-only diagnostics; there are no parameter editing fields. The switch is not persisted in Setting or saves.

## Defaults and override scope

The defaults now match the values displayed by the old Debug input fields, correcting the previous mismatch between displayed and rendered values.

| HDRP parameter | Default |
| --- | --- |
| PhysicallyBasedSky.aerosolDensity | 0.024 |
| PhysicallyBasedSky.aerosolAnisotropy | 0.8 |
| PhysicallyBasedSky.aerosolTint | RGB (1, 1, 1) |
| PhysicallyBasedSky.colorSaturation | 0.7 |
| PhysicallyBasedSky.horizonTint | RGB (0.5, 0.65, 1) |
| Fog.tint | RGB (0.8, 0.8, 1) |
| Fog.meanFreePath | 20000 meters |
| CloudLayer.layerA.ambientProbeDimmer | 1 |

All components are created with Add(false), enabling overrideState only for these eight parameters. Fog enable, color mode, height, weather, cloud layer types, and other parameters remain under game control. Whether Fog.tint affects the final image therefore also depends on the underlying fog configuration; matching camera-stack parameters does not prove that the effect is visible in the current image.

The global Volume is named SunGlasses Atmosphere Volume, with weight 1 and priority 1500, and uses the layer of LightingPostProcessVolume. It takes precedence over vanilla lighting (1000) and yields to enabled Photo Mode overrides (2000). When overrides are enabled, the mod controls these eight parameters unless a higher-priority Volume overrides them. When disabled, the underlying game or Photo Mode parameters take effect again.

Aerosol density, color saturation, and cloud ambient intensity must be in [0,1], anisotropy in [-1,1], and color channels in [0,1]. All values must be finite. The minimum fog attenuation distance is read directly from HDRP Fog.meanFreePath.min, verified locally as 1 meter. Invalid configurations are rejected as a whole without changing the existing valid configuration.

## Updates and lifecycle

自定义雾兼容：每帧读取游戏原生 ClimateSystem.fog.overrideState。为 true 时暂停本模组的 Fog.tint 和 Fog.meanFreePath 覆盖，让游戏气候雾接管；恢复为 false 后，下一次渲染更新重新启用这两个覆盖。适用于 Time & Weather Anarchy、自带开发者菜单 Override Fog，以及使用同一接口的其他模组，不依赖其程序集。太阳色调、天空与云层校正继续生效，因此仍可能改变雾的最终视觉观感。参数值在暂停期间保留，配置更新及资源重建后也遵循相同规则。

The system is explicitly registered through UpdateSystem in PreCulling after DynamicExposureSystem. The current order is LightingSystem → RemapLightingSystem → DynamicExposureSystem → AtmosphereSystem.

Each frame checks scene state, the sun Transform, and the original lighting Volume. Overrides are disabled with a waiting status while resources are unavailable or loading is in progress. The Volume is created once the scene is ready. Parameters are written only after configuration changes or resource recreation, avoiding repeated writes of all HDRP parameters every frame.

City preload suspends overrides and releases resources; loading completion permits recreation. System stop, mod disposal, and system destruction also release the Volume, Profile, and Profile components. If the scene destroys the host object itself, the system cleans up the old Profile before recreating resources. The game World can survive across cities, so resource cleanup does not depend on World destruction.

OverridesEnabled controls atmosphere overrides themselves; the system's Enabled property controls updates and resource lifecycle. 设置中的独立“颜色校正”勾选项默认开启，保存到模组设置，并在加载及应用设置时恢复。开启时启用大气覆盖，同时选择 Gentle warmth 太阳色调预设（RGB 0.76、0.85、1）；关闭时禁用大气覆盖，同时选择 Original / no tint 预设（白色）。原 Previous gentle warmth 预设更名为 Gentle warmth。现有按太阳高度向白色渐变的规则保持有效。选择曝光曲线“无”不会关闭颜色校正。Debug 对开关或色调的临时修改不写入保存设置，应用设置会恢复对应的预设。Disabling atmosphere overrides directly in Debug does not affect dynamic exposure or sun lighting. Re-enabling the system recreates resources using the retained configuration; restarting restores atmosphere parameters to defaults and loads the saved Color Correction switch.

## Debug diagnostics

SunGlasses → Atmosphere provides the Enable atmosphere overrides checkbox and displays system status, whether the Volume is active, configured sky and fog/cloud parameters, effective main-camera parameters, and a camera match check. Unchecking disables the override Volume on the next PreCulling update; checking reapplies the current configuration.

Atmosphere camera check compares the actively controlled parameters in the main camera's HDRP stack against the configuration. 自定义雾接管时跳过两项雾参数比较，显示 Applied to main camera; fog delegated to custom fog，同时提供 Fog overrides suspended for custom fog 状态。It displays Applied to main camera when all controlled parameters match, Off when the system or overrides are disabled, Waiting when resources are not ready, and Mismatch for competing Volumes or camera-layer mismatches. Color checks compare RGB, and scalar checks use small absolute/relative error tolerances.

## Implementation alignment and verification

2026-10-05 自定义雾兼容：官方编译、后处理与本机部署通过，0 警告、0 错误。基于本机游戏及 Time & Weather Anarchy v3.2 程序集确认原生雾覆盖接口和衰减距离冲突。本次尚未验证城市中自定义雾切换与最终画面。

2026-10-04 设置与优先级调整：官方编译与后处理通过，0 警告、0 错误。游戏程序集确认常规照明 1000、Photo Mode 2000；大气覆盖改为 1500。本次未部署，游戏未运行，设置切换及 Photo Mode 覆盖的视觉效果尚未在游戏内验证。以下为此前实现的验证记录。

2026-10-04: official C# compilation, mod post-processing, and local deployment passed with 0 warnings and 0 errors. The deployed and built DLLs had matching SHA256 hashes.

In-game verification results:

- The system was dormant in the main menu without a valid scene. After entering test-map, atmosphere and dynamic exposure both reported Applied to main camera.
- Effective camera density was 0.024, horizon RGB was (0.5,0.65,1), and fog RGB was (0.8,0.8,1); all other parameters matched defaults.
- Inspecting parameter flags confirmed exactly five sky, two fog, and one cloud overrides. Fog enable and color mode were not overridden.
- Non-finite configurations were rejected. Valid density and fog-distance changes took effect on subsequent frames. Defaults were restored after testing.
- Disabling overrides restored underlying atmosphere values in the camera. Stopping the system released the host and Profile; re-enabling recreated resources and matched the camera. Dynamic exposure remained functional throughout.
- With simulation paused and time changed through the existing Debug controls, atmosphere and exposure still matched the camera. The specular sun remained enabled with lightDimmer 0.01, and the existing lighting system continued operating normally.
- The new version exited and restarted successfully. In the final process, third-party mod backends, the full HUD, and all three Debug sections worked. UI, Modding, SceneFlow, and SunGlasses logs contained no ERROR entries or exceptions. Normal simulation speed was restored and the time override released.

The first verification process omitted several third-party mods' C# backends, causing UI binding errors. An isolated build of the previous commit was used for comparison, then the same new-version DLL was restored and launched again. The new process loaded those backends completely and the errors did not recur. No third-party files or game mod combination were changed. The root cause of the initial omissions remains undetermined.

Actual city switching within one process and individual Photo Mode editing interactions have not received a full regression test. Current verification confirms resource stop/recreation and camera-stack matching. Visual quality across different maps, weather, and seasons still requires manual adjustment.
