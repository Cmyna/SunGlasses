# 后处理前 HDR 渲染静态检查

## 剩余链路实测补充：2026-10-12

当前暂停场景、相机与时间未改变。完成原生GBuffer/浮点纹理GPU读回，以及固定9.302704 EV的Albedo、间接漫反射、镜面和Color AOV。临时高优先级Volume请求关闭普通雾，取得NoFogColor。SSR关闭的AOV回调超时；单独重试同样超时，第二次连基线Color都未取得。因此没有SSR关闭的有效因果对照，也不能把回调失败归咎于SSR。

### 实际绑定与曝光

- ShaderVariablesGlobal常量缓冲实际值：_IndirectDiffuseLightingMultiplier=2.05、_ProbeExposureScale=1、_ReplaceDiffuseForIndirect=0、_IndirectDiffuseMode=0、_ReflectionLightingMultiplier=1、_EnableRayTracedReflections=0。
- 实际雾常量：_FogEnabled=1、_PBRFogEnabled=0、_EnableVolumetricFog=0、_HeightFogBaseExtinction=0.0001515152（对应1/6600）。
- frame97869的当前ExposureTexture读回(0.001319550,9.302704)，上一帧(0.001319533,9.302722)。乘数差异约0.0013%，当前快照未看到明显历史曝光错配。单一稳定时刻不能排除曝光变化时的错配。
- VT运行配置：DefaultPVTStack四层依次RGBA_BC7_SRGB、RGBA_BC7_UNorm、RGBA_BC7_UNorm、RGBA_BC7_UNorm；ExtendedPVTStack首层RGBA_BC7_SRGB。BC7 sRGB条目9595，UNorm条目21978，待加载材质0。这支持颜色层与非颜色层分别配置，但配置不能替代物理GPU资源视图追踪。

### 当前公寓表面结果

每个表面先用Albedo预览选择内部多边形，白墙再筛选线性亮度0.48–0.8，屋顶筛选0.015–0.2。逐像素统计中位数，位置见remaining-aov/sample-regions.png。未采集AO诊断、未切换AO。

| 表面 | 像素数 | Albedo亮度中位数 | 雾开启相对无雾Color变化 | 镜面AOV/Color | GBuffer解码与Albedo RGB最大通道差中位数 |
|---|---:|---:|---:|---:|---:|
| 公寓正面白墙 | 8267 | 0.6015 | -3.49% | 0.76% | 0.00489 |
| 公寓侧面白墙 | 5330 | 0.6226 | -3.24% | 1.88% | 0.00322 |
| 公寓深色屋顶 | 1981 | 0.09867 | +1.38% | 14.55% | 0.000824 |

普通雾在这些样本中降低白墙亮度、提高深色屋顶亮度，方向是缩小此处明暗差异，不支持“这段雾把屋顶压暗”。本轮NoFogColor缺少其后的RestoredColor成功捕获，无法用恢复前后差异估计完整时域误差；采集记录确认相机、时间、固定曝光相同，但每张记录未另外保存有效Fog.enabled读回。结论限于这一临时Volume对照及其幅度，不是全场景证明。

GBuffer0使用不指定目标格式的原生RGBA8读回，离线按sRGB解码；Albedo使用RGBAFloat诊断。两者来自不同帧，存在TAA抖动、遮罩边界、8bit量化和诊断pass差异。正面白墙第90百分位RGB差可到0.0503，因此不能声称所有像素逐点一致。深色屋顶差中位数约0.000824、90百分位约0.00389，未看到大幅重复解码导致的整体暗化。也不能排除贴图进入实际Albedo之前，两个pass共同使用的错误处理。

实际GBuffer3、ColorPyramid、SSR浮点读回均未发现NaN或Inf；NaNKiller不是当前这些有效值异常压暗的证据。此检查无法排除更早出现异常并已被清零的像素。

镜面AOV包括多个镜面贡献，不等于纯SSR。白墙镜面贡献小，SSR作为其主要亮度根因的优先级降低；深色屋顶镜面贡献约14.6%，SSR/探针/天空镜面合成仍有检查价值，尚未识别是哪种贡献。

### 更新后的优先级

1. Deferred漫反射与实际2.05倍间接光增益，以及贴图/VT到Albedo的共同上游处理。
2. 深色屋顶的SSR、探针与天空镜面分量；需要有效分离测量，当前没有关闭SSR因果结果。
3. 预曝光在变化或历史重投影情况下的处理；稳定时刻未看到大幅错配。

普通雾、GBuffer0写入/读取颜色转换、当前稳定曝光错配作为本次公寓屋顶压暗根因的优先级下降，均不是全局排除。AO继续按用户要求留在本轮范围之外。

