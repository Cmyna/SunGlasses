# 动态曝光与校准工具

## 运行功能与 Debug 边界

动态曝光默认使用内置 `realistic` 曲线，每个渲染帧根据太阳角度计算自动曝光上下限，不依赖开发者菜单是否打开或 Debug 系统是否启用。

| 模块 | 责任 |
| --- | --- |
| `DynamicExposureSystem` | DOTS 托管系统：读取太阳状态、求值、应用曝光、管理 Volume 和场景资源 |
| `ExposureCurve` | 曲线数据校验与复制、太阳弧角插值、内置曲线加载；不引用 Debug 类型 |
| `ExposureDebugSystem` | DOTS 托管系统：持有校准 Controller，管理临时时间覆盖及恢复 |
| `ExposureCalibrationController` | 普通 C# 类：校准参数、样本录制和删除、Custom JSON、操作状态、相机诊断 |
| `ExposureCalibrationDebugTab` | 开发者菜单 Exposure 区域及操作绑定 |

动态系统通过 UpdateSystem 显式注册在 PreCulling 的 RemapLightingSystem 之后；Debug 系统注册在 MainLoop，每帧不执行曝光逻辑，保留入口以处理禁用时的停止清理。两者使用自身 World 的 PlanetarySystem。曝光不再由自定义 MonoBehaviour 或 LateUpdate 驱动。亮度监测、CustomPass、GPU 回读和平均亮度/P50 行已删除。

## 曝光应用规则

系统持有一个全局 HDRP Volume，优先级 3000、权重 1，使用 LightingPostProcessVolume 的层。只覆盖 Exposure.limitMin 和 limitMax，保留底层 Volume 的曝光模式及其他参数。组件用 VolumeProfile.Add<Exposure>(false) 创建；true 会启用其他覆盖，包括 Fixed 模式，可能导致画面过曝。

1. 手动校准预览开启：采用 center ± width/2。
2. 否则动态曝光开启：采用运行曲线的插值结果。
3. 两者都关闭：禁用 Volume，交由其他 Volume 控制曝光。

手动预览与动态开关独立。关闭手动预览会恢复已开启的动态曲线；关闭动态曝光不会关闭已开启的手动预览。有效的 Center 或 Width 编辑开启手动预览，但不改变动态开关。

太阳必须有效、Transform 必须存在、游戏时间必须有限、原始照明 Volume 必须可用，覆盖才能生效。主菜单及资源未就绪时显示等待状态。系统发布本帧已写入自身 Volume 的 ExposureSnapshot，录制与 Applied exposure bounds 读取该快照，避免把另一次求值当作已应用结果。

Preview check 独立读取主相机的 HDRP 栈。模式为 Automatic 或 AutomaticHistogram，且上下限与快照误差均小于 0.01 EV100 时显示 Applied to main camera。快照本身不能证明其他 Volume 没有覆盖它。

## 曲线插值

上午太阳弧角取太阳高度；下午正高度映射为 180° - elevation，负高度映射为 -180° - elevation。每个实际样本在求值时提供一个镜像节点，JSON 与校准列表只保存实际样本。

按圆周相邻节点的角距离插值，包括 ±180° 接缝。时间只选择太阳弧的上午或下午一侧，不决定插值距离。单样本给出恒定范围；最高样本与镜像之间也保持该样本上下限。Active interpolation samples 显示运行曲线样本编号、角度、镜像标识和比例；手动预览时显示 Off。

接收曲线时校验：至少一个样本，时间为 [0,24)、高度为 [-90,90]、数值有限、宽度非负、上下限位于 [-10,20] EV100。数据被复制，后续校准编辑不会修改运行曲线。

## 校准操作

开发者菜单 SunGlasses → Exposure：

