# 验证索引 · ShotField

**每个结论都必须能追到证据。** 本页把「版本 → Commit → 判据 → 日志 → 截图/视频 → 验证环境」串起来。

## 三栏口径（不许混用）

| 栏位 | 含义 |
| --- | --- |
| **模拟器已验证** | 在 PICO Emulator（Swan x86_64 / PICO OS 6 / Android 16）上真跑过，有日志或截图 |
| **pending-真机** | 代码已落地，但只有编辑器/模拟器证据，头显行为未取证 |
| **未验证** | 只有代码或设计，没有任何运行证据 |

**「代码存在」不等于「功能可用」。** 全沉浸立体首帧、90fps、手柄交互、手势/眼动/面捕一律 pending-真机。
本项目**无 PICO 真机**；Quest 3S 跑 Meta 生态，装不了本项目 APK，**不作为验收层**。

---

## 版本索引

| 版本 | Commit | 判据 | 状态 |
| --- | --- | --- | --- |
| 0.9.0-lab.20261124 | `ea04f3c` | `NetworkSessionController` 成为桌面/VR 唯一会话，PalmMenu 场景栏可呼出 WorldSpace 联机 modal；IPv4/端口校验、Host/Join/断开、心跳与主线程 Tick；`NETWORK-ENTRY-RESULT=PASS (9/9)`，Play Mode 双菜单与控制器同实例，Host 按钮进入 Hosting | 编辑器与 Play Mode 已验证；真机双端同步 pending-设备 |
| 0.9.0-lab.20261124 | `401bc71` | 同一真实机位先判 Violation（信封正面 5/5 采样点可见），加挡板后 Pass，撤挡板恢复 Violation；`CONSTRAINT-PAIR-RESULT=PASS (6/6)`，成对 FAIL/PASS 图与两份日志已落盘 | 编辑器已验证；真机交互未纳入本判据 |
| 0.9.0-lab.20261124 | `1f85736` | 资产抽屉显示 Tripo/现有资产/灰盒的来源、路径、导入状态、实测面数与包围盒；GLB 几何无效即失败并销毁临时模型，失败保留灰盒；`ASSET-RECONCILIATION-RESULT=PASS (10/10)`，真实门样本 4,163 tris / 0.34497×1.50×0.99418m | 编辑器 UI 与导入已验证；Android IL2CPP/模拟器 pending-APK |
| 0.9.0-lab.20261124 | `30cb0de` | CameraMotionRecorder 支持注入时间采样；12s/30Hz 真实 raw/smoothed 双轨、12→6s 重定时、50% 寻址；`B5-DUAL-TRACK-RESULT=PASS (8/8)`，360 帧 mp4 `VIDEO-EXPORT-RESULT=PASS (11/11)`（连续镜头口径） | 编辑器已验证；真机录制手感 pending |
| 0.9.0-lab.20261124 | `1856a95` | DeepSeek/Tripo 凭据读取统一到 `LocalAiConfig`；只写 `persistentDataPath/ai_credentials.json`，旧 `ai_config.local.json` 在目标不存在时迁移；Assets 内同名文件由构建闸与扫描器硬拒；`CREDENTIAL-PATH-RESULT=PASS (9/9)` | 编辑器已验证；APK 端迁移 pending-APK |
| 0.9.0-lab.20261124 | `720c4cb` | 三块独立空间卡：监看板/多轨时间轴/剧本资产抽屉；抓取、0.5–3m 缩放、吸附、Clip 调序/裁切、Tripo 失败回退灰盒、PalmMenu 呼出；`THREE-CARD-RESULT=PASS (13/13)`、Play Mode `THREE-CARD-PLAY=PASS` | 编辑器已验证；真机抓握/射线手感 pending |
| 0.9.0-lab.20261124 | `03a36ad` | 天工 FBX 合并为 1 renderer / 68,569 三角形；Resources 运行时加载、白模/天工往返切换、桌面一键按钮；`STAGE-PRESET-RESULT=PASS (7/7)`、Play Mode `STAGE-PRESET-PLAY=PASS` | 编辑器已验证；APK/真机 pending；天工权属/许可待核 |
| 0.9.0-lab.20261124 | `53934fe` | GLTFUtility pin `dfdc508...`；真实 `door_v01.glb` 运行时导入、归一化、Collider/抓取补齐；`TRIPO-RUNTIME-IMPORT=PASS (5/5)` | 编辑器已验证；Android IL2CPP 导入 pending |
| 0.9.0-lab.20261124 | `c27a3c2` | Passthrough + Spatial Mesh 配置、无 subsystem 时虚拟创作台回退；`MR-TABLETOP-CONFIG=PASS (3/3)` | 编辑器配置与回退已验证；模拟器/真机桌面识别 pending |
| 0.9.0-lab.20261124 | `d8a1a55` | SceneEdit 本地单次回调、per-actor revision 冲突拒绝、重连快照；`NET-VERIFY-RESULT=PASS (13/13)`、`NET-SCENE-EDIT-RESULT=PASS (6/6)` | 编辑器已验证；VR 入口/真机 pending |
| 0.9.0-lab.20261124 | `11ff013` | 演员/代理预算、选中联动、可逆恢复、淡入淡出、PalmMenu 触感重绑定；P5 四验证器 10/10 | 编辑器已验证；真机手感 pending |
| 0.9.0-lab.20261124 | `35e8d57` | 18 个 Editor 文件菜单根统一为 `ShotField` | 编辑器已验证 |
| 0.9.0-lab.20261124 | `04748f2` | M1 六步自动演示 `PASS (6/6)`，演员/道具重载 delta=0；P6 四机位 15 秒编辑器出片，验片 `PASS (11/11)`（1080p30 / 450 帧 / 11.927Mbps / 全帧中位 1.96） | 编辑器已验证；**Android 真编码 pending-APK** |
| 0.9.0-lab.20261124 | `0ba5ac8` | 中文字体 SDF 从 1x1 占位重建为 2048x2048 真实字形表 | 编辑器已验证 |
| 0.9.0-lab.20261124 | `cbab098` | P5 元数据补齐、掌心菜单 XR 射线器、非废弃查询 API、变体工具指向唯一主场景 | 编辑器已验证；VR 手感 pending-真机 |
| 0.9.0-lab.20261123 | `7a931dd` | 六阶段 Core 状态机 + 单主场景入口断言 + Console 长驻留归零；门禁 16/16（16 文件） | 编辑器已验证 |
| 0.9.0-lab.20261123 | `3d3d626` | 摘 mcp-extensions 与 ai.assistant 传递链；XRI 3.6.1、core-utils 2.6.0 显式钉为直接依赖；guard 补扫描盲区；场景落点（4 文件） | 编辑器已验证 |
| 0.9.0-lab.20261123 | `476f633` | 关闭凭据扫描盲区，记录每次构建期凭据扫描 | 编辑器已验证 |
| 0.9.0-lab.20261123 | `1eedaf2` | 凭据留在构建输入之外，不只留在 git 外 | 编辑器已验证 |
| 0.9.0-lab.20261123 | `387949e` | 文档：p0/p2/p3/p6 提交入验证索引 | 文档 |
| 0.9.0-lab.20261122 | `ff27576` | DeepSeek 真实接入（失败不静默降级）+ Labs 分区 + 测试版标注；门禁 11/11 | 编辑器已验证 |
| 0.9.0-lab.20261122 | `5d43a00` | 演员枢轴贴地（实例级）+ 家具落位拒绝与真实绕行 + Tripo 适配器 + mp4 导出链路 + 连接中心；门禁 13/13 | 编辑器已验证 |
| 0.9.0-lab.20261122 | `5c901d9` | 脚枢轴 prefab `baseOffset` 1.0→0，消除 Play 态浮高 1m；六人脚底间隙 +0.014~0.017m；门禁 13/13 | 编辑器已验证（含 Play 态） |
| 0.9.0-lab | `35510bc` | **P0**：凭据全历史 0 命中；修「备注」样式误用与重复语义 ID；新增 `console_clean` 门禁（13→14）；README 三块重写 + 本索引 | 编辑器已验证；Console 归零结论见 `7a931dd` 长驻留协议 |
| 0.9.0-lab | `dfb7172` | **P2**：`SCRIPT-IMPORT-RESULT=PASS (4/4)` 四格式导入 + 预览确认前置 + Android SAF 全链 | 导入层编辑器已验证；**SAF 与 APK 启动 pending-APK** |
| 0.9.0-lab | `5c61097` | **P3**：`SCRIPT-PROPOSAL-RESULT=PASS (3/3)` 通用提案契约，三剧本模式各异 | 真实 API 已验证；**可编辑提案 UI 未做** |
| 0.9.0-lab | `0c1844d` | **P6 前置**：导出规格升至 1920×1080 / 30fps / 15s / ≥8Mbps，命名规则集中 | **PENDING-APK**，真 mp4 未出 |

