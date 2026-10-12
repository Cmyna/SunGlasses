# Dynamic Exposure and Calibration Tools

## Runtime and Debug boundaries

Dynamic exposure defaults to None (no override). When a built-in curve is selected, each rendered frame calculates automatic exposure bounds from the sun angle, independently of whether the developer menu is open or the Debug system is enabled.

设置中的“曝光曲线”下拉框提供“无、真实、明亮”，默认“无”。“无”关闭动态曝光覆盖；其余选项应用对应的内置曲线。应用设置会清除手动曝光预览。独立的“颜色校正”勾选项默认关闭，不受曝光曲线选择影响：开启时启用大气覆盖并选择 Gentle warmth 太阳色调；关闭时禁用大气覆盖并选择 Original / no tint。两个选项保存到模组设置并在加载时应用。重置默认值会选择“无”、关闭颜色校正，并将地面补光设为 1。旧的暗部增亮设置及其曝光模式、直方图百分位修改已移除。

| Module | Responsibility |
| --- | --- |
| `DynamicExposureSystem` | DOTS managed system: reads sun state, evaluates and applies exposure, manages Volume and scene resources |
| `ExposureCurve` | Validates and copies curve data, interpolates by solar arc angle, loads built-in curves; does not reference Debug types |
| `ExposureDebugSystem` | 两种构建共用：内置预设切换、动态曝光开关、曝光诊断和 Exposure 面板；Debug 构建额外持有采样 controller 并负责其生命周期清理 |
| `ExposureSamplingController` | 仅 Debug：Custom 样本读写、采集与删除、Custom 曲线启停、手动曝光预览、时间接管与恢复 |
| `ExposureSamplingDebugTab` | 仅 Debug：Exposure calibration 面板和采样操作绑定 |

采样源码及系统、菜单的 Debug 扩展统一放在 `*.Debug.cs` 文件中。项目文件按构建配置集中控制编译：`Debug` 包含这些文件，其他配置排除。公共代码通过可省略实现的 partial 方法连接采样初始化、清理、菜单插入、手动曝光覆盖和诊断；Release 中对应的方法声明和调用均由编译器移除。源码中不再散布 `#if DEBUG`。Release 保留内置曲线及 Exposure 诊断面板，不包含采样类型、Custom 文件读写或时间接管。游戏中的开发者菜单与构建配置独立。

内置 JSON 仅由 `ExposureCurve.LoadBundled` 读取。采样 controller 启动时只加载 Custom 文件，不切换运行时曲线；采样列表始终是 Custom，内置预设切换不会替换该列表。

The dynamic system is explicitly registered through UpdateSystem in PreCulling after RemapLightingSystem. The Debug system is registered in MainLoop; it performs no exposure calculations each frame and retains the update entry point for stop cleanup when disabled. The dynamic system and Debug-only sampling controller use PlanetarySystem from the same World; diagnostics read the runtime snapshot. Exposure is no longer driven by a custom MonoBehaviour or LateUpdate. Brightness monitoring, CustomPass, GPU readback, and average brightness/P50 rows have been removed.

## Exposure application rules

The system owns a global HDRP Volume with priority 1500 and weight 1, using the layer of LightingPostProcessVolume. Vanilla lighting has priority 1000 and Photo Mode's CinematicControlVolume has priority 2000, so enabled Photo Mode overrides take precedence. It overrides only Exposure.limitMin and limitMax, preserving the exposure mode and other parameters from underlying Volumes. The component is created with VolumeProfile.Add<Exposure>(false); true would enable other overrides, including Fixed mode, potentially overexposing the image.

1. Debug 构建中，手动曝光预览开启时使用 center ± width/2。
2. Otherwise, when dynamic exposure is enabled, use the runtime curve's interpolated result.
3. When both are disabled, disable the Volume and let other Volumes control exposure.

Manual preview and the dynamic switch are independent. Disabling manual preview restores the enabled dynamic curve; disabling dynamic exposure does not disable an active manual preview. Valid Center or Width edits enable manual preview without changing the dynamic switch.

Overrides require a valid sun, an existing Transform, finite game time, and an available original lighting Volume. The main menu and unavailable resources produce a waiting status. The system publishes an ExposureSnapshot of the values written to its own Volume for the current frame. Recording and Applied exposure bounds read that snapshot, avoiding treating a separate evaluation as an already-applied result.

Exposure check independently reads the main camera's HDRP stack. It reports Applied to main camera when the mode is Automatic or AutomaticHistogram and both bounds differ from the snapshot by less than 0.01 EV100. The snapshot alone cannot prove that another Volume has not overridden it.

## Curve interpolation

The morning solar arc angle equals solar elevation. In the afternoon, positive elevation maps to 180° - elevation and negative elevation maps to -180° - elevation. Each actual sample supplies a mirrored node during evaluation; JSON and the calibration list store only actual samples.

Interpolation uses angular distance between adjacent nodes on the circle, including the ±180° seam. Time selects the morning or afternoon side of the solar arc but does not determine interpolation distance. A single sample produces constant bounds; the interval between the highest sample and its mirror also retains that sample's bounds. Active interpolation samples displays runtime sample indices, angles, mirror markers, and interpolation ratio. It displays Off during manual preview.

Curve acceptance requires at least one sample, time in [0,24), elevation in [-90,90], finite values, nonnegative width, and bounds within [-10,20] EV100. Data is copied, so later calibration edits do not change the runtime curve.

## Calibration operations

Developer menu: SunGlasses → Exposure（两种构建共用的预设、开关和诊断）；SunGlasses → Exposure calibration（仅 Debug 的手动采样与 Custom 操作）。

