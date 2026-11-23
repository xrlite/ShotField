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
| 0.9.0-lab.20261122 | `ff27576` | DeepSeek 真实接入（失败不静默降级）+ Labs 分区 + 测试版标注；门禁 11/11 | 编辑器已验证 |
| 0.9.0-lab.20261122 | `5d43a00` | 演员枢轴贴地（实例级）+ 家具落位拒绝与真实绕行 + Tripo 适配器 + mp4 导出链路 + 连接中心；门禁 13/13 | 编辑器已验证 |
| 0.9.0-lab.20261122 | `5c901d9` | 脚枢轴 prefab `baseOffset` 1.0→0，消除 Play 态浮高 1m；六人脚底间隙 +0.014~0.017m；门禁 13/13 | 编辑器已验证（含 Play 态） |
| 0.9.0-lab | `35510bc` | **P0**：凭据全历史 0 命中；修「备注」样式误用与重复语义 ID；新增 `console_clean` 门禁（13→14）；README 三块重写 + 本索引 | 编辑器已验证；**Console 未真正归零**，见下方口径局限 |
| 0.9.0-lab | `dfb7172` | **P2**：`SCRIPT-IMPORT-RESULT=PASS (4/4)` 四格式导入 + 预览确认前置 + Android SAF 全链 | 导入层编辑器已验证；**SAF 与 APK 启动 pending-APK** |
| 0.9.0-lab | `5c61097` | **P3**：`SCRIPT-PROPOSAL-RESULT=PASS (3/3)` 通用提案契约，三剧本模式各异 | 真实 API 已验证；**可编辑提案 UI 未做** |
| 0.9.0-lab | `0c1844d` | **P6 前置**：导出规格升至 1920×1080 / 30fps / 15s / ≥8Mbps，命名规则集中 | **PENDING-APK**，真 mp4 未出 |

> 后续版本 tag 规划：`v0.9.1-alpha`（Console 归零 + 统一工作流）→ `v0.9.2-alpha`（四格式 + AI 提案）→ `v0.9.3-alpha`（资产 + Tripo）→ `v0.9.4-alpha`（沙盘 1:1 + VR UI）→ `v0.9.5-beta`（时间轴 + MP4）→ `v0.9.6-beta`（模拟器完整旅程）。
> **tag 只在对应判据真过后打，不提前占位。**

---

## 门禁（14 项）

运行入口：`ShotField > Verifiers`，或工作区脚本 `04_Project/巡检/verify_all.ps1`（走 live-bridge 直调）。

| id | 判据 |
| --- | --- |
| `palette` | 视觉语言：景物两级灰、角色分色、机位琥珀色 |
| `actor_navigator` | 演员导航参数与代理配置 |
| `locomotion` | 移动与输入链路 |
| `navmesh` | NavMesh 烘焙与可达性 |
| `m3_assembly` | M3 装配完整性 |
| `timeline` | 时间轴片段与时长 |
| `layout` | 场景布局与家具落位 |
| `camera_preset` | 机位预设约束 |
| `props` | 道具语义标记 |
| `architecture` | 分层与行数预算 |
| `m1_edit` | M1 编辑闭环 |
| `actor_grounding` | **实例级**：遍历场景全部激活 ActorNavigator，脚底与地板顶面 ±0.02m |
| `actor_placement` | 落位拒绝（不与家具重叠）+ 真实绕行 |
| `console_clean` | Console 错误与警告计数均为 0（反射读 `LogEntries.GetCountsByType`，**无白名单豁免**） |

最近一次全量：`14 run / 0 failed / 0 no-verdict`，日志 `verify_all_20261123_020318.log`。

> ⚠️ **`console_clean` 的口径局限必须知道**：门禁是 **edit-mode 短窗口**运行，第三方包的 one-shot / 异步警告在该窗口内不复现，因此判 PASS。
> 按完整归零协议（清 Console → Domain Reload → 进 Play → 操作提案/摆位/时间轴 → 驻留 5 分钟 → 再读）实测为
> **`CONSOLE-CLEAN-RESULT=FAIL Errors=0 Warnings=2`**。
> **以长驻留读数为准，Console 尚未真正归零。** 两条残留均为远程包侧，见下表。

