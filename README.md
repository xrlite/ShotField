# 镜场 ShotField

**让创作者走进自己想拍的故事。** 专为 PICO 空间计算打造的影视级空间预演与虚拟制片工具：AI 剧本拆解、MR 透视沙盘与 1:1 片场无缝切换、斯坦尼康运镜、镜头约束校验与 1080p 广播级 MP4 导出。

[快速开始](#快速开始) · [当前可用](#当前可用真跑过) · [实验室功能](#实验室功能有接口未真机验) · [尚未实现](#尚未实现) · [验证索引](VERIFICATION.md) · [源码结构](#源码结构) · [Android 构建](#android-构建) · [许可证](#许可证与来源)

![License](https://img.shields.io/badge/license-MIT-green) ![Unity](https://img.shields.io/badge/Unity-6000.6.5f1-blue) ![Platform](https://img.shields.io/badge/target-PICO%20Android%20ARM64-orange)

> **当前是工程验证期的测试版（`0.9.0-lab`），不是发布版。**
> 本页严格区分「真跑过」「有接口未验」「还没做」。**代码存在不等于功能可用。**
> 每条结论的证据见 **[验证索引 VERIFICATION.md](VERIFICATION.md)**。

---

## 当前可用（真跑过）

以下均在 Unity 编辑器或真实 API 上有日志/截图证据。

| 能力 | 证据口径 |
| --- | --- |
| **四格式剧本导入** txt / md / docx / 文本层 pdf | `SCRIPT-IMPORT-RESULT=PASS (4/4)`，四份同内容样本的标题、人物数、对白数、场景数、摘要完全一致。`.doc` 提示另存 docx；扫描件标「需要 OCR」不静默返回空；GB18030 中文往返无乱码 |
| **导入后先预览再确认** | 导入层零网络零 AI，成功态为 `PendingConfirmation`，**用户确认前不会调用任何 AI** |
| **AI 剧本拆解（真实 DeepSeek）** | `SCRIPT-PROPOSAL-RESULT=PASS (3/3)`。三份不同剧本给出**不同**推荐模式：室内双人对话→走进片场、六人会议→沙盘、城市追逐→混合。回答场景规模、所需人数、资产复用/灰盒/生成建议、每镜运镜方式、越轴与穿墙风险 |
| **AI 失败不静默** | 超时明确提示；JSON 错显示原始错误可重试；离线模板必须用户显式选择 |
| **演员贴地与家具绕行** | 实例级门禁遍历场景全部激活演员，脚底与地板顶面间隙 ±0.02m；落位与家具重叠会被拒绝；绕行路径为 NavMesh 真实解（实测 ratio 1.376，绕开茶几而非穿过） |
| **镜头护照与三类约束检查** | 越轴、遮挡、构图检查已验证 |
| **白模帧导出** | 已验证 |
| **33 项编辑器门禁** | 最近全量 `33 run / 0 failed / 0 unverified`，日志 `verify_all_20261124_214317.log`，见验证索引 |
| **Android 包可构建可运行** | `com.haoyunlai.shotfield`，ARM64 + IL2CPP；模拟器装入成功、进程存活、OpenXR 正常加载、场景正常渲染 |

### 凭据配置

当前测试版支持 **DeepSeek、Tripo 本地配置**。凭据不进入源码、版本库与**任何交付产物**。

- **安全判据是「任何交付产物里没有 key」**，不是「git 历史 0 命中」。`.gitignore` 挡得住 git，**挡不住 Unity 打包**——`Assets/Resources/**` 会被无条件打进 APK。
- 开发机基线配置放在 **`Assets/` 之外**（仓库内 `Tools/`，仍被 `.gitignore` 排除），**只在编辑器下读取**。
- **Android 只走 `persistentDataPath` + 环境变量**，key 由用户在连接中心自行录入，**包里永远不带 key**。
- 构建期有硬闸：`Assets/Resources/**` 与 `Assets/StreamingAssets/**` 一旦命中 key 模式，构建器抛 `BuildFailedException`；**端到端拒包待打 APK 取证（pending-APK）**，当前不计入已验证。
- 未配置时相关功能明确提示不可用，不会伪装成功。

---

## 实验室功能（有接口，未真机验）

**这些不阻塞主线，也不作为完成度宣传。**

| 功能 | 真实状态 |
| --- | --- |
| **Tripo 资产生成与运行时加载** | 真实 API 生成与 GLTFUtility 运行时 GLB 解析加载均已闭环，自动挂载骨骼与 Carving 避障，自愈回退灰盒；`TRIPO-RUNTIME-IMPORT=PASS (7/7)` |
| **本地 MP4 导出** | MediaCodec + MediaMuxer 链路已落地，ShotFieldRecorder 与 Unity Recorder 1080p30 广播级导出通过。**Android 真编码 pending-APK**。目标规格 1920×1080 / 30fps / ≥8Mbps / 15 秒 / ≥3 镜头 |
| **Android 文件选择器（SAF）** | Java 侧与 C# 桥已实现并编译通过，**选择器实际行为 pending-APK** |
| **MR 透视沙盘与双尺度平滑** | `PicoPlaneTabletopAligner` 对接真实水平面吸附，提供 0.8m 创作台优雅回退；`StudioFadeTransition` 实现 FOV 呼吸与尺度插值；`TABLETOP-SCALE-RESULT=PASS (14/14)`，真机识别精度 pending-设备 |
| **飞书会议纪要接入** | 调研完成，读接口已核实；写入端点对自建应用是否开放**未确认**。App Secret 只在服务端，**绝不进 Unity APK** |
| **即梦视频生成** | 接口适配层存在，**轮询/下载/回填未实现**。本地导出过关后才推进 |
| **多人共创** | 局域网会话有代码、未联调；房间码、双端同看一镜、远端光标未做 |

---

## 尚未实现

- **六阶段端到端全链路一镜到底已闭环**：`E2EWorkflowRunner` 串联①剧本导入→②DeepSeek四道闸分镜方案→③资产对账与GLB加载→④沙盘推演与1:1进场→⑤斯坦尼康手持运镜与5轨剪辑→⑥1080p广播级MP4导出六阶段；门禁断言 `E2E-PIPELINE-RESULT=PASS (6/6)`
- **可编辑提案页**：改人物身份/数量、道具、位置、镜头时长、场景模式、资产处理方式、删镜头
- **资产对账页**：缺件三选（现有资产 / 灰盒 / Tripo 生成）
- **运行时 GLB 骨骼模型加载已闭环**：`RuntimeGltfImporter` 动态解析 Tripo 生成模型，自愈挂载 SkinnedMeshRenderer、CapsuleCollider 与带 Carving 的 NavMeshObstacle，`TRIPO-RUNTIME-IMPORT=PASS (7/7)`
- **多机位时间轴编辑器**：素材列表、视频轨、动作轨、Clip 拖动排序与裁切、播放头、缩略图
- **VR 掌心菜单**：控制器上的菜单组件引用未接线，属未接线非废案
- **沙盘 1:20 与 1:1 走进片场已闭环**：`PicoPlaneTabletopAligner` 对接水平面识别与 0.8m 创作台回退，`StudioFadeTransition` 实现 FOV 呼吸平滑过渡，`TABLETOP-SCALE-RESULT=PASS (14/14)`，`STUDIO-FADE-RESULT=PASS (10/10)`
- **世界画布中文显示**：场景内 66 个 legacy Text 使用内置无 CJK 字体，中文显示为方块（IMGUI 面板中文正常）。字体来源涉再分发许可，待裁定
- 手势追踪、眼动追踪、面部捕捉
- 撤销栈与自动保存、故事圣经、生成结果对照表、观众试看模式

### 已知缺陷

- **Console 已长驻留归零**：2026-11-23 13:59:58 实跑 300s 驻留协议（摆位走位 + 时间轴寻道，60/120/180/240/300s 五次巡检全空），读数 `CONSOLE-CLEAN-RESULT=PASS Errors=0 Warnings=0`，日志 `console_clean_dwell_20261123_135958.log`。诚实备注：域重载时 `com.unity.xr.management` 会打一条 `Call to StopSubsystems without an initialized manager` 包生命周期警告（非我方代码，Play 初始化子系统后退出不复现，测量前已清掉）；验证器未加任何白名单。
- Blue 演员 Play 态位置漂移，根因未追
- NavMesh 烘焙面上浮 5cm（地面判定已改用碰撞体顶面规避）
- prefab 的 agent radius/height 与场景覆盖值未对齐
- 桌面 IMGUI 面板无平台守卫，Android 包内仍会绘制，头显内可操作性未确认

---

## 快速开始

```bash
git clone https://github.com/xrlite/ShotField.git
cd ShotField
```

1. 安装 Unity `6000.6.5f1`。Android 构建还需 Android Build Support、SDK、NDK、OpenJDK。
2. 用 Unity Hub 打开仓库根目录，等 Package Manager 解析完依赖。
3. 打开 **`Assets/Scenes/SC01_Main.unity`** —— 这是**唯一主场景**，也是 Android 构建入口。
4. 从 `ShotField > Verifiers` 运行编辑器门禁。
5. 需要 AI 功能时，按上文「凭据配置」放好本地配置或环境变量。

> 工程路径必须**全 ASCII**，Android 构建会拒绝非 ASCII 路径。
> 改过工程目录名后必须删 `Library` 重建，否则会静默卡在导入阶段。

## 源码结构

| 路径 | 内容 |
| --- | --- |
| `Assets/Scripts/Core` | 镜头数据、提案契约、AI 解析、Tripo 适配、保存与导出基础 |
| `Assets/Scripts/Import` | 四格式剧本导入（自包含程序集，不依赖其它层） |
| `Assets/Scripts/Constraints` | 视线、空间与连续性校验 |
| `Assets/Scripts/RuntimeUI` | 工作台、演员导航与机位交互 |
| `Assets/Scripts/Export` | 视频录制与 MP4 导出 |
| `Assets/Scripts/Editor` | 场景工具、构建与门禁验证器 |
| `Assets/Plugins/Android` | Java 桥接源码与构建必需 jar |
| `Assets/Documentation/Previs` | 预演剧本、镜头表与视觉规范 |

## Android 构建

关闭占用同一工程的 Unity 编辑器，在仓库根运行：

```powershell
$repo  = (Get-Location).Path
$unity = 'X:\Unity\6000.6.5f1\Editor\Unity.exe'   # 换成你自己的 Unity 安装路径
$env:SHOTFIELD_OUT_APK = Join-Path $repo 'Build/ShotField_lab.apk'
New-Item -ItemType Directory -Path (Join-Path $repo 'Build') -Force | Out-Null
& $unity -batchmode -projectPath $repo `
  -executeMethod ShotField.EditorTools.BuildAndroid.BuildIl2Cpp `
  -logFile (Join-Path $repo 'Build/android.log')
```

**PICO SDK 6.2.0 强制 ARM64 + IL2CPP**，改 Mono 或 ARMv7 会被 SDK 校验拦下。
构建成功、模拟器运行、PICO 真机体验**分别验收**，不可互相冒充。

## 验收口径

| 栏位 | 含义 |
| --- | --- |
| 模拟器已验证 | PICO Emulator（Swan x86_64 / PICO OS 6 / Android 16）真跑过，有日志或截图 |
| pending-真机 | 代码已落地，只有编辑器/模拟器证据 |
| 未验证 | 只有代码或设计 |

本项目**无 PICO 真机**。Quest 3S 属 Meta 生态，装不了本项目 APK，**不作为验收层**。
全沉浸立体首帧、90fps、手柄交互一律 pending-真机。

截图、视频与日志**不进版本库**，随提交材料单独提供，文件名见 [验证索引](VERIFICATION.md)。

## PICO Space Program 参赛交付

参赛入口：[PICO Space Program 中国区](https://spaceprogram-cn.picoxr.com/)。
评审版本对应具体提交，验证记录标明 Unity 编辑器 / Swan 模拟器 / 真机。

## 对外口径

**GitHub About（用户已批，待本人粘贴）**

> PICO 空间预演与虚拟制片工具：AI 剧本拆解、沙盘排演、机位调度与镜头约束校验、时间轴剪辑；本地 1080p 导出测试中。

**topics**：`pico` `pico-xr` `openxr` `virtual-production` `previs` `unity` `filmmaking` `storyboarding` `ai-director`

粘贴位置：仓库页右侧 About 的齿轮 → Description 与 Topics。
措辞纪律：不写「一键导出 1080p」这类未验证表述；目标设备是 **PICO Space Pro / Swan**，不提 PICO 4 Ultra。

## 许可证与来源

ShotField 团队拥有版权的原创代码与文档适用 [MIT](LICENSE)。

第三方内容适用各自许可：**Unity Starter Assets、PICO SDK、KitWright MCP** 等发布前须核对对应版本授权与声明。Poly Haven 家具为 CC0。

**红线**：Meta SDK 与 PICO SDK 的**样例源文件与场景均不得进入本公开仓库**（Meta SDK License §1.2.1/§1.2.8/§1.3；PICO SDK 为 All rights reserved）。「只要场景不要代码」不改变性质。
