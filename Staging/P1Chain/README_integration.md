# P1 离线工作流链 + 剪映式时间轴 · 暂存集成说明

> 暂存目录：`F:\pico\04_Project\ShotField\Staging\P1Chain\`
> 状态：**全部为新文件，未放入 Assets/，未编译验证（编译 pending-集成）**。
> 离线链已串通：剧本 mock 解析 → 提案确认 UI → 场景应用 → 时间轴分段 → 导出 JSON 清单。
> 在线段（DeepSeek 解析、即梦/Seedance 成片）接口壳子已搭好；未配置 key 时明确报「未配置」，不造假结果。
> 效率数字：**未验证**（本阶段无实测耗时）。

---

## 1. 文件清单与集成去向

| 暂存路径（绝对） | 集成后目标 | 命名空间 | 职责 |
|---|---|---|---|
| `Staging\P1Chain\Core\ProposalDtos.cs` | `Assets/Scripts/Core/ProposalDtos.cs` | `ShotField.Core` | 提案条目/整份提案 DTO，位置复用 `Vector3Dto` |
| `Staging\P1Chain\Core\ProposalModel.cs` | `Assets/Scripts/Core/ProposalModel.cs` | `ShotField.Core` | `ScriptAnalysisResult` → `ChainProposalDto` 纯数据转换；缺资产核对 |
| `Staging\P1Chain\Core\TimelineClipModel.cs` | `Assets/Scripts/Core/TimelineClipModel.cs` | `ShotField.Core` | 剪映式 clip 列表：拼接/排序/删段/按时间定位分段 |
| `Staging\P1Chain\Core\ChainExporter.cs` | `Assets/Scripts/Core/ChainExporter.cs` | `ShotField.Core` | 导出清单 DTO + JSON 落盘（复用 Saves 目录口径） |
| `Staging\P1Chain\Adapters\IVideoGenerationAdapter.cs` | `Assets/Scripts/Core/Adapters/`（并入 Core asmdef） | `ShotField.Core` | 视频生成适配器抽象，请求/结果复用 Seedance DTO |
| `Staging\P1Chain\Adapters\SeedanceVideoAdapter.cs` | `Assets/Scripts/Core/Adapters/`（并入 Core asmdef） | `ShotField.Core` | Seedance/即梦同系可换端点适配器，组合复用 `SeedanceBridge` |
| `Staging\P1Chain\RuntimeUI\ProposalConfirmWindow.cs` | `Assets/Scripts/RuntimeUI/ProposalConfirmWindow.cs` | `ShotField.RuntimeUI` | 桌面 IMGUI 独立确认窗口，逐条接受/拒绝/改参数 |
| `Staging\P1Chain\RuntimeUI\ChainOrchestrator.cs` | `Assets/Scripts/RuntimeUI/ChainOrchestrator.cs` | `ShotField.RuntimeUI` | 离线链编排器：解析→提案→落场景→时间轴→导出 |
| `Staging\P1Chain\RuntimeUI\ChainApplyReport.cs` | `Assets/Scripts/RuntimeUI/ChainApplyReport.cs` | `ShotField.RuntimeUI` | 一轮应用结果报告 DTO（计数 + 成功/失败/缺资产清单） |
| `Staging\P1Chain\RuntimeUI\ChainTimelineController.cs` | `Assets/Scripts/RuntimeUI/ChainTimelineController.cs` | `ShotField.RuntimeUI` | 分段模型衔接 `DirectorTimeline` 的播放/寻道（新衔接文件） |
| `Staging\P1Chain\RuntimeUI\MonitorScaler.cs` | `Assets/Scripts/RuntimeUI/MonitorScaler.cs` | `ShotField.RuntimeUI` | 监看板独立缩放模块 |
| `Staging\P1Chain\RuntimeUI\ActorWalkHaptics.cs` | `Assets/Scripts/RuntimeUI/ActorWalkHaptics.cs` | `ShotField.RuntimeUI` | 走位手柄震动独立模块 |

行数预算：单文件均 ≤200 行（实测见 CHANGELOG 登记行）。

## 2. 离线链每环输入 / 输出

| 环 | 输入 | 处理 | 输出 |
|---|---|---|---|
| ① 剧本解析 | 剧本文本 `rawScript` | `AiScriptParser{ IsOfflineMock=true }.ParseScriptAsync(rawScript)` | `ScriptAnalysisResult`（场次/人物/拍节 beats） |
| ② 提案构建 | 上一步结果 + 原始文本 | `ProposalModel.BuildFromAnalysis`：每拍节生成 Shot + Actor 条目，道具关键词命中生成 Prop 条目；对照 `PropLibrary` 登记缺资产 | `ChainProposalDto`（items + missingAssets） |
| ③ 提案确认 | `ChainProposalDto` | `ProposalConfirmWindow` 逐条处置：接受/拒绝/改时长/写备注，状态回写 DTO | 条目 `decision = Accepted / Rejected / Edited / Pending` |
| ④ 场景应用 | 已接受条目 | 编排器经既有公开 API 落场景：道具 `SpawnProp`、演员 `TryPlace`、机位 `CameraPreset.Apply` | `ChainApplyReport`：成功清单 + 失败清单 + 缺资产清单 |
| ⑤ 时间轴分段 | 已确认提案 | `TimelineChainModel.FromProposal` 成段；`ChainTimelineController` 绑定既有 `DirectorTimeline` | clip 列表（机位+时长+演员动作引用），逐段寻道 |
| ⑥ 导出包 | 分段模型 + 提案 | `ChainExporter.BuildManifest` → `SaveManifest` | `Saves/chain_export_<UTC时间戳>.json`（文件名纯 ASCII；清单 + 每段镜头意图；无视频产物） |

缺资产处理：道具库无条目、条目无 `prefabPath` 或标记 `isProcedural` 时，id 进入 `missingAssets`；放置返回 null 进入 `placementFailures`，**不静默跳过**。

## 3. 复用的既有公开 API（已对源码签名核对）

| 新代码调用 | 既有成员（来源） |
|---|---|
| 离线解析 | `AiScriptParser.IsOfflineMock` / `ParseScriptAsync(string) → Task<ScriptAnalysisResult>`（Core/AiScriptParser.cs） |
| 解析结果字段 | `ScriptAnalysisResult.sceneTitle/conflictSummary/beats`；`ScriptBeat.character/action/dialogue/suggestedShotType` |
| 资产核对 | `PropLibrary.Find(id) → PropEntry`（`prefabPath` / `isProcedural`） |
| 道具落场景 | `PropBrowserUI.SpawnProp(string propId, Vector3 worldPos) → GameObject` |
| 演员落位 | `ActorNavigator.TryPlace(Vector3, float sampleRadius, out Vector3 snapped) → bool`；事件 `OnDestinationSet`（`Action<Vector3>`） |
| 演员选择 | `ActorSelection.All` / `Current` / `Changed` 事件 |
| 语义锚点 | `SemanticMarker.Find(string id)` |
| 机位预设 | `CameraPreset.Apply(CameraPresetId, Camera, Vector3 anchor) → ShotCameraState` |
| 时间轴 | `DirectorTimeline.SetDuration(float)` / `Seek(float)` / `Play()` / `Pause()` / `TogglePlay()` / `Stop()` / `isPlaying` / `currentTime` |
| 影片相机 | `CameraMotionRecorder.filmCamera`（public 字段） |
| 落盘目录 | `SceneEditStateStore.ResolveFolder()`（SHOTFIELD_SAVE_DIR → Saves/） |
| 视频桥接 | `SeedanceBridge.ApiKey/ApiEndpoint/IsOfflineMock` / `SubmitGenerationAsync(SeedanceTaskRequest) → Task<SeedanceTaskResult>` |
| 端点常量 | `ServiceEndpoints.VolcengineArkBase` |
| 空间 DTO | `Vector3Dto.FromUnityVector3(Vector3)` / `ToUnityVector3()` |

未另造平行 DTO：机位状态复用 `ShotCameraState`，分段引用复用 `ActorTake`/`CameraTrajectory` 的既有语义，导出条目直接内嵌 `TimelineClipDto`。

## 4. 对原文件的 patch 清单

**本轮：零 patch。** 所有衔接走新文件，未改任何既有源码：

- 时间轴衔接：`ChainTimelineController` 调用 `DirectorTimeline` 公开 API，未改 `DirectorTimeline.cs` / `TimelineControlsUI.cs`。
- 监看板缩放：`MonitorScaler` 只读写 `viewportFrame` 的 `RectTransform.sizeDelta`，未改 `SpatialMonitor.cs`。
- 走位震动：`ActorWalkHaptics` 订阅既有 `OnDestinationSet` 事件，未改 `ActorNavigator.cs` / `ActorSelection.cs`。

### 可选 patch（仅当集成时公开 API 不够用，由场景写者执行；本轮不要求）

- **P1（可选）**：若希望跨段由时间轴主动推事件而非控制器轮询，可在 `DirectorTimeline` 新增 `public event Action<int> OnClipBoundary;`，在 `Update` 内分段下标变化时触发；`ChainTimelineController` 改为订阅该事件。当前轮询实现无需此 patch。
- **P2（可选）**：若道具面板需要只读查询绑定状态，可在 `PropBrowserUI` 加 `public bool HasBinding(string propId)`；当前编排器以 `SpawnProp` 返回 null 判定失败，无需此 patch。

## 5. 集成接线步骤

1. 按 §1 表把文件拷入 `Assets/Scripts/` 对应目录；`Adapters/` 子目录并入 `ShotField.Core` asmdef（适配器只依赖 Core 类型与 `HttpClient`，不反向引用 RuntimeUI/Editor）。
2. 等待 Unity 编译，Console 须 0 error（暂存目录当前不参与编译）。
3. 场景新建空物体 `P1Chain`，挂 `ChainOrchestrator`；同物体可挂 `ProposalConfirmWindow`（编排器 Awake 会自动补挂）与 `ChainTimelineController`。
4. 字段接线（留空时编排器自动 `FindAnyObjectByType` 兜底）：
   - `browser` = 场景内 `PropBrowserUI` 实例；
   - `timeline` = `DirectorTimeline` 实例；
   - `filmCamera` = 影片相机（留空自动找 `CameraMotionRecorder.filmCamera`，再退到 `Camera.main`）；
   - `anchorTransform` 可留空（锚点回退 envelope 标记 → 当前演员 → 兜底坐标）。
5. 监看板：挂 `MonitorScaler`，`viewportFrame` 拖到 `SpatialMonitor.viewportFrame`。
6. 走位震动：挂 `ActorWalkHaptics`，选择手柄 `hand`，调 `amplitude` / `durationSeconds`。
7. 触发入口：外部调用 `orchestrator.ImportScript(剧本文本)`；确认窗口点「应用已接受」；需要落盘时调 `orchestrator.ExportPackage()`。
8. 在线适配（待 key）：`new SeedanceVideoAdapter(VideoProviderKind.SeedanceArk)`，填 `ApiKey` / `ApiEndpoint`，`IsOfflineMock = false`；切即梦用 `VideoProviderKind.Jimeng` 并替换 `ApiEndpoint`（同系可换端点）。

## 6. 编辑器验证步骤

| # | 操作 | 成功现象 | 排查顺序 |
|---|---|---|---|
| 1 | 打开工程等编译 | Console 0 error，新脚本出现在对应命名空间 | 先看 asmdef 引用是否齐；Adapters 是否误放进 Editor 程序集 |
| 2 | Play 后调 `ImportScript` 传一段含动作/道具关键词的文本 | 左侧提案窗口弹出；条目数 = 拍节数 ×（镜头+演员）；道具关键词命中 Prop 条目 | 窗口不可见 → 检查 `ProposalConfirmWindow.Visible`；无提案 → 看解析结果 beats 是否为空 |
| 3 | 逐条接受/拒绝后点「应用已接受」 | 报告 `placedPropIds` / `placedActorIds` 非空；失败项进 `placementFailures`；缺资产 id 在 `missingAssets` | 道具 null → 核对 `PropBrowserUI.bindings` 是否绑定 prefab；演员未落位 → 看 NavMesh 与 `TryPlace` 半径 0.6m |
| 4 | 时间轴绑定后播放 | 总时长 = 各段时长之和；播放逐段推进，跨段触发 `OnClipChanged`；`JumpToClip` 定位段首 | 总时长不对 → 看 clip 时长是否 ≥0.1s；不寻道 → 检查 `timeline` 引用 |
| 5 | 调 `ExportPackage()` | 返回 `Saves/chain_export_*.json` 绝对路径；JSON 含 `clips` 与 `missingAssets`；无伪造视频字段 | 落盘失败 → 核对 `SHOTFIELD_SAVE_DIR` 与 Saves 目录写权限 |
| 6 | 监看板拖缩放滑条 | 视口尺寸随「基准尺寸 × scale」变化；画幅切换后 `ResetBaseSize` 重新取基准 | 尺寸不动 → 检查 `viewportFrame` 引用 |
| 7 | 走位下达目标 | 编辑器/模拟器无设备只记日志不报错；真机手柄短促震动（**pending-真机**） | 无震动 → 核对 `hand` 节点与 `supportsImpulse` |

证据口径按三栏写：`模拟器已验证` / `pending-真机` / `未验证`。本轮代码未编译、未上场景，全部记 **未验证（pending-集成）**。

## 7. 存疑点

- **演员名匹配**：解析器角色名（`ActorA` / `Director`）与场景 GameObject 名（`SC01_Performer_A` 等）不一致时，`ResolveActor` 回退到 `ActorSelection.Current`；集成时需在场景命名或映射表上对齐。
- **机位锚点**：默认取 envelope 标记 → 当前演员 → 兜底坐标；多主体场景的锚点策略后续再定。
- **在线请求体**：沿用 `SeedanceBridge` 既有 JSON 格式；即梦/方舟真实字段契约待 key 到位后核对。
- **haptics 接口**：用 `UnityEngine.XR` 旧版 `InputDevices`；若 PICO SDK 要求走 XRI 控制器设备，集成时改为取 `XRController` 组件。
- **默认时长**：提案每段默认 4.0s，尚未与制作包 §4 的 S01 4.5s / S02 4.0s 逐镜对齐；后续由护照字段回填。

## 8. 红线遵守说明

- 未写 `Assets/` 下任何文件；未改场景；未碰 `Staging\PalmMenu\`；未动验证器、`ActorNavigator` / `ActorSelection` / `DesktopStudio*` 文件。
- 未删文件；未写 `F:\pico` 之外；未改 `AGENTS.md`；未执行 git add/commit/push；未杀 Unity 进程。
- 无实测效率数字，文中一律标「未验证」。