> 提交链（新→旧）：`ea04f3c` ← `401bc71` ← `1f85736` ← `30cb0de` ← `1856a95` ← `720c4cb` ← `03a36ad` ← `53934fe` ← `c27a3c2` ← `d8a1a55` ← `11ff013` ← `35e8d57` ← `04748f2` ← `0ba5ac8` ← `cbab098` ← `670470a` ← `b3a6ef6` ← `7f06dfb` ← `7a931dd` ← `3d3d626` ← `476f633` ← `1eedaf2` ← `387949e` ← `0c1844d` ← `5c61097` ← `dfb7172` ← `35510bc`。2026-11-24 新提交均在本地，**尚未 push**。
> 后续版本 tag 规划：`v0.9.1-alpha`（Console 归零 + 统一工作流）→ `v0.9.2-alpha`（四格式 + AI 提案）→ `v0.9.3-alpha`（资产 + Tripo）→ `v0.9.4-alpha`（沙盘 1:1 + VR UI）→ `v0.9.5-beta`（时间轴 + MP4）→ `v0.9.6-beta`（模拟器完整旅程）。
> **tag 只在对应判据真过后打，不提前占位。**

---

## 门禁（30 项）

运行入口：`ShotField > Verifiers`，或工作区脚本 `04_Project/巡检/verify_all.ps1`（走 live-bridge 直调）。

