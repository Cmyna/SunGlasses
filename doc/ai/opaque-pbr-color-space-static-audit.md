# 不透明渲染与颜色空间静态核查

日期：2026-10-12。安装版基线：CS2 1.6.2f1 / Unity 2022.3.71f1，D3D11。

## 结论

本轮没有找到默认建筑材质正常渲染路径中“Tint 未解码”“Tint 被重复解码”或“GBuffer0 写出前统一 gamma 提亮”的代码证据。三个建筑染色通道在 `BatchDataSystem.BatchDataJob.UpdateObjectData` 中分别调用 `Color.linear`，随后经实例属性上传和 shader 乘法/混合，能够用静态代码确认线性数值约定，不需要再改色实测。

发现的具体颜色精度缺陷/限制集中在 GBuffer2 的镜面 F0 快速编码与解码：极低值会失真或归零。这是所比较的 Unity HDRP 上游也采用的有损近似，而不是已证明的 CS2 专属修改错误。它可能造成低 F0 材质 Deferred/Forward 的响应差异，不能把它解释为 GBuffer0 漫反射被重复解码。

另有条件性风险：染色遮罩重叠时插值权重可能超过1；外部代码把线性数据写进原本期望 sRGB 的 ColorSet/Color 属性会触发二次解码；VT 的实际纹理内容及 native GPU view 是否与声明一致，无法仅凭 managed IL 证明。没有确认这些条件在当前骨灰堂或公寓资产上构成缺陷。

## 方法与覆盖

本轮不连接、不修改运行中的游戏。只读安装程序集/资源，输出 IL、D3D11 shader 反汇编与数学模型到 `obj/render-audit/static-color-audit`。完整 decompile root 在 setup.md 中为 `(none)`；本轮使用 Mono.Cecil 的定向 IL 提取，不创建整套反编译工程。

新提取的代表程序共113份：23个带 GBuffer pass 的游戏 shader，57个 GBuffer + 56个 Forward。包括默认、基础、曲面、网络、植被、角色、impostor、terrain；水/玻璃/透明类程序存在于清单中，但不把它们等同于正常不透明 draw。代表选择覆盖所存在的低/中/高质量和 VT 开关，优先少关键字；Forward 中一些程序始终编译 DEBUG_DISPLAY 功能，因此同时追运行时调试条件。并非覆盖每个材质、平台、关键字组合或逐指令证明113份程序的全部语义。

复核此前导出的默认 shader 全部336个 GBuffer 片元程序：GBuffer0 RGB 输出点168个 mov、168个 mul。该统计只说明输出点形态，不能证明上游没有非线性。

本次安装资源 SHA256 与此前提取记录一致：

- resources.assets：95242b9385d43bdaae38d797c939e080f8a9768d7f42bd4dc81d5ae74fb106a6
- globalgamemanagers.assets：5aca9280a56b674e104c42f9b4a435fb24b6bbc2650ffb2890941d99c2f931f4
- sharedassets0.assets：6062cb316f61bece2d73c084ff2a3f027df329e9defa09355c492381835b7f8a

程序集哈希、代表程序关键字与路径分别记录于 assembly-hashes.txt、materials/manifest.json。尝试从 UnityPy 可读设置对象查找 m_ColorSpace 与 stacks，没有取得对应序列化配置（serialized-settings.json为空）；没有据此推断配置不存在或配置值。

## 1. 建筑颜色生产与上传：可静态确定三个 Tint 的空间

`VisualCustomizeSection.OnUpdateMeshColor` 把颜色直接写入 `CustomMeshColor.m_ColorSet[channel]`。`ColorSet.set_Item` 只赋值；检查的 MeshColorSystem 颜色选择与拷贝路径没有 RGB get_linear。其 RandomizeAlphas 仅处理 alpha，不能当作 RGB gamma 运算。

决定性转换在 `BatchDataSystem/BatchDataJob.UpdateObjectData`：

```text
IL_0a00: ColorSet.m_Channel0
IL_0a05: Color.get_linear -> V_77
IL_0a13: ColorSet.m_Channel1
IL_0a18: Color.get_linear -> V_78
IL_0a26: ColorSet.m_Channel2
IL_0a2b: Color.get_linear -> V_79
```

