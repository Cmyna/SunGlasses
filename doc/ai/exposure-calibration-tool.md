# Dynamic Exposure and Calibration Tools

## Runtime and Debug boundaries

Dynamic exposure defaults to None (no override). When a built-in curve is selected, each rendered frame calculates automatic exposure bounds from the sun angle, independently of whether the developer menu is open or the Debug system is enabled.

设置中的“曝光曲线”下拉框提供“无、真实、明亮”，默认“无”。正式配置由当前 `Mod.Setting` 实例唯一持有；每次渲染更新观察预设值，外部赋值即使没有调用 Apply 也会被读取。“无”关闭正式曝光覆盖；其余选项使用对应内置曲线。预设变化时清除临时 Custom 曲线和手动预览，无关设置变化或单纯调用 Apply 不清除它们。独立的“颜色校正”默认关闭，不受曝光预设影响，其大气及太阳色调规则见 atmosphere-system.md。恢复默认按钮清除临时覆盖，再保存“无”、颜色校正关闭、地面补光 1。旧的暗部增亮及其曝光模式、直方图百分位修改已移除。

曲线对象和上次处理的预设仅为资源缓存，不是第二份用户配置。预设不变时不重复加载；加载失败关闭正式覆盖并记录一次错误，不沿用其他预设，每帧不重试。改选预设或系统重新初始化后允许重新加载。缺少正式设置时释放资源并停止覆盖；默认比较实例的构造不影响正在使用的设置或曲线。

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

手动预览优先于临时 Custom 曲线，Custom 曲线优先于正式预设。正式预设为 None 时仍可显式启用实验预览或 Custom 曲线；面板分别显示基础预设与实际曝光来源。关闭预览恢复下层曲线。正式预设变化时清除两种临时覆盖；曝光面板重复选择当前预设也明确清除覆盖。有效 Center/Width 编辑启用手动预览，但不修改或保存正式预设。

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
| Use bright curve / Use realistic curve | 修改并保存正式预设，清除临时 Custom 曲线和手动预览；重复选择也清除覆盖，保留 Custom 样本列表 |
| Enable custom curve | 勾选时校验当前非空 Custom 列表并建立临时曲线，清除预览；取消时清除临时曝光覆盖，返回当前 Setting 的预设（包括 None）。不修改正式配置 |
| Reload custom samples | 重新加载 Custom 文件；成功时替换列表并解除保存保护，不改变已提交的运行时曲线或手动预览；失败时保留现有列表和文件 |
| Runtime exposure curve | Name of the current dynamic exposure curve |
| Apply custom curve | 校验并复制非空样本列表为临时 Custom 曲线，清除手动预览，不修改正式预设 |
| Manual calibration preview | Enables or releases manual bounds overrides |
| Enable dynamic exposure | 读取正式预设是否为 None；关闭写入并保存 None，从关闭状态开启选择并保存 Realistic，同时清除临时曝光覆盖 |
| Saved exposure preset / Exposure source | 分别显示 Setting 中的正式选择及实际使用的预设、临时 Custom 曲线或手动 EV 预览 |
| Clear temporary exposure overrides | 清除 Custom 曲线及手动预览，不修改保存的预设或样本 |
| Record sample | 将可用的已应用曝光快照记录到 Custom 列表并保存，无需先启用 Custom 曲线 |
| Remove sample N | Deletes and saves the specified Custom record without changing the submitted runtime curve |

Center and Width accept numeric text. Width must be nonnegative, and bounds must remain within -10 to 20 EV100. Invalid or incomplete text is retained for editing without changing valid values; Status explains the reason. Recording is rejected when no exposure snapshot is available.

After editing Custom samples, Apply custom curve must be invoked again to change the runtime curve. Deleting all records does not disable an existing runtime curve, and an empty list cannot be submitted. Recording, deletion or successful reload notifies the panel through SamplesChanged to refresh it; the Controller does not directly request UI rebuilding.

## Debug time control and lifecycle

Time accepts decimal hours in [0,24), for example 5.85 means 05:51. The first takeover saves PlanetarySystem's time and override state, then sets overrideTime=true. Release time control restores the saved state. If another caller has changed the override value, the controller relinquishes ownership without overwriting the other caller's new value.

Closing the menu does not release preview or time control. City preload, Debug stop, mod disposal, and system destruction release temporary time and manual preview. The dynamic system releases its Volume/Profile on city preload, stop, disposal, and destruction, then recreates them according to scene resource availability. The World can survive across cities, so system destruction cannot replace city-loading notifications.

正式预设保存到模组设置，与城市存档独立。Exposure 面板的内置预设按钮及启用开关与设置页使用同一实例，通过 ApplyAndSave 保存；Custom 曲线、手动预览、时间接管不写入普通设置，样本继续使用独立文件。恢复默认也释放临时时间接管。读档或动态系统停止会释放 Volume 和曲线缓存、清除临时曝光覆盖，恢复更新时按当前设置重建；关闭开发者菜单不改变正式配置。

## 设置来源改造验证

2026-10-16：`Tests/SettingsBehavior` 直接链接正式 Mod、Setting、曝光/大气/光照系统及调试控件源码，用最小引擎替身验证 39 项行为断言，包括反射赋值跳过 Apply、控件双向读写与保存入口、预设缓存、临时覆盖退出、默认实例隔离、资源重建及卸载。另有缺失 realistic 内置资源的独立检查，确认关闭覆盖、错误去重及改选有效预设恢复。使用实际 Unity 类型的既有地面色调行为检查也通过。

替身不验证实际磁盘写入、原生对象延迟销毁或 HDRP 相机混合；真实界面、冷启动保存恢复、暂停、跨城市切换及 Photo Mode 仍需游戏内验证。本次未部署、未发布。

Debug、Release 编译及 Windows/macOS/Linux 官方 IL/Burst 后处理均通过，两个配置均为零警告、零错误，构建使用 DeployLocalMod=false。

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