| id | 判据 |
| --- | --- |
| `palette` | 视觉语言：景物两级灰、角色分色、机位琥珀色 |
| `actor_navigator` | 演员导航参数与代理配置 |
| `locomotion` | 移动与输入链路 |
| `navmesh` | NavMesh 烘焙与可达性 |
| `m3_assembly` | M3 装配完整性 |
| `timeline` | 时间轴片段与时长 |
| `b5_dual_track` | `B5-DUAL-TRACK-RESULT=PASS (8/8)`：12s/30Hz 真实录制、raw/smoothed 等采样、起终点保持、高频分量下降、12→6s 重定时不改轨迹、50% 精确落在 6s |
| `layout` | 场景布局与家具落位 |
| `camera_preset` | 机位预设约束 |
| `props` | 道具语义标记 |
| `architecture` | 分层与行数预算 |
| `m1_edit` | M1 编辑闭环 |
| `actor_grounding` | **实例级**：遍历场景全部激活 ActorNavigator，脚底与地板顶面 ±0.02m |
| `actor_placement` | 落位拒绝（不与家具重叠）+ 真实绕行 |
| `constraint_pair` | `CONSTRAINT-PAIR-RESULT=PASS (6/6)`：真实主场景/信封关键面、同一机位无挡板 Violation、加挡板 Pass、撤挡板恢复 Violation、机位状态稳定、规则原因非空 |
| `console_clean` | Console 错误与警告计数均为 0（反射读 `LogEntries.GetCountsByType`，**无白名单豁免**）；长驻留 300s 协议见下 |
| `workflow_assembly` | `WORKFLOW-ASSEMBLY-RESULT=PASS (6/6)`：反射核对六阶段枚举顺序与数值 0..5、初始阶段 Import、五次前进到 Export、终点原地、Reset、类型归属 `ShotField.Core`，无 RuntimeUI/Editor 泄漏 |
| `scene_single_entry` | `SCENE-SINGLE-ENTRY-RESULT=PASS (3/3)`：EditorBuildSettings 恰好 1 个启用条目、路径 == `BuildAndroid.ScenePath`、场景资产真实存在 |
| `tripo_runtime_import` | `TRIPO-RUNTIME-IMPORT=PASS (5/5)`：真实 GLB 导入、Mesh/Renderer、Collider、目标高度；Android IL2CPP pending |
| `asset_reconciliation` | `ASSET-RECONCILIATION-RESULT=PASS (10/10)`：三源路径/状态、实测面数与包围盒、无几何不伪造、真实 Tripo 导入、UI 回读、失败灰盒回退、五幕导航；Android IL2CPP pending |
| `stage_preset` | `STAGE-PRESET-RESULT=PASS (7/7)`：Resources 预设、单 renderer、68,569 三角形、灰模材质、主场景组件、白模/天工往返 |
| `three_card_workspace` | `THREE-CARD-RESULT=PASS (13/13)`：三卡默认隐藏、独立 Canvas、抓取/缩放/吸附、双轨 Clip 调序/裁切、轨迹引用不变、Tripo 失败回退灰盒、PalmMenu 三入口、无 legacy Text |
| `credential_path` | `CREDENTIAL-PATH-RESULT=PASS (9/9)`：新/旧文件名、路径分离、迁移条件、环境变量优先、Tripo 单入口、只写新路径、构建闸/扫描器/gitignore 硬拒 |
| `mr_tabletop_config` | `MR-TABLETOP-CONFIG=PASS (3/3)`：Passthrough、Spatial Mesh、虚拟台回退；真实桌面识别 pending-设备 |
| `network_session` | `NET-VERIFY-RESULT=PASS (13/13)`，含 `NET-SCENE-EDIT-RESULT=PASS (6/6)`：双向同步、本地单次回调、revision 拒绝、重连快照、旧 JSON 兼容 |
| `network_entry` | `NETWORK-ENTRY-RESULT=PASS (9/9)`：IPv4/端口校验、真实回环握手、WorldSpace modal、Host/Join/断开/关闭、PalmMenu 呼出、断开归零；真机双端 pending |
| `palm_menu_vr` | `PALM-MENU-VR-RESULT=PASS (10/10)`：掌心菜单构建、分栏、空引用降级；运行时手感 pending-真机 |
| `block_layout` | `BLOCK-LAYOUT-RESULT=PASS (10/10)`：弧面布局、距离档、板尺寸与排序 |
| `tabletop_scale` | `TABLETOP-SCALE-RESULT=PASS (10/10)`：1:20/1:1 换算、演员预算、MR 平面识别 pending-真机 |
| `studio_fade` | `STUDIO-FADE-RESULT=PASS (10/10)`：淡入淡出状态机与遮罩参数；人眼观感 pending-真机 |

