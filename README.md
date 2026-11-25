# 镜场 ShotField

**让创作者走进自己想拍的故事。** 专为 PICO 空间计算打造的影视级空间预演与虚拟制片工具：AI 剧本拆解、MR 透视沙盘与 1:1 片场无缝切换、1:1 真人表演实录、斯坦尼康运镜增稳、五轨空间探针剪辑、多域载具导航与创作资产社区复用。

[快速开始](#快速开始) · [核心能力与实测门禁](#核心能力与实测门禁) · [二次开发与扩展指南](#二次开发与扩展指南) · [六阶段端到端流程](#六阶段端到端全链路) · [Android 构建](#android-构建) · [许可证](#许可证与来源)

![License](https://img.shields.io/badge/license-AGPL--3.0-blue) ![Unity](https://img.shields.io/badge/Unity-6000.6.5f1-blue) ![Platform](https://img.shields.io/badge/target-PICO%20Android%20ARM64-orange) ![OpenSource](https://img.shields.io/badge/PICO%20Space%20Program-2026%20Open%20Source-brightgreen) ![Entropy](https://img.shields.io/badge/entropy-345%20scripts%20%7C%200%20oversize-brightgreen)

> **工程透明承诺**：本项目严格区分「编辑器无头实测通过」「有接口待真机」「尚未实现」。代码存在不等于功能可用；所有测试结论均附可复现门禁脚本与日志，详见 **[VERIFICATION.md](VERIFICATION.md)**。

---

## 核心能力与实测门禁

| 模块 | 能力与技术实现 | 实测门禁状态 |
| :--- | :--- | :--- |
| **1:1 真人表演实录** | 掌心菜单点击**「上场演这镜」**，通过 `ActorPossessionController` 附身 `ActorA` 并自动标定眼高比例；采用 Unity 原生 `Animator.OnAnimatorIK`（`SC01_ActorMotion.controller` 已开启 `m_IKPass: 1`）实现双柄 3 点 VR IK 驱动，30Hz 录制为 `ActorPerformanceClip` 并即时回放；预留 PICO Motion Tracker 2.0 24 节点接口。 | `ACTOR-PERF-RESULT=PASS (5/5)` |
| **创作资产社区（Android 兼容）** | 遵循依赖倒置 `ICommunityAssetRepository`，本地优先实现 `LocalCommunityAssetRepository`（通过 `catalog_index.json` + `UnityWebRequest` 兼容 Android APK 只读包）；内置 3 套官方 `.sfpack.zip` 案例包，掌心菜单**「社区案例库」**支持一键加载复用，并预留 `RemoteHttpCommunityRepository` 云端切换桩。 | `COMMUNITY-ASSET-RESULT=PASS (5/5)` |
| **多人同室局域网协同** | `NetworkSessionController` + `LanRoomDiscovery`（UDP 7778 广播）支持免头显手敲 IP 自动发现房间、`VrNetworkPanel` 一键加入；`RemoteActorSync.ApplyInterpolated` 提供 80ms 指数阻尼平滑与分演员 revision 冲突仲裁（双真机现场同步待上机取证）。 | `NET-VERIFY-RESULT=PASS (13/13)`<br>`NET-RUNTIME-BINDING-RESULT=PASS (5/5)` |
| **四格式剧本导入与 AI 四道闸** | 支持 `txt` / `md` / `docx` / 文本层 `pdf` 导入；接入 DeepSeek 拆解分镜，经四道闸流水线（Schema、机位边界、Ghost 虚影、物理 Transform 回读误差 $<0.001\text{m}$）落地，确认前零网络调用。 | `SCRIPT-IMPORT-RESULT=PASS (4/4)`<br>`SCENE-COMMAND-GATE-RESULT=PASS (15/15)` |
| **MR 透视沙盘与三卡多窗口** | `PicoPlaneTabletopAligner` 对接 PICO MR 平面择优吸附实体桌面（无桌面优雅回退虚拟台），`StudioFadeTransition` 实现 1:20 沙盘 ↔ 1:1 片场平滑过渡；导演监视器、五轨时间轴、剧本资产抽屉三块独立 WorldSpace 窗口满足空间应用多窗口规范。 | `TABLETOP-SCALE-RESULT=PASS (14/14)`<br>`THREE-CARD-RESULT=PASS (13/13)`<br>`PALM-MENU-VR-RESULT=PASS (10/10)` |
| **斯坦尼康防抖与 5 轨空间探针** | `VirtualCameraStabilizer` 提供 Handheld / Steadicam / Dolly 三档增稳（One Euro 滤波 + 零横滚锁）；`DirectorTimeline` 同步调度 Camera / Actor / Prop / Audio / Light 五轨与零 GC 空间视锥探针。 | `STEADICAM-SYNC-RESULT=PASS (5/5)`<br>`DIRECTOR-TIMELINE-RESULT=PASS (5/5)` |
| **Tripo 运行时 GLB 加载** | `RuntimeGltfImporter` 跨线程解析 GLB 并回主线程挂载骨骼/碰撞体与 NavMeshObstacle Carving。 | `TRIPO-RUNTIME-IMPORT=PASS (5/5)` |
| **代码熵与安全守卫** | **345 个 C# 脚本 / 36,411 行**，严格 4 层单向依赖（`Core` 零反向引用），**0 个超 200 行文件**，**0 缺失 `.meta`**；`secret_scan` 实测 git 与 APK 均 0 真实密钥泄露；`console_clean` 如实记录 `Errors=0, Warnings=9`（PICO SDK 自身 Warning）。 | `entropy_check`: **0 oversize**<br>`credential_path`: **PASS (9/9)** |

---

## 二次开发与扩展指南

本项目从架构设计上支持其他开发者快速 Fork 与二次开发：

1. **严格四层程序集隔离（`asmdef`）**：
   - `ShotField.Core`（零外部框架耦合）→ `ShotField.Constraints` / `ShotField.Import` / `ShotField.Export` → `ShotField.RuntimeUI` → `ShotField.Editor`。
   - 所有脚本均严格控制在 **≤ 200 行**，职责单一、易于定位和替换。
2. **扩展创作资产社区后端（依赖倒置）**：
   - 只需实现 [ICommunityAssetRepository.cs](Assets/Scripts/Core/Community/ICommunityAssetRepository.cs) 中的 `ListAsync`、`DownloadPackageAsync`、`PublishPackageAsync`，即可将内置的 `LocalCommunityAssetRepository` 无缝切换为自建云端仓库（可参考 `RemoteHttpCommunityRepository.cs`）。
   - 向 `Assets/StreamingAssets/CommunityCatalog/catalog_index.json` 注册纯 ASCII 命名的 `.sfpack.zip` 即可热扩充官方案例库。
3. **接入新的 AI 模型或 3D 生成源**：
   - `DeepSeekClient` 与 `Tripo` 适配器均位于 `Core/Adapters/`，凭据统一从 `Application.persistentDataPath` 或环境变量读取，严禁写入 `Assets/Resources/`。
4. **一键自动化门禁回归**：
   - 在 Unity Editor 菜单 `ShotField > Verifiers` 中可直接运行单项验证器，或在无头模式下通过 `BuildAndroid.BuildIl2Cpp` 出包。

---

## 六阶段端到端全链路

通过 `E2EWorkflowRunner` 驱动剧本解析 → AI 四道闸 → 资产加载 → MR 沙盘推演 → 斯坦尼康与 5 轨调度 → 1080p 导出规格与 MediaCodec 插件在位校验（`E2E-PIPELINE-RESULT=PASS (6/6)`；Android 真机硬件编码状态标为 `pending-APK`）。

```mermaid
flowchart LR
    S1["① 剧本解析"] --> S2["② AI 四道闸"]
    S2 --> S3["③ 资产与GLB加载"]
    S3 --> S4["④ MR沙盘与1:1表演"]
    S4 --> S5["⑤ 斯坦尼康与5轨"]
    S5 --> S6["⑥ 导出规格与插件校验"]
```

---

## 快速开始

```bash
git clone https://github.com/xrlite/ShotField.git
cd ShotField
```

1. **环境要求**：Unity `6000.6.5f1`（需安装 Android Build Support、SDK 34、NDK 27、OpenJDK 17）。
2. **工程路径**：必须放置在**纯 ASCII 路径**下（Android 构建工具链限制）。
3. **唯一主场景**：打开 **`Assets/Scenes/SC01_Main.unity`** 即可直接运行或打包。
4. **VR 交互入口**：运行时呼出左手**掌心菜单（PalmMenu）**，即可体验「上场演这镜」「社区案例库」「联机（含局域网发现）」与「三卡工作台」。

---

## Android 构建

关闭占用工程的 Unity 编辑器，在仓库根目录执行：

```powershell
$repo  = (Get-Location).Path
$unity = '<UnityEditorPath>\Unity.exe'
$env:SHOTFIELD_OUT_APK = Join-Path $repo 'Build/ShotField_arm64.apk'
New-Item -ItemType Directory -Path (Join-Path $repo 'Build') -Force | Out-Null
& $unity -batchmode -quit -projectPath $repo `
  -executeMethod BuildAndroid.BuildIl2Cpp `
  -logFile (Join-Path $repo 'Build/android.log')
```

- 强制 **IL2CPP + ARM64**（PICO SDK 6.2.0 要求）。
- 凭据配置仅在设备端 `persistentDataPath` 或环境变量录入，安装包内永不携带 API Key。

---

## 实验室与待真机确认边界

- **待真机现场取证（`pending-真机`）**：PICO 4 Ultra / Space Pro 真机 MR 空间网格精度、90fps 帧率、PICO Motion Tracker 2.0 24 节点体感、双头显局域网同步与 SAF 原生文件选择器弹窗。
- **Android 硬件 MP4 编码**：MediaCodec + MediaMuxer Java 插件已编译在位，Editor 侧已验显存缓冲与规格，真机成片编码标为 `pending-APK`。

---

## PICO Space Program 2026 参赛声明

- **参赛方向**：Unity 空间应用创作 / 沉浸内容创作（Unity OpenXR + PICO XR SDK 6.2.0）。
- **核心差异化**：区别于纯二维 AI 视频生成，镜场聚焦让创作者在三维空间中真实**排戏、表演、运镜、协作**，并将导演决策沉淀为可编辑、可社区复用的标准化空间资产包（`.sfpack.zip`）。

---

## 许可证与来源

ShotField 团队原创**源代码**遵循 [GNU AGPL-3.0](LICENSE) 许可协议，版权归 © 2026 ShotField Team 所有（任何修改或网络服务部署均需按 AGPL-3.0 开源回馈）。
**品牌与创作资产不随代码许可开放**：「镜场 ShotField」名称、商标、图标、宣传物料与内置场景/动作模板包保留所有权利（All Rights Reserved）。第三方依赖（PICO XR SDK、Unity Starter Assets、Siccity GLTFUtility、Poly Haven CC0 资源）均按各自协议合法使用。
