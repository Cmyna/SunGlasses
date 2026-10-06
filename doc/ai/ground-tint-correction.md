# PBS 地面环境光修正

## 行为

SunGlasses 默认将 `Render Settings` GO 的运行时 Volume Profile 中
`PhysicallyBasedSky.groundTint` 设为黑色。修正随 RemapLightingSystem 每帧运行，
独立于 Color Correction、Atmosphere Overrides、曝光曲线及开发者菜单是否开启。
正常设置页没有相关开关。

开发者菜单 `SunGlasses → Atmosphere → Ground tint weight (0 = black, 1 = original)`
提供一个 0–1 FloatField，步长 0.01。0 为黑色，1 为绑定参数时捕获的原色，
中间值在线性 RGB 上插值并保留原 Alpha。拒绝 NaN/Infinity，有限值限制到 0–1。
权重只在本次运行中保留，不进入 Setting，不写存档；Mod.OnLoad 重置为 0。

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

2026-10-06 现场试验：修改运行时 groundTint 为黑色后，当前相机的有效值变黑、
sky hash 从 1849955632 变成 1255825280，SH 探针明显改变，建筑阴影墙面显著变暗。
用户确认背光面与向光阴影面的亮度不一致得到解决，要求默认启用。
用户要求继续保持黑色，因此现场参数保持黑色；未重启游戏验证新 DLL 的调试控件。

实际游戏 Unity Color/ColorParameter 的离线行为检查覆盖：黑色与 Alpha、0.5/1 插值、
1000 帧不累乘、卸载还原颜色和覆盖标记、外部颜色更新、参数对象切换、外部编辑保留、
空目标。脚本见 `research/test-ground-tint.ps1`，不需要启动游戏。

构建命令：`dotnet build SunGlasses.csproj -c Debug -p:DeployLocalMod=false`。
运行中的程序集不能通过一次构建替换；构建与实际新 DLL 的现场 UI 验证要分别记录。

本次 Debug 构建及官方 IL/Burst 后处理通过，零警告、零错误；离线行为检查通过。
检测到 Cities2 进程已退出后，使用项目 DeployWIP 目标部署到本地 Mods/SunGlasses，
安装 DLL 与构建产物的 SHA256 一致。新调试控件尚未在重新启动的游戏中验证。