最近一次全量（2026-11-24 17:33:55）：**30 run / 0 failed/no-verdict / 0 no-error**，日志 `verify_all_20261124_173355.log`。其中 `network_session` 输出 `NET-VERIFY-RESULT=PASS (13/13)` 与 `NET-SCENE-EDIT-RESULT=PASS (6/6)`；`network_entry` 输出 `NETWORK-ENTRY-RESULT=PASS (9/9)`；`stage_preset` 输出 `STAGE-PRESET-RESULT=PASS (7/7)`；`three_card_workspace` 输出 `THREE-CARD-RESULT=PASS (13/13)`；`credential_path` 输出 `CREDENTIAL-PATH-RESULT=PASS (9/9)`；`b5_dual_track` 输出 `B5-DUAL-TRACK-RESULT=PASS (8/8)`；`asset_reconciliation` 输出 `ASSET-RECONCILIATION-RESULT=PASS (10/10)`；`constraint_pair` 输出 `CONSTRAINT-PAIR-RESULT=PASS (6/6)`；`console_clean` 读数 `Errors=0 Warnings=0`，此前 300s 驻留协议见 `console_clean_dwell_20261123_135958.log`。

> ✅ **Console 长驻留归零（2026-11-23 13:59:58 实跑通过）**：协议为 清 Console → 域重载 → 清重载噪音 → 进 Play → 摆位走位（`PrevisCast_A_Red` MoveTo）+ 时间轴寻道（`SH_04`）→ Play 态驻留 300s，60/120/180/240/300s 五次巡检 Console 全空 → 退出 Play。读数 `CONSOLE-CLEAN-RESULT=PASS Errors=0 Warnings=0`，日志 `console_clean_dwell_20261123_135958.log`。
>
> **诚实备注（未加白名单）**：域重载时 `com.unity.xr.management` 在本会话未初始化子系统的情况下会打一条 `Call to StopSubsystems without an initialized manager` 警告；属包生命周期噪音、非我方代码，Play 初始化过子系统后退出不再复现，测量前已清掉。验证器未对该条加任何白名单豁免。

### 本轮警告清零判据备注（全程未加白名单）

- **`FindObjectsByType` 废弃重载 6 处** → 单参 `FindObjectsInactive.Exclude` 重载；旧 SortMode 重载在 XRI3 本身已废弃，排序参数一并去掉。
- **`XRBaseController` 废弃类型 9 处** → 非废弃 `HapticImpulsePlayer`（`ActorWalkHaptics` / `PalmMenuHaptics` / `PalmMenuController`）。场景内 left/rightController 引用均为 fileID=0、`ActorWalkHaptics` 未挂载，换类型不断线；**运行时触感仍 pending-真机**。
- **删 1 个 CS0414 死字段** `DesktopStudioPanel.note`。