| 残留警告 | 来源 | 为什么工程侧修不掉 |
| --- | --- | --- |
| `Account API did not become accessible within 30 seconds` | `com.unity.ai.assistant`（editor-only） | 云账号/网络/编辑器焦点，不进运行时与 APK，工程脚本不调用 |
| `SpatialAdapter: Spatial Camera missing in scene SC01_Main!` | `com.bytedance.pico.spatialadapter` 的 `Scripts/Editor/SceneValidator.cs` | `IProcessSceneWithReport` **无条件** `FindObjectsOfType<SpatialCamera>()`，不读 MR 开关（上游注释写了该判断但代码没实现）。包经 manifest 远程解析进 PackageCache，改缓存重解析即丢；Unity 无公开 API 反注册其它程序集的场景处理回调；vendoring 会触犯 PICO SDK 再分发红线 |

**地面基准取地板碰撞体顶面 y=0**，不取 NavMesh 采样面（烘焙上浮 5cm，属未修缺陷）。

---

## 关键证据与已知失效证据

证据文件（截图/视频/日志）**不进版本库**（二进制红线），存放于工作区 `05_验证记录/`，随提交材料单独提供。下表给出文件名供核对。

| 结论 | 证据 | 环境 | 判定 |
| --- | --- | --- | --- |
| 六人脚底贴地（Play 态） | `20261123_0005_SceneView_修复后_红蓝演员贴地与茶几.png` + 门禁 `actor_grounding` | 编辑器 Scene + Play | 模拟器已验证（编辑器层） |
| 家具真实绕行 | `20261123_0042_GameView_运行时绕行虚线_东侧绕开茶几.png`；起 (0.8,0,0.6)→终 (0.8,0,3.2)，4 拐点，路径 3.576m / 直线 2.600m，ratio 1.376 | 编辑器 Play | 已验证 |
| DeepSeek 真实提案 | `deepseek_live_20261122_213814.log`（200 / json_object / 3 镜 / 236 tokens） | 真实 API | 已验证 |
| DeepSeek Unity 内 E2E | `deepseek_unity_e2e_20261122_220014.log`，末行 `DEEPSEEK-E2E-RESULT=PARTIAL` | 编辑器 Play | **PARTIAL**：接受 7 拒绝 1、落场与时间轴成立；在线返回中文称谓的端到端命中未复验 |
| Tripo 真实生成 | `tripo_spike_20261122_2305.log`，末行 `TRIPO-SPIKE-RESULT=PASS`；GLB 落盘 2,856,144 字节 | 真实 API | 已验证到**落盘**；运行时导入未实现 |
| mp4 导出链路 | 编辑器菜单自测 PASS；两条失败分支已验 | 编辑器 | **Android 真编码 pending-APK** |
| 模拟器装机运行 | `20261118_1752_模拟器_D包运行截图.png`、`emu_D_run_20261118_1752.log` | Swan 模拟器 | 已验证：装入成功、进程存活、崩溃缓冲区空、OpenXR 正常加载 |

### ⚠️ 已作废的证据（不要再引用）

| 证据 | 为什么作废 |
| --- | --- |
| `20261122_MRspike_v01.mp4` | 内容全黑。双重根因：adb screenrecord 抓不到 VST 合成层（取证手段错）；spike 场景无光源。**MR 类证据禁止用 adb screenrecord 单独作证。** |
| `20261122_1831_编辑器_六人脚底贴地_侧拍.png` 及 `b87bacb` 的「gap=-0.001m」结论 | 参数级断言冒充实例级事实。当时验证器在 (10000,10000,10000) 建临时 host 只测 agent 参数，不读场景实例，埋地 0.86m 仍判 PASS。已由实例级 `actor_grounding` 取代。 |
| 「模拟器上 OpenXR 初始化崩溃、根因未定」 | 2026-11-18 17:52 装机取证推翻，不要再按它排期。 |

---

## 未验证 / 已知缺陷（诚实清单）