| Action or display | Behavior |
| --- | --- |
| Use bright curve / Use realistic curve | 应用内置运行时曲线并开启动态曝光，清除手动预览；保留 Custom 采样列表 |
| Enable custom curve | 勾选时校验并应用当前非空 Custom 列表，同时开启动态曝光并清除预览；取消时恢复最近一次通过设置或 Exposure 面板选中的内置预设（包括 None），并清除预览。勾选状态反映运行时是否选择 Custom，与全局动态曝光开关独立 |
| Reload custom samples | 重新加载 Custom 文件；成功时替换列表并解除保存保护，不改变已提交的运行时曲线或手动预览；失败时保留现有列表和文件 |
| Runtime exposure curve | Name of the current dynamic exposure curve |
| Apply custom curve | Validates and copies the nonempty Custom list into the runtime curve, enables dynamic exposure, and clears manual preview |
| Manual calibration preview | Enables or releases manual bounds overrides |
| Enable dynamic exposure | Controls curve evaluation independently of manual preview |
| Record sample | 将可用的已应用曝光快照记录到 Custom 列表并保存，无需先启用 Custom 曲线 |
| Remove sample N | Deletes and saves the specified Custom record without changing the submitted runtime curve |

Center and Width accept numeric text. Width must be nonnegative, and bounds must remain within -10 to 20 EV100. Invalid or incomplete text is retained for editing without changing valid values; Status explains the reason. Recording is rejected when no exposure snapshot is available.

After editing Custom samples, Apply custom curve must be invoked again to change the runtime curve. Deleting all records does not disable an existing runtime curve, and an empty list cannot be submitted. Recording, deletion or successful reload notifies the panel through SamplesChanged to refresh it; the Controller does not directly request UI rebuilding.

## Debug time control and lifecycle

Time accepts decimal hours in [0,24), for example 5.85 means 05:51. The first takeover saves PlanetarySystem's time and override state, then sets overrideTime=true. Release time control restores the saved state. If another caller has changed the override value, the controller relinquishes ownership without overwriting the other caller's new value.

Closing the menu does not release preview or time control. City preload, Debug stop, mod disposal, and system destruction release temporary time and manual preview. The dynamic system releases its Volume/Profile on city preload, stop, disposal, and destruction, then recreates them according to scene resource availability. The World can survive across cities, so system destruction cannot replace city-loading notifications.

The options-page curve selection is persisted in mod settings, independently of city saves. Debug changes to the runtime curve, dynamic switch, and calibration parameters are temporary; applying the mod settings restores the saved preset. Custom samples use a separate file. Disabling Debug does not discard curves or samples and does not stop dynamic exposure.

## Sample file

The Custom file is SunGlasses/exposure-calibration.json under Unity Application.persistentDataPath, retaining the version 1 format:

```json
{
  "version": 1,
  "samples": [{
    "recordedAtUtc": "2026-10-04T00:00:00.0000000Z",
    "timeOfDayHours": 6.5,
    "solarElevationDegrees": 5.0,
    "centerEv100": 11.5,
    "widthEv100": 1.0
  }]
}
```

Serialization uses DataContractJsonSerializer. Loading validates the entire file first; failure preserves the previous list and file. After a read failure, saving is blocked until Reload custom samples succeeds. Older files containing only version are treated as empty lists. Recording or deletion triggers saving: all records are serialized and read back for validation before replacement through a temporary file. Errors appear in Status and logs.

## Implementation alignment and verification

2026-10-09 采样边界拆分及 partial 重构：Debug、Release 的官方 C# 编译与后处理均通过，0 警告、0 错误。两份 DLL 的元数据检查确认：采样 controller、采样面板、持有采样 controller 的接口、手动曝光接口及所有 Debug partial 扩展方法仅存在于 Debug；运行时系统、曝光诊断及 bright / realistic 内嵌资源在两种构建中均存在。本次未部署；新的 Custom 启停、面板操作和时间恢复流程尚未进行游戏内验证。下方保留此前版本的验证记录，其旧面板操作名称不代表当前界面。

2026-10-04 设置接入：官方编译与后处理通过，0 警告、0 错误。本机游戏程序集确认常规照明优先级 1000、Photo Mode 优先级 2000；两个新覆盖 Volume 均为 1500。本次未部署，游戏未运行，新的设置操作及 Photo Mode 视觉效果尚未进行游戏内验证。以下为此前实现的验证记录。

The separation was completed on 2026-10-04. Official C# compilation, mod post-processing, and local deployment passed with 0 warnings and 0 errors. Built and deployed DLLs had matching SHA256 hashes. The following were verified in a loaded city through the Unity debugging connection and game UI:

- realistic was enabled by default, dormant in the main menu without a sun, and active in the city. Main-camera bounds matched, and the mode remained AutomaticHistogram.
- Manual center 10 and width 2 produced 9..11 EV100. Disabling preview restored the curve; invalid input did not change valid parameters.
- Selecting Custom preserved the realistic runtime curve. Recording successfully saved a version 1 file, and explicit application produced the range corresponding to the recorded sample.
- After deleting and reloading the calibration list, the runtime curve still retained its previously submitted copy.
- bright took effect at a fixed evening time; known realistic nodes and mirrored nodes both returned 11.5..12.5 EV100.
- With simulation speed 0, Debug time changes continued updating exposure and matching the main camera.
- Disabling Debug released time and manual overrides while dynamic exposure continued. Disabling the dynamic system released resources; re-enabling recreated them and restored their effect.
- The Debug panel built successfully, and logs contained no related exposure exceptions. After testing, the original Custom file was restored with an identical hash, along with realistic, normal simulation speed, and no time override.

An actual second city load and a full process-exit regression had not been performed for this verification record. Cleanup is implemented through preload, stop, and destruction entry points. Visual quality across different maps, seasons, weather, and camera directions still requires further manual calibration.