> **裁定 19 已结案（乙：`SC01_Main` 唯一主场景）**：`scene_single_entry` 门禁断言构建场景表仅一个启用条目且路径一致。「== 录屏场景」这一条**只作文档约定，不伪造断言**。

**地面基准取地板碰撞体顶面 y=0**，不取 NavMesh 采样面（烘焙上浮 5cm，属未修缺陷）。

---

## 关键证据与已知失效证据

证据文件（截图/视频/日志）**不进版本库**（二进制红线），存放于工作区 `05_验证记录/`，随提交材料单独提供。下表给出文件名供核对。

| 结论 | 证据 | 环境 | 判定 |
| --- | --- | --- | --- |
| 六人脚底贴地（Play 态） | `20261123_0005_SceneView_修复后_红蓝演员贴地与茶几.png` + 门禁 `actor_grounding` | 编辑器 Scene + Play | 模拟器已验证（编辑器层） |
| 家具真实绕行 | `20261123_0042_GameView_运行时绕行虚线_东侧绕开茶几.png`；起 (0.8,0,0.6)→终 (0.8,0,3.2)，4 拐点，路径 3.576m / 直线 2.600m，ratio 1.376 | 编辑器 Play | 已验证 |
| 约束 FAIL→PASS→FAIL 可逆对照 | `constraint_evidence_20261124_1647/20261124_1647_约束_FAIL.png`、`..._约束_PASS.png`、两份同名 `.log` 与 `constraint_evidence.json`；同一机位 (0.95,1.453,2.25)，Violation→Pass→Violation | 编辑器真实场景 + 现有 VisibilityConstraint | 编辑器已验证；非真机交互验收 |
| VR 联机入口 | `network_entry_20261124_1732/vr_network_modal.png`、`network_entry_evidence.json`；`NETWORK-ENTRY-RESULT=PASS (9/9)`；Play Mode 控制器/模态/PalmMenu 双呈现层同实例，Host 按钮进入 Hosting | 编辑器 UI 层隔离渲染 + Play Mode 接线 | 编辑器已验证；真机双端同步 pending-设备 |
| DeepSeek 真实提案 | `deepseek_live_20261122_213814.log`（200 / json_object / 3 镜 / 236 tokens） | 真实 API | 已验证 |
| DeepSeek Unity 内 E2E | `deepseek_unity_e2e_20261122_220014.log`，末行 `DEEPSEEK-E2E-RESULT=PARTIAL` | 编辑器 Play | **PARTIAL**：接受 7 拒绝 1、落场与时间轴成立；在线返回中文称谓的端到端命中未复验 |
| Tripo 真实生成 | `tripo_spike_20261122_2305.log`，末行 `TRIPO-SPIKE-RESULT=PASS`；GLB 落盘 2,856,144 字节 | 真实 API | 已验证到**落盘**；运行时导入见下一行 |
| Tripo GLB 运行时导入 | `TRIPO-RUNTIME-IMPORT=PASS (5/5)`；真实样本 `06_素材/Tripo生成/door_v01.glb` | 编辑器 Play | 编辑器已验证；Android IL2CPP pending |
| 资产对账 UI | `asset_reconciliation_20261124_1612/asset_reconciliation_tripo.png`、`asset_reconciliation_evidence.json`；`ASSET-RECONCILIATION-RESULT=PASS (10/10)`；4,163 tris / 0.34497×1.50×0.99418m | 编辑器真实 GLB + UI 层隔离渲染 | 编辑器已验证；Android IL2CPP/模拟器 pending-APK |
| 天工宏大叙事预设 | 源 FBX SHA256 `b49a0702...ec1ee89`；`TIANGONG-PRESET=PASS renderers=1 triangles=68569`；`STAGE-PRESET-RESULT=PASS (7/7)` | 编辑器 + 本地 Resources | 编辑器已验证；权属/许可待核；APK/真机 pending |
| 三块空间卡与掌心呼出 | `20261124_1335_监看板_v05.png`、`20261124_1335_时间轴_v05.png`、`20261124_1335_剧本资产抽屉_v05.png`、`20261124_1357_掌心菜单卡片栏_v09.png`；`THREE-CARD-RESULT=PASS (13/13)`、`THREE-CARD-PLAY=PASS` | 编辑器 Play | 编辑器已验证；真机抓握/射线手感 pending |
| 凭据单路径与迁移 | `CREDENTIAL-PATH-RESULT=PASS (9/9)`；`secret_scan_resources_20261124_142635.log` 24 文件 / 0 命中 | 编辑器 + 工作区扫描 | 编辑器已验证；APK 端旧文件迁移 pending-APK |
| B5 双轨运镜实录 | `B5_frames_20261124_1534/B5_dual_track_12s_960x540.mp4`、`raw_smoothed_t06.png`、`smoothed_t06.png`、`B5_evidence_20261124_1534.json`；12.00s / 360 帧 / 30fps / 1.986Mbps / 全帧中位差 3.56，`VIDEO-EXPORT-RESULT=PASS (11/11)`（连续镜头 min-shots=0） | 编辑器录制状态机 + ffmpeg | 编辑器已验证；真机录制手感 pending |
| M1 六步连续实录 | `20261124_M1六步操作_v02.mp4`；`M1-SIXSTEP-DEMO=PASS (6/6)`，actorDelta=0.0000 / propDelta=0.0000 | 编辑器 Play + Recorder | 编辑器已验证；模拟器六步 pending-APK |
| P6 四机位本地出片 | `20261124_P6预演_15s_8Mbps_v06.mp4`；15/45 两档 `VIDEO-EXPORT-RESULT=PASS (11/11)`：1920x1080 / 30fps / 15.00s / 450 帧 / 11.927Mbps / 全帧中位 1.96 / 切镜达标 | 编辑器 Play + Recorder + ffmpeg CBR | 编辑器已验证；Android 真编码 pending-APK |
| mp4 导出链路 | 编辑器菜单自测 PASS；两条失败分支已验；验片脚本改为全帧运动/切镜扫描，`--pairs` 仅诊断 | 编辑器 | **Android 真编码 pending-APK** |
| Console 长驻留归零 | `console_clean_dwell_20261123_135958.log`，`CONSOLE-CLEAN-RESULT=PASS Errors=0 Warnings=0`，300s 五次巡检全空 | 编辑器 Play 驻留 | 模拟器已验证（编辑器层） |
| 模拟器装机运行 | `20261118_1752_模拟器_D包运行截图.png`、`emu_D_run_20261118_1752.log` | Swan 模拟器 | 已验证：装入成功、进程存活、崩溃缓冲区空、OpenXR 正常加载 |