- **运行时 GLB 导入未实现**：`com.plattar.unitygltf` 实为 **Editor-only 导出器**（`Compiler.asmdef` / `PlattarCompile.asmdef` 均 `includePlatforms:["Editor"]`），**进不了 APK**。已选 GLTFUtility(MIT) 路线，待引入。
- **世界画布中文豆腐块**：全场景 66 个 legacy `UnityEngine.UI.Text`、0 个 TMP，字体为内置 Arial（无 CJK）。IMGUI 面板中文正常。字体来源涉许可，待裁定。
- **掌心菜单 VR 侧未接线**：`PalmMenuController` 的 8 个引用全为 0，`InstantiateButton` 遇空引用返回 null。属未接线，非废案。
- **Blue 演员 Play 态漂位**：序列化 (-1.00,0,1.40) → Play (-0.636,0.050,1.082)，根因未追。
- **NavMesh 烘焙面上浮 5cm**（y=0.050）未修。
- **prefab agent radius/height（0.5/2）与场景覆盖值（0.30/1.75）未对齐**。
- **在线取片半截链路**：`SeedanceBridge` 提交后只判 HTTP 成功，轮询/下载/回填未实现。
- **桌面 IMGUI 面板无平台守卫**，Android 包内仍会绘制，头显内能否操作未确认。

---

## 凭据口径

**安全判据 = 任何交付产物里没有 key。** 不是「git 历史 0 命中」——`.gitignore` 挡得住 git，**挡不住 Unity 打包**：`Assets/Resources/**` 会被无条件打进 APK。

| 层 | 口径 |
| --- | --- |
| 版本库 | 全历史扫描 0 命中；无 `ai_config*` 被跟踪 |
| 开发机基线 | 放在 `Assets/` **之外**（仓库内 `Tools/`，仍被 gitignore），**仅编辑器读取** |
| Android 运行时 | 只走 `persistentDataPath` + 环境变量，key 由用户在连接中心录入，**包里不带 key** |
| 构建期硬闸 | `IPreprocessBuildWithReport` 扫 `Assets/Resources/**` 与 `Assets/StreamingAssets/**`，命中 key 模式即 `BuildFailedException`，**带 key 的包构建不出来** |
| 门禁期取证 | `巡检/secret_scan.ps1 -Targets resources,apks -Strict`，要求 `hitCount=0` **且 `scannedFiles` 非 0**（空扫不许判 PASS） |

环境变量：`SHOTFIELD_DEEPSEEK_KEY` / `SHOTFIELD_TRIPO_KEY`。未配置时明确提示不可用，**不静默降级伪装成功**。

> **历史事故留痕**：两把活 key 曾放在 `Assets/Resources/ai_config.local.json`，git 侧干净但**会被打进 APK**。现存旧包未带 key 纯属时间差（包构建于配置出现之前）。据此把判据从「仓库里没有 key」改判为「交付产物里没有 key」。

---

## 已定死的规格口径（不得再引用作废值）

| 项 | 现行规格 | 已作废 |
| --- | --- | --- |
| 演员 `baseOffset` | **枢轴挪到脚底后 `baseOffset = 0`** | `+0.748f` 是 2026-11-21 的历史补偿值，**已作废，不得再当规格引用** |
| 地面基准 | 地板碰撞体顶面 **y = 0** | NavMesh 采样面（烘焙上浮 5cm），不作地面判据 |
| 导出规格 | 1920×1080 / 30fps 固定 / 15s / ≥8Mbps / ≥3 真实切镜 | 720p / 24fps / 5s 首版参数 |
| 导出文件名 | `Build/Video/SC01_Demo_yyyyMMdd_HHmm.mp4` | `SC01_Final.mp4` 仅作历史测试产物，**不作验收** |

---

## 第三方许可红线

- **Meta SDK**：License §1.2.8 不得使 SDK 受制于开源许可、§1.2.1 除授权示例源码外不得衍生、§1.3 再分发须保留版权与 Notice。来源 <https://developers.meta.com/horizon/licenses/oculussdk/>
- **PICO SDK**：本地 `LICENSE.md` 为 All rights reserved，禁复制传播。来源 <https://developer.picoxr.com/document/distribute/sdk-license-terms/>
- **两者样例源文件与场景均不得进本 public 仓库**，「只要场景不要代码」不改变性质。
- Poly Haven 家具为 CC0。