### 清理和证据

两次AOV辅助采集均自动恢复原aovRequests、销毁临时GameObject和Volume/RenderTexture，最后回读Exposure=AutomaticHistogram、Fog=True、SSR=True、临时GameObject不存在。没有修改建筑染色、相机、AO、用户设置或存档。辅助程序集仍留在进程内；剩余异步读回已Complete，没有MonoBehaviour/回调持续挂载。

obj/render-audit/remaining-live/readback.txt和纹理文件记录原生GPU读回；remaining-aov记录5组成功浮点捕获及首轮error.txt；ssr-retry/error.txt记录重试失败；remaining-analysis.json记录统计。RemainingProbe.cs、RemainingAovProbe.cs、SSRRetryProbe.cs及生成/分析脚本保留复现来源。

## 2026-10-12 当前游戏回读：排除AO后的候选范围

本节优先于下文早先的静态候选清单。通过Unity调试接口只读回读主摄像机，frame=78297，Main Camera，position=(-756.31,586.77,-492.98)，1920×1080，Deferred，TemporalAntialiasing。不同调用之间游戏继续运行，因此是当前会话状态检查，不是同一冻结帧的GPU执行追踪。AO按用户要求暂不纳入排查，没有关闭AO或改动其参数。

### 已从本次候选中移除

- RenderGuard额外sRGB解码：设置False、状态Off；23个默认材质使用BH/SG_DefaultShader，未发现实验shader材质。
- gamma Blit：确实加载并属于Colossal.UI.UIBlitPass，但活动注入点为AfterPostProcess。Colossal.UI.dll的Execute把结果绘制到m_UserBackground，再UpdateUserBackground；不是本次要找的后处理前主颜色写入。
- Outlines Pass和DebugBlit：活动注入点也是AfterPostProcess。
- 体积云：FrameSettings允许，但有效Volume.enable=False；不能用材质仍加载判断pass启用。
- SSGI：FrameSettings允许，但有效GlobalIllumination.enable=False，_IndirectDiffuseTexture是1×1黑色占位Texture2DArray。
- 体积雾：FrameSettings允许Volumetrics，但Fog.enableVolumetricFog=False；普通大气雾仍开启。
- 光线追踪：有效FrameSettings.RayTracing=False。
- Tunnel Pass：注册在BeforePostProcess且enabled=True，但UndergroundViewSystem的tunnelsOn、pipelinesOn、subPipelinesOn、contourLinesOn全部False。Game.dll Execute的IL_0009–003d检查这四项并直接返回，当前不会绘制/合成地下颜色。
- Depth Fade Pass直接改色：当前TAA jitter=True、DEPTH_FADE_FROM_TEXTURE=True，pass运行；但Execute只向线性深度和历史深度目标dispatch，然后绑定_GlobalDepthFadeTex。没有读取/写入cameraColorBuffer。它可能间接影响消费深度淡出的特效，不是已发现的全局材质颜色压暗pass。
- 当前诊断覆盖：m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled()=False；摄像机AOV collection内部m_AOVRequestData=null，无活动AOV测量列表。

### 仍保留，按排查价值排序

| 候选 | 当前依据 | 能解释什么、还缺什么 |
|---|---|---|
| 默认材质VT/GBuffer0 | 原shader正在使用；实际GBuffer0=R8G8B8A8_SRGB | 核对实际采样资源视图和GBuffer线性读回；格式匹配不足以排除VT绑定错误 |
| Deferred间接光及预曝光 | Deferred模式；有效IndirectLightingController.indirectDiffuseLightingMultiplier=2.05；自动直方图曝光开启 | 统一2.05倍可在后处理前放大间接漫反射，明暗比例测量通常会抵消这个因子；不是深色专属衰减的证据 |
| 普通雾/OpaqueAtmosphericScattering | AtmosphericScattering=True，Fog.enabled=True；meanFreePath=6600，baseHeight=0，maximumHeight=8000，SkyColor模式 | 后处理前改变整幅HDR颜色；需比较雾前后像素，判断透射和散射贡献 |
| SSR及间接镜面合成 | SSR=True、有效ScreenSpaceReflection.enabled=True、Approximation算法，实际SSR_Lighting_Texture为RGBA16F | 当前实际启用；主要作用于镜面分量，是否影响目标墙面需按该分量贡献确定优先级 |
| 直接光、天空/探针及光照合成 | ReflectionProbe/SkyReflection允许，间接镜面乘数=1，reflectionProbeIntensityMultiplier=1 | 是基线链路，仍需实际像素分量检查；“代码存在”不代表已证明异常 |