### ⚠️ 已作废的证据（不要再引用）

| 证据 | 为什么作废 |
| --- | --- |
| `20261122_MRspike_v01.mp4` | 内容全黑。双重根因：adb screenrecord 抓不到 VST 合成层（取证手段错）；spike 场景无光源。**MR 类证据禁止用 adb screenrecord 单独作证。** |
| `20261122_1831_编辑器_六人脚底贴地_侧拍.png` 及 `b87bacb` 的「gap=-0.001m」结论 | 参数级断言冒充实例级事实。当时验证器在 (10000,10000,10000) 建临时 host 只测 agent 参数，不读场景实例，埋地 0.86m 仍判 PASS。已由实例级 `actor_grounding` 取代。 |
| 「模拟器上 OpenXR 初始化崩溃、根因未定」 | 2026-11-18 17:52 装机取证推翻，不要再按它排期。 |
| `verify_all_20261123_020318.log` | 历史读数 14 run / 0 failed；门禁增至 16 项后已被 `verify_all_20261123_140741.log` 取代，勿与当前口径并列引用。 |
| `verify_all_20261123_133106.log` | 移包中间态：14 run / 1 failed/no-verdict；移包完成后已复跑通过，勿当当前口径。 |

---

## 未验证 / 已知缺陷（诚实清单）