| 操作或显示项 | 行为 |
| --- | --- |
| Use bright curve / Use realistic curve | 切换内置运行曲线并开启动态曝光，加载只读样本，清除手动预览 |
| Use custom samples | 加载可编辑 Custom 样本，清除手动预览，保留运行曲线及动态开关 |
| Calibration sample source | 当前校准列表来源，可能与运行曲线不同 |
| Runtime exposure curve | 当前动态曝光的曲线名称 |
| Apply custom curve | 将非空 Custom 列表校验并复制到运行曲线，开启动态曝光、清除手动预览 |
| Manual calibration preview | 开启或释放手动上下限覆盖 |
| Enable dynamic exposure | 控制曲线运行，与手动预览独立 |
| Record sample | 仅在 Custom 来源下，把可用已应用快照记录为新样本并保存 |
| Remove sample N | 删除指定 Custom 记录并保存，不修改已提交的运行曲线 |

Center、Width 支持数值文本。宽度非负，上下限必须保持在 -10 到 20 EV100。非法或未完成文本保留供编辑，但不改变有效值，Status 给出原因。没有可用曝光快照时拒绝录制。

Custom 编辑后必须再次 Apply custom curve 才改变运行曲线。删光记录不会禁用已有运行曲线，空列表不可提交。录制或删除通过 SamplesChanged 事件通知面板刷新，Controller 不直接调用 UI 重建。

## Debug 时间与生命周期

Time 接受 [0,24) 的十进制小时，例如 5.85 为 05:51。首次接管保存 PlanetarySystem 的时间及 override 状态，然后设置 overrideTime=true。Release time control 恢复保存状态；如果其他调用者已改变覆盖值，则放弃自身所有权，不覆盖对方的新值。

关闭菜单不会释放预览或时间控制。城市预加载、Debug 停止、Mod 卸载、系统销毁时释放临时时间与手动预览。动态系统在城市预加载、停止、卸载、销毁时释放 Volume/Profile，之后按场景资源可用性重建。World 可跨城市保留，系统销毁不能代替切城通知。

曲线选择、动态开关和校准参数属于会话状态，不写入城市存档。Custom 样本属于独立文件。关闭 Debug 不丢失曲线或样本，也不停止动态曝光。

## 样本文件

Custom 文件是 Unity Application.persistentDataPath 下的 SunGlasses/exposure-calibration.json，保留 version 1 格式：

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

使用 DataContractJsonSerializer。加载时先完整校验，失败时保留原列表和文件；读取失败后禁止保存，直到成功重新选择 Custom。仅有 version 的旧文件视为空列表。录制或删除触发保存，先序列化并读回验证所有记录，再经临时文件替换；错误显示在 Status 和日志中。

## 实现对齐与验证

2026-10-04 完成拆分，官方 C# 编译、Mod 后处理、本地部署均通过，0 警告、0 错误。构建与部署 DLL 的 SHA256 一致。通过 Unity 调试连接和游戏 UI 在载入的城市中验证：

- realistic 默认启用，主菜单无太阳时休眠，进入城市后生效；主相机上下限匹配，模式保持 AutomaticHistogram。
- 手动中心 10、宽度 2 得到 9..11 EV100；关闭预览恢复曲线，无效输入不改变有效参数。
- Custom 来源保留 realistic 运行曲线；录制保存 version 1 文件成功，显式应用得到记录对应的范围。
- 删除并重新加载校准列表后，运行曲线仍持有之前提交的副本。
- bright 在固定晚间时间生效；realistic 已知节点及镜像节点均返回 11.5..12.5 EV100。
- 模拟速度为 0 时，Debug 调整时间后曝光继续更新并匹配主相机。
- 禁用 Debug 释放时间和手动覆盖，动态曝光继续；禁用动态系统释放资源，重新启用可重建并生效。
- 调试面板构建成功，日志没有相关曝光异常。测试后原始 Custom 文件已恢复，哈希一致，恢复 realistic、正常速度和非覆盖时间。

真实的第二次城市加载及完整进程退出回归尚未执行，相应清理由预加载、停止和销毁入口实现。不同地图、季节、天气和相机方向的视觉质量仍需继续人工校准。