随后把 V_77/78/79 分别交给 ObjectProperty=2/3/4 的 UpdateColorMask。枚举特性直接确认映射：

| ObjectProperty | shader 名称 | 类型 |
|---|---|---|
| ColorMask1=2 | colossal_ColorMask0 | float4 |
| ColorMask2=3 | colossal_ColorMask1 | float4 |
| ColorMask3=4 | colossal_ColorMask2 | float4 |

UnityEngine.Color.get_linear 的安装版 IL 分别对 r/g/b 调用 Mathf.GammaToLinearSpace，alpha 原样保留。UpdateColorMask 的立即路径直接 SetPropertyValue；平滑路径把新旧颜色作为 float4 平滑与 clamp，发生在线性转换之后，不再次解码。

`UnsafeBatches.SetPropertyValue<T>` 按属性分配偏移和 instanceIndex 将结构体直接 CopyStructureToPtr。UploadInstancesImpl 使用 MemCpy/AddUpload。提取的 SparseUploader CopyKernel 普通复制分支是 ld_raw/store_raw，不执行 gamma 数学。ManagedBatches.AddBatchRenderer 把 MetadataValue 与其 GraphicsBufferHandle 传给 BatchRendererGroup.AddBatch。

因此正常对象路径的约定为：

```text
ColorSet RGB（期望sRGB）
-> 每通道 Color.linear 一次
-> 线性 float4 实例属性（alpha不转换）
-> 缓冲复制与批次元数据
-> shader 读取 Tint RGB 并直接乘、lerp
```

这条代码证据覆盖第三、第二、第一通道；第二通道无需非白色 GPU 实验。此前 GPU 缓冲回读是附加一致性证据，不是本次推论前提。Native renderer 的某次具体 draw 绑定仍不是本次的帧捕获证明。

证据：Game-Game.UI.InGame.VisualCustomizeSection.il.txt；Game-Game.Rendering.ColorSet.il.txt；Game-Game.Rendering.MeshColorSystem*.il.txt；Game-Game.Rendering.BatchDataSystem_BatchDataJob.il.txt 的 IL_09a8–0a85 与两个 UpdateColorMask overload；object-property-mapping.txt；UnityEngine.CoreModule-UnityEngine.Color.il.txt；Colossal.Core-Colossal.Rendering.UnsafeBatches_4.il.txt；Colossal.Core-Colossal.Rendering.ManagedBatches_1.il.txt；SparseUploader-CopyKernel-0.asm。

## 2. 材质默认颜色：Color 与 Vector 不能混为一谈

`ManagedBatches.DefaultValueFromMaterial` 对 Color 类型属性检查 QualitySettings.activeColorSpace；Linear 分支对从 MaterialPropertyBlock、SurfaceAsset colors 字典或 Material.GetColor 取得的值调用 Color.linear，然后写入默认值。Vector 路径不进行这项颜色转换。

这与建筑实例 ColorMask 更新是两条数据入口：不能因为两处都看到 `.linear` 就认定同一个实例值被转换两次。默认属性复制与实例覆盖也不能错误地理解为两个颜色因子相乘。

API 风险：外部 mod 若预先把颜色变成线性，再写入期望 sRGB 的 MeshColor/CustomMeshColor 或被上述代码作为 Color 处理的默认属性，会再转一次。反之，直接写 GPU 实例数据则需要传线性值。参数叫 Color、Vector 或 tint，不足以决定其空间；要遵守具体入口的约定。

证据：Colossal.Core-Colossal.Rendering.ManagedBatches_1.il.txt 的 DefaultValueFromMaterial 两个 overload，尤其 IL_01cb–025c 和 IL_0127–0189。

## 3. 纹理与 VT：代码保留格式，仍存在资产内容边界

TextureAsset.CreateTexture 使用资产保存的 m_Format 创建 Texture2D/Texture2DArray；LoadRawTextureData/SetPixelData 上传字节。本段不是“先手动解码，再用sRGB资源上传”的颜色变换路径。