当前曝光是AutomaticHistogram、CenterWeighted、Progressive；limitMin=9.286116、limitMax=10.28612。这不是当前实际EV的GPU读回，fixedExposure=6.324944在自动模式下不是有效固定曝光值。后续定量比较应冻结曝光或核对真实ExposureTexture；仍坚持排查范围在RenderPostProcess之前。

实际缓冲回读：GBuffer1=R8G8B8A8_UNorm，GBuffer2=R8G8B8A8_UNorm，GBuffer3和ColorPyramid=B10G11R11_UFloatPack32；SSR=R16G16B16A16_SFloat；当前/上一帧ExposureTexture=R32G32_SFloat。Shader.GetGlobalTexture显示的是回读时最后绑定状态，并非完整GPU frame capture。

证据：obj/render-audit/live-state.txt、LivePassProbe.cs、Game.il.txt中的DepthFadePass/UndergroundPass、Colossal.UI.il.txt中的UIBlitPass；有效间接光乘数、地下开关、AOV与debug状态由额外只读eval核对。Shader.GetGlobalFloat同名字段可能位于常量缓冲而返回0，这类API默认值没有用于推断实际光照参数。

本轮未修改摄像机、Volume、材质、AO、建筑、设置或存档。读取辅助程序集已加载到当前进程，没有创建MonoBehaviour或回调。以上收敛仍是状态与IL证据，不是确认根因。

日期：2026-10-12。问题：是否有某个 pass 错误提亮或压暗深色材质，且发生在后处理之前。

## 结论与边界

确实存在能改变明暗关系的前置运算，但本轮未确认正常游戏画面的一个错误 pass。不能把“发现非线性运算”直接等同于“发现渲染缺陷”。

本轮直接读取本机安装版 resources.assets、globalgamemanagers.assets、sharedassets0.assets 和 Unity.RenderPipelines.HighDefinition.Runtime.dll/Game.dll。未启动或修改游戏，未切换材质或模组，未部署。setup.md 没有登记完整反编译源码，因此使用只读 Cecil IL 和微软 D3DDisassemble；没有创建整套游戏反编译工程。

枚举到471个Shader/ComputeShader对象；对选定主要链路导出569份D3D11反汇编。默认材质全部336个GBuffer片元变体导出并检查RGB写出形态；其他pass/kernel以代表程序人工检查、变体元数据/反汇编结构扫描为主。不是569个程序都完成逐指令语义证明，更不覆盖所有资产包、平台、LOD、材质和运行时关键字组合。

## 安装版中的调度链

HDRenderPipeline.RecordRenderGraph 的IL确认以下主要调用顺序，实际执行受分支、FrameSettings、Volume、RenderGraph依赖和CustomPass启用状态约束：

1. 创建颜色、漫反射和SSS缓冲，RenderPrepass（含GBuffer）。
2. AO、contact shadows、SSR、screen-space indirect diffuse、screen-space shadows、volumetric lighting。
3. Deferred lighting、Forward opaque、subsurface scattering。
4. RenderSky（其实现调用opaque atmospheric scattering）、云、custom pass、透明物体、另一个云hook。
5. color pyramid、distortion、调试/accumulation、custom pass等。
6. RenderPostProcess。

所以“后处理前Color”并不等于“刚完成BRDF的光照”。雾、云、透明合成和custom pass也可能已经写入该颜色。曝光排除指排除自动曝光变化；HDRP仍可能把预曝光缩放嵌入光照和缓冲读写，不能把曝光运算从数据流中删除。

证据：obj/render-audit/method-RecordRenderGraph.txt、method-CreateSSSBuffer.txt、Unity.RenderPipelines.HighDefinition.Runtime.il.txt。

## 各环节检查