- **运行时 GLB 导入与资产对账已实现，Android 侧未验**：`com.plattar.unitygltf` 是 Editor-only 导出器，已改用 pin 的 GLTFUtility(MIT)；真实 `door_v01.glb` 在编辑器导入/缩放/Collider/抓取补齐 `PASS (5/5)`，资产抽屉对账 `PASS (10/10)`。IL2CPP 裁剪、URP shader 与端上加载仍 pending-APK。
- **世界画布中文豆腐块已修**：TMP 迁移 + `ShotFieldSansSC-Regular.otf` + 2048x2048 SDF 字形表（`0ba5ac8`）；M1/P6 实录已见中文 UI。字体许可仍需补进资产来源清单。
- **P5 运行态接线已补**：演员/代理预算、选中联动、可逆恢复、淡入淡出、触感重绑定已完成（`11ff013`）；三块空间卡片及 PalmMenu 呼出已完成（`720c4cb`），真机抓握、射线与触感手感仍 pending。
- **M2 VR 联机入口已在编辑器/Play Mode 打通**：桌面与 VR 共用 `NetworkSessionController`，PalmMenu 可呼出联机 modal，真实回环握手与 Host 按钮已验；两台 PICO 真机同步、设备键盘输入与手柄射线点击仍 pending-设备。
- **MR 只有配置与 Editor 回退证据**：`MrTabletopBootstrap` 已接 Passthrough + Spatial Mesh，无 subsystem 时回退虚拟创作台；模拟器/真机真实桌面识别与贴合精度 pending-设备。
- **天工预设仅 1:1 舞台切换**：已合并为单网格灰模并提供桌面一键切换；沙盘迷你模式、演员/相机自动重排、权属/许可与 Android 包内加载未验。
- **P6 Android 真编码未验**：编辑器交付片已 PASS (11/11)，MediaCodec/MediaMuxer 需重打 APK 后在模拟器取证。
- **掌心菜单 VR 侧未接线**：`PalmMenuController` 的 8 个引用全为 0，`InstantiateButton` 遇空引用返回 null。属未接线，非废案。
- **Blue 演员 Play 态漂位**：序列化 (-1.00,0,1.40) → Play (-0.636,0.050,1.082)，根因未追。
- **NavMesh 烘焙面上浮 5cm**（y=0.050）未修。
- **prefab agent radius/height（0.5/2）与场景覆盖值（0.30/1.75）未对齐**。
- **在线取片半截链路**：`SeedanceBridge` 提交后只判 HTTP 成功，轮询/下载/回填未实现。
- **桌面 IMGUI 面板无平台守卫**，Android 包内仍会绘制，头显内能否操作未确认。

---

## 工程依赖口径（2026-11-23 依赖图实测）

### 2026-11-24 编译恢复与 Recorder

- Safe Mode 复现真因：`Library/Bee` 编译状态陈旧，RuntimeUI rsp 未包含 P5 新文件；归档 Bee（零删除）后 Unity 从零重建，`ScriptAssemblies` 恢复，0 error CS。
- `com.unity.recorder` **5.1.8** 已通过 Package Manager 安装；M1/P6 共用 `ShotFieldRecorder`，输出 1080p30 MP4。
- KitWright MCP 8765 与官方 MCP 8080 均已连通；官方 server 位于工作区 `04_Project/Tools/venv`，不写用户目录。
- 天工 v05 Android 仅按用户指定路径提取；源 FBX 4,879,628 B，SHA256 `b49a0702b418ac8f2841990d98507419679d68f71f2e6b584f2f4c4e3ec1ee89`。Unity 合并为单网格 68,569 三角形，本地 Resources 加载，FBX/mesh/prefab 均 gitignore；**权属/许可未核，不作公开仓库资产**。

### Safe Mode 事故与处置

- **真根因**：`com.bytedance.pico.mcp-extensions@0.1.2` 硬依赖 `com.unity.ai.assistant: 2.9.0-pre.2`，manifest 删直接条目挡不住传递拉取；旧版 `DynamicAssemblyBuilder.cs(191,20)` 报 **UAC0020**（`Assembly.Load(byte[])` 进非 Unity ALC），被 `Library/ImmutablePackage.ruleset` 定为 Error，`scriptCompilationFailed=true`，**Unity 原生拒绝进 Play**。
- **解法（实测依赖图后定的第五条路）**：摘 mcp-extensions + 把 XRI 3.6.1 与 core-utils 2.6.0 显式钉为直接依赖。XRI/core-utils 原本经 mcp-extensions 传递引入，工程 18 个 .cs + 两个 asmdef 在用 XRI，只摘扩展会让 XRI 降版炸第二次。
- **当前 lock 实测**：ai.assistant **ABSENT**、mcp-extensions **ABSENT**、XRI **3.6.1 depth 0**、core-utils **2.6.0 depth 0**、`com.coplaydev.unity-mcp` 保留（depth 0）、`com.bytedance.pico.xr` 保留（6.2.0，depth 0）。
- **代价**：失去 PICO agent 侧 MCP 附加 API（编辑器辅助，不进 APK）；产品运行时触感不受影响，走 pico.xr 的 PXR API。
- **MR 包处置**：`com.bytedance.pico.spatialadapter` 已移；MR 归 Labs，P7 再加回。`MR_Spike.unity` 期间存在缺脚本引用，该场景录屏早已判无效，故意不动。
- **TMP 更正**：`com.bytedance.pico.xr` 6.2.0 依赖含 `com.unity.textmeshpro: 3.0.6`，**TMP 经传递可用**（lock depth 1）；此前「manifest 无 TMP 包」只对直接依赖成立。**中文字体仍未拍板**，不是已解决项。

