# 设置与运行状态回归

使用 .NET 10 执行，不需要启动游戏或安装测试框架：

```powershell
dotnet run --project Tests/SettingsBehavior
dotnet run --project Tests/SettingsBehavior -p:OmitRealisticCurve=true -- --missing-curve
```

项目直接链接正式的 Mod、Setting、光照、大气、曝光、调试控件、曲线源码及内置 JSON。
通过反射修改正式 Setting，跳过 Apply，推进更新后检查写入灯光和 Volume 的结果。
另覆盖默认实例隔离、曲线缓存、临时覆盖退出、恢复默认、场景资源重建、停止恢复、
卸载及同 World 重新加载。第二条命令有意不嵌入 realistic 资源，检查加载失败、
重复更新不重试或刷日志、改选有效预设恢复。

Unity/Game 类型由最小替身提供。原生对象的销毁时序、HDRP 相机混合、实际磁盘保存、
真实界面的展示与输入交互、独立镜面太阳不在本测试范围内；这些需要游戏内验证。
ApplyAndSave 替身只记录调用，不将测试配置写入玩家文件。