VTProceduralCPU 创建两种 GPU cache：GraphicsFormat=108（RGBA_BC7_SRGB）与109（RGBA_BC7_UNorm）；CreateStack 把 StackData.layerFormats 直接交给 Procedural.CreationParameters.layers，然后创建 CPUTextureStack。颜色与非颜色层具有分开的格式支持。没有在这段 managed 代码找到把所有层强制改为 UNorm 或 sRGB 的逻辑。

需要区分三件事：资产字节采用什么编码、声明/创建格式是什么、native Unity VT 最终采用什么资源视图。前两者可以继续静态核查具体资产和配置；最后一层涉及引擎native实现，managed IL不能自动补全证明。此前的资源回读显示公寓颜色为BC7_SRGB、ControlMask为BC7_UNorm，VT颜色层也是SRGB，但本轮没有把旧快照冒称为当前GPU帧捕获。

非颜色数据（遮罩、法线、metallic、smoothness、AO）正常应保持数值读取。格式是 UNorm 只意味着无自动sRGB解码，不保证内容已经线性；sRGB格式也不能纠正错误预编码的字节。

## 4. 材质生成与 GBuffer

默认材质普通路径采样颜色、乘基础色、混合三组染色，再处理雪/信息视图/decal。普通非VT代表 GBuffer-2645：雪前 baseColor 有 saturate；decal颜色执行已加权的加法；随后 diffuse=base*(1-metallic)、F0=lerp(.04,base,metallic)。这符合常规 metallic 参数化的结构，不是逐点证明全部BRDF能量守恒。

Tint RGB 在这段运算中不再手动 gamma 解码。Tint alpha 也是数值数据，Color.linear 保留它；不能对整个float4都套pow。

染色遮罩的真实权重是 w1=(r+g)*g、w2=b*(r+g+b)，最终混合强度min(r+g+b,1)。当RGB非负且sum<=1时，相关lerp是凸组合；遮罩重叠较强时w1/w2可能>1，产生外插，随后saturate可能压掉细节。数学反例：mask=(1,1,0)、Tint0=1、Tint1=0，第一次染色插值会得到-base。这是明确的输入域风险，不是已确认的原版资产缺陷。离线检查此前导出的骨灰堂完整遮罩：16,777,216像素中465个sum>1.001，但没有w1或w2>1.001的像素；不能凭sum略>1就宣称异常。

GBuffer0 创建为 R8G8B8A8_SRGB，shader提供线性diffuseColor；Deferred按sRGB读约定得到线性数据。代表读取段没有对 GBuffer0 再执行手动 gamma。GBuffer1/2 保存数值或显式编码的参数，不能按GBuffer0规则统一再解码。

GBuffer3 保存预曝光的烘焙/间接漫反射与自发光等贡献，存在特殊AO标记；不是原始albedo。半浮点/无符号浮点缓冲与8bit颜色缓冲也有精度和动态范围边界。

证据：571-GBuffer-2645.asm，materials/BH_SG_DefaultShader/GBuffer-*、materials/BH_Terrain_SG_TerrainCBT/GBuffer-82.asm，method-CreateSSSBuffer.txt，Deferred_Direct_Fptl-0.asm与bindings.json。特殊SSS、透射、植被与impostor编码应按feature flag解释，不能把所有GBuffer0统一称为diffuseColor。

## 5. 具体精度问题：GBuffer2 镜面 F0 编码

检查的普通 GBuffer 分支写出：

```text
e = saturate(1.055 * F0^(1/2.4) - 0.055)
```

Deferred 对其执行：

```text
F0' = e * (e * (e*0.305306011 + 0.682171111) + 0.012522878)
```

编码没有精确sRGB低值线性段，解码使用多项式近似，加上8bit量化，所以它们不是严格互逆。F0<=约0.000833805时编码直接归零。按round-to-nearest的8bit量化数学模型：

| 输入线性F0 | 解码近似 | 相对误差 |
|---|---:|---:|
| 0.001 | 0.00005962 | -94.04% |
| 0.005 | 0.00354684 | -29.06% |
| 0.02 | 0.01896415 | -5.18% |
| 0.04 | 0.03888315 | -2.79% |
| 0.1 | 0.10044949 | +0.45% |