---

## 凭据口径

**安全判据 = 任何交付产物里没有 key。** 不是「git 历史 0 命中」——`.gitignore` 挡得住 git，**挡不住 Unity 打包**：`Assets/Resources/**` 会被无条件打进 APK。

| 层 | 口径 |
| --- | --- |
| 版本库 | 全历史扫描 0 命中；无 `ai_config*` 被跟踪 |
| 开发机基线 | 放在 `Assets/` **之外**（仓库内 `Tools/`，仍被 gitignore），**仅编辑器读取** |
| Android 运行时 | 只走 `persistentDataPath/ai_credentials.json` + 环境变量；旧 `ai_config.local.json` 在目标不存在时自动迁移，key 由用户在连接中心录入，**包里不带 key** |
| 构建期硬闸 | `IPreprocessBuildWithReport` 扫 `Assets/Resources/**` 与 `Assets/StreamingAssets/**`，命中 key 模式即 `BuildFailedException`，**带 key 的包构建不出来** |
| 门禁期取证 | `巡检/secret_scan.ps1 -Targets resources,apks -Strict`，要求 `hitCount=0` **且 `scannedFiles` 非 0**（空扫不许判 PASS） |

环境变量：`SHOTFIELD_DEEPSEEK_KEY` / `SHOTFIELD_TRIPO_KEY`。未配置时明确提示不可用，**不静默降级伪装成功**。

> **Guard 现状（2026-11-23）**：`CredentialBuildGuard.cs` 现 **151 行**，`IPreprocessBuildWithReport` 覆盖全部构建路径，不改 `BuildAndroid.cs`。原向上遍历会先撞上 `04_Project` 下残留的嵌套 `05_验证记录` 陈旧目录而写错证据目录；现改为先定位含 `04_*` 的工作区根，再取其直属 `05_*`，与 `verify_all.ps1` 口径对齐；找不到退化 `persistentDataPath` + `LogWarning`。扫描根扩到 Assets 全量文本文件，跳 `.meta` 与二进制，正则 dash-aware。
> **2026-11-24 复扫**：`secret_scan.ps1 -Targets resources` → 19 文件 / 0 命中；`-Targets apks` → 605 文件 / 0 命中。APK 分支已从 Git Bash `tar` 改为 .NET `ZipFile`，避免把 `F:` 误当远程主机。
>
> **端到端拒包仍 pending**：真实构建期 `BuildFailedException` 拒包链路尚未打 APK 取证；包冻结闸已解出，待稍后打 APK 时取证。**不计入已验证。**

> **历史事故留痕**：两把活 key 曾放在 `Assets/Resources/ai_config.local.json`，git 侧干净但**会被打进 APK**。现存旧包未带 key 纯属时间差（包构建于配置出现之前）。据此把判据从「仓库里没有 key」改判为「交付产物里没有 key」。

---

## 已定死的规格口径（不得再引用作废值）

| 项 | 现行规格 | 已作废 |
| --- | --- | --- |
| 演员 `baseOffset` | **枢轴挪到脚底后 `baseOffset = 0`** | `+0.748f` 是 2026-11-21 的历史补偿值，**已作废，不得再当规格引用** |
| 地面基准 | 地板碰撞体顶面 **y = 0** | NavMesh 采样面（烘焙上浮 5cm），不作地面判据 |
| 导出规格 | 1920×1080 / 30fps 固定 / 15s / ≥8Mbps / ≥3 真实切镜 | 720p / 24fps / 5s 首版参数 |
| 导出文件名 | 产品内导出 `Build/Video/SC01_Demo_yyyyMMdd_HHmm.mp4`；编辑器验收片 `20261124_P6预演_15s_8Mbps_v06.mp4` | `SC01_Final.mp4` 仅作历史测试产物，**不作验收** |

---

## 第三方许可红线

- **Meta SDK**：License §1.2.8 不得使 SDK 受制于开源许可、§1.2.1 除授权示例源码外不得衍生、§1.3 再分发须保留版权与 Notice。来源 <https://developers.meta.com/horizon/licenses/oculussdk/>
- **PICO SDK**：本地 `LICENSE.md` 为 All rights reserved，禁复制传播。来源 <https://developer.picoxr.com/document/distribute/sdk-license-terms/>
- **两者样例源文件与场景均不得进本 public 仓库**，「只要场景不要代码」不改变性质。
- Poly Haven 家具为 CC0。