| 环节 | 实际看到的运算 | 判断 |
|---|---|---|
| 默认材质GBuffer0 RGB | 336个变体中168个mov、168个mul；写出阶段没有mov_sat/mul_sat/mad | 未发现写出点统一再做gamma、开方或黑色阈值；不等于上游所有颜色计算正确 |
| GBuffer格式 | CreateSSSBuffer使用GraphicsFormat=4，即R8G8B8A8_SRGB；它被注册为GBuffer MRT0 | shader写出为线性值，硬件颜色格式参与编码/解码；不能仅凭sample指令证明颜色空间正确 |
| GBuffer2 RGB | 默认材质中可见pow(1/2.4)、1.055和-0.055；Deferred对GBuffer2执行0.305306/0.682171/0.012523多项式解码 | 对应镜面颜色参数编码/解码，不能误判成GBuffer0漫反射被提亮；低值近似误差仍应单独评估 |
| Deferred AO | 安装版中确实存在2.0404、-4.7951、2.7552等GTAO多次反弹系数，并分别用于相关光照分量 | 有真实的Albedo相关衰减机制，存在非线性不意味着错误或重复遮蔽 |
| 光照合成/预曝光 | CombineLighting两程序分别直接读回、或乘Exposure；Deferred对部分预曝光输入取逆缩放，末尾施加当前缩放 | 没有在代表合成程序看到统一gamma变换；历史曝光/分支绑定不一致仍不能靠静态代码排除 |
| 雾 | OpaqueAtmosphericScattering输出散射颜色及alpha；渲染位于RenderPostProcess之前 | 会改变HDR颜色，需要结合blend state与输入透射率判断；尚未发现错误符号或错误重复合成的证据 |
| NaNKiller | 检查IEEE NaN/Inf，异常像素整体写零，有限值保留 | 若上游产生NaN/Inf会变黑；不是针对普通深色的black crush阈值，当前未证明发生过异常 |
| Custom Blit | Hidden/BH/CustomPass/Blit对RGB作log→×0.454545→exp，即约abs(c)^(1/2.2) | 明确的非线性候选，但尚未建立它是正常游戏画面当前启用pass的证据 |

## 一个具体的、尚未确认为缺陷的候选

Hidden/BH/CustomPass/Blit 的 Custom Pass 0-3.asm 第54–56行：

```text
log r0.xyz, |r1.xyzx|
mul r0.xyz, r0.xyzx, l(0.454545, ...)
exp o0.xyz, r0.xyzx
```

对0–1正值会提亮，尤其低值；并不直接支持“它压暗深色”。若把输出作为线性HDR再次参与光照/合成，会破坏亮度关系；若用于显示编码或特殊界面，可能有合理用途。

静态布局显示它可读取_ColorPyramidTexture或_AfterPostProcessColorBuffer，按_CustomPassInjectionPoint分支选择，输出采用SrcAlpha/OneMinusSrcAlpha混合。这使注入点、目标缓冲格式和调用者成为关键。本轮Game.dll顶层方法字面量扫描未找到该shader名称，resources.assets的197个Material也未发现本文件内直接指向shader617的引用。这不排除外部包、序列化引用、动态创建或其他程序集引用，只说明不能把该程序的存在当作正常摄像机执行证据。

## AO如何让深色相对更暗

可识别的多次反弹近似为：

```text
a = 2.0404 * albedo - 0.3324
b = -4.7951 * albedo + 0.6417
c = 2.7552 * albedo + 0.6903
M = max(x, ((x*a + b)*x + c)*x)
```

x为可见度。在x=0.5时，albedo=0.02、0.2、0.6对应M约0.5、0.5508、0.72435。深色的反弹补偿较少，所以同样遮蔽下输出/Albedo可能更低。这是数学例子，不是新的游戏测量。实际是否超出合理范围，需要核查输入AO、材质AO、specular occlusion及分量归属，不能因为源码出现两段公式便宣称同一漫反射被重复乘两次。

此前同表面tint测试主要漫反射接近线性，削弱了所测范围内强Albedo相关光照衰减的假设；但无法排除一个在tint之前就固定乘入的错误系数，也无法证明Albedo诊断和实际GBuffer结果一致。

## 仍需验证的断点

- VT页缓存的真实资源视图、纹理颜色空间，以及在绑定时是否与shader预期一致。静态sample缺少硬件SRV状态。
- GBuffer0原始存储与sRGB解码后的实际值。要明确读回方式，避免把原始sRGB字节直接当线性值比较。
- Deferred直接/间接漫反射在AO前后，以及真实AO/材质AO输入。特别补测低Albedo和明显直射光。
- Deferred合成之后、雾/云之后、每个已启用CustomPass之后、RenderPostProcess之前的同像素HDR值。
- 当前/上一帧Exposure比例及实际缓冲格式，确认历史SSGI/SSR分量没有错误缩放。
- 极低值是否发生格式量化、shader clamp或NaN写零；GBuffer3静态格式为B10G11R11_UFloatPack32，不能只看AOV浮点目标就假设整条中间链均为RGBAFloat。

静态分析最适合找到可疑运算和明确检查断点；只能在建立输入、分支、绑定和写入目标后，才足以确认某个pass的颜色错误。本轮没有确认可直接修复的原版缺陷。

## 复现资料

obj/render-audit/inventory.py、extract.py、read-il.ps1、split-il.py为本轮只读提取工具；inventory.json、programs.json、hashes.json记录对象、覆盖范围和安装资源SHA256。反汇编、布局和IL均保存在同目录，不上传游戏资源。extract.py不修改安装文件、不生成替换shader。
