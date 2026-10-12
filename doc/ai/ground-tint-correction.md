# PBS 地面环境光修正

## 行为

SunGlasses 根据实际太阳高度，调整 `Render Settings` GO 的运行时 Volume Profile 中
`PhysicallyBasedSky.groundTint`：太阳高度 ≤0° 保留原色，≥5° 使用设置选择的白天权重（默认保留原色），
0–5° 在线性 RGB 上渐变。修正随 RemapLightingSystem 每帧运行，
独立于 Color Correction、Atmosphere Overrides、曝光曲线及开发者菜单是否开启。
正常设置页的“天空”分组提供“地面补光”滑块，范围 0–1，步长 0.01，默认 1。
0 关闭白天地面补光，1 保留原版补光，中间值按比例保留原色；夜间始终保留原版地面照明。
设置保存到模组配置，加载、应用设置及重置默认时同步到运行参数。

开发者菜单 `SunGlasses → Atmosphere → Daytime ground tint weight (0 = black, 1 = original)`
提供一个 0–1 FloatField，步长 0.01，控制 ≥5° 的白天终点。0 为黑色，
1 为绑定参数时捕获的原色；夜间终点始终为原色，0–5° 过渡到所选白天终点。
保留原 Alpha。拒绝 NaN/Infinity，有限值限制到 0–1。
开发者菜单修改只在本次运行中保留，不写入设置；再次应用设置会恢复保存的“地面补光”值。

实际原色权重为 `Lerp(1, daytimeWeight, Clamp01(elevation / 5))`。
太阳高度使用 `Asin(Clamp(-sun.transform.forward.y, -1, 1))` 转为角度，
不依赖上一帧的 SunTintElevation 或时钟时间；太阳尚未有效时释放修改。
诊断显示当前高度、有效原色权重、原色及输出。

## 生命周期

- 使用 `Volume.profile`，不能修改 `sharedProfile` 来驱动已实例化的 Profile。
- GroundTintOverride 保存原始颜色、原 overrideState 和上次输出，避免每帧累乘。
- 参数被游戏或其他调用者改成新颜色时，更新插值基准；颜色编辑不把本修正开启的
  overrideState 当作原始开关状态。
- 场景/Profile 更换时释放旧参数、捕获新参数；目标消失时释放。
- Mod.OnDispose、OnStopRunning、OnDestroy 均释放；只还原仍等于本修正输出的参数，
  避免覆盖更新的外部编辑。
- 直接改源 PBS 参数，不另外添加全参数 Volume，不调用 Harmony。

## 证据与验证

2026-10-12 后续默认值调整：“重置为默认”关闭颜色校正，曝光曲线设为 None，
地面补光设为 1；首次使用及缺失设置也采用这组默认值，已有保存设置仍按保存值加载。
Release 构建及官方后处理通过，零警告、零错误；已部署本地，DLL 哈希一致。

2026-10-12：地面补光接入正常设置页，补齐中英文名称与说明，默认仍为 0。
Debug 构建及官方后处理通过，零警告、零错误；既有离线地面色调行为检查通过，
涵盖昼夜过渡、0/0.5/1 权重、重复帧和卸载还原。本次未部署，正常设置页交互尚未在游戏内验证。

2026-10-06 现场试验：修改运行时 groundTint 为黑色后，当前相机的有效值变黑、
sky hash 从 1849955632 变成 1255825280，SH 探针明显改变，建筑阴影墙面显著变暗。
用户确认背光面与向光阴影面的亮度不一致得到解决，要求默认启用。
用户要求继续保持黑色，因此现场参数保持黑色；未重启游戏验证新 DLL 的调试控件。

实际游戏 Unity Color/ColorParameter 的离线行为检查覆盖：黑色与 Alpha、0.5/1 插值、
1000 帧不累乘、卸载还原颜色和覆盖标记、外部颜色更新、参数对象切换、外部编辑保留、
空目标。脚本见 `research/test-ground-tint.ps1`，不需要启动游戏。

后续夜间反馈：PBS groundTint 也是夜晚环境光的一部分，全天全黑导致夜景照明不足。
按用户要求增加 0–5° 插值；边界 -20/0/5/40°、过渡 1/2.5/4°、夜间保留原色、
白天调试终点与过渡混合的行为检查通过。这替代此前全天全黑的默认行为。

构建命令：`dotnet build SunGlasses.csproj -c Debug -p:DeployLocalMod=false`。
运行中的程序集不能通过一次构建替换；构建与实际新 DLL 的现场 UI 验证要分别记录。

本次 Debug 构建及官方 IL/Burst 后处理通过，零警告、零错误；离线行为检查通过。
检测到 Cities2 进程已退出后，使用项目 DeployWIP 目标部署到本地 Mods/SunGlasses，
安装 DLL 与构建产物的 SHA256 一致。新调试控件尚未在重新启动的游戏中验证。