这是数学重建，不是新GPU测量；具体边界受量化规则和shader浮点精度影响。普通非金属F0通常在本段为0.04，因此不应把0.001例子的94%误差套到所有深色非金属表面。低反射率金属或特殊镜面颜色更值得关注；正常Forward使用线性F0直接着色，不经过这对GBuffer2存储近似，因此可能出现小或低值较大的路径差异。

Unity 2022.3/staging 的 [Lit.hlsl](https://raw.githubusercontent.com/Unity-Technologies/Graphics/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl) 与 [Color.hlsl](https://raw.githubusercontent.com/Unity-Technologies/Graphics/2022.3/staging/Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl)采用同类快速编码/解码。这是上游参照；游戏结论仍来自安装版指令，不能把上游代码当作安装版完整源码。

GBuffer0自身也会量化极暗漫反射；它的sRGB低值步长约为1/(255*12.92)=0.0003035。这是存储精度限制，不是第二次sRGB解码。

## 6. Deferred/Forward光照与HDR合成

安装版 HDRenderPipeline.SetRenderingFeatures 明确开启 GraphicsSettings.lightsUseLinearIntensity 和 lightsUseColorTemperature。灯光颜色的native VisibleLight.finalColor生产过程不是纯managed IL能完全展开的部分；不能据此逐一认证所有灯光资产的单位/光谱真实性。

Deferred的代表程序包含普通材质颜色分离、灯光/阴影、镜面反射、间接光、多次反弹AO、SSS条件分支和预曝光。SSS的sqrt(albedo)不能套到普通建筑。AO多次反弹是颜色相关近似补偿；它不是sRGB变换，也不能只数两组公式就宣称同一分量被遮蔽两次。镜面GGX等也包含sqrt、log、exp，指令存在本身不能证明gamma错误。

GBuffer3、SSR、SSGI等预曝光输入按各分支约定读取/反缩放，末端施加当前曝光；CombineLighting代表程序分别为直接读取或乘曝光。检查的合成写出没有统一对最终HDR RGB做gamma。历史帧错配、任意分支资源绑定仍不能由少数代表程序排除。

Forward-3241 的2.4指数解码位于调试材质输出分支，对收集出的debug颜色作可选转换，然后输出；后续正常光照分支受结果标志控制。不能把这里的sRGBToLinear认成所有Forward建筑albedo的第二次解码。113份清单的指数扫描只用于定位，正常分支人工核查重点是默认建筑与相关编码；不声称每份Forward的每个分支都被完整证明。

普通大气雾是后处理前的线性散射/透射合成；它会改变颜色，并不自动违反PBR。Custom Blit的pow(1/2.2)若插入线性HDR光照链会产生真实错误，但此前调用者IL定位到UI AfterPostProcess，不能因为shader在资源包中就将它列为正常建筑PBR缺陷。当前本轮没有重新读取任何运行时注入点。

ACES、contrast、saturation与显示编码属于光照后的图像处理。它们可以扩大明暗差距或改变色相，但这种非线性不能直接证明材质BRDF计算错误。间接光乘数等艺术参数改变有效照明标定，也不等价于对单次反射BRDF作非法gamma。

## 仍无法静态排除的具体边界

1. 全部资产及外部导入器的颜色贴图/遮罩/法线内容与声明格式是否一致；本轮没有检查全资产库。
2. Unity native VT页缓存/SRV/RTV与shader声明的最终配套，以及驱动行为。
3. 第三方mod或CustomPass绕开上述入口，直接写错空间或替换shader。
4. 所有Deferred tile kernel、Forward特殊材质、LOD和关键字组合的语义，以及动态曝光历史与资源重绑定。
5. 绝对能量标定：太阳/天空/探针/材质反射率、GI增益与艺术覆盖，需要物理参考或分量测量，不能只凭存在GGX代码认证。

## 建议

不据此对默认建筑GBuffer0统一追加sRGB解码。若继续修复实验，应优先把具体资产格式/内容不匹配或明确的参数入口错误作为目标；若研究GBuffer2低F0误差，需要配套改写编码与解码，而不能只改一端。任何修改之前先限定材质分支及影响范围。

本轮仅新增核查文档与obj内证据/提取脚本，未修改或部署mod、未改shader资源、未操作游戏。
