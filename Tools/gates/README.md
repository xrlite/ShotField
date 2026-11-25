# Gates（门禁脚本镜像）

本目录是工作区 `04_Project/巡检/` 门禁脚本的**只读镜像**，随仓库提交，便于评审方审阅断言与判据。
权威运行位置仍是工作区 `04_Project/巡检/`；本镜像不保证路径自洽。

## 文件

| 文件 | 作用 |
| :--- | :--- |
| `verify_all.ps1` | 跑 `verifiers.map.json` 里全部验证器，产出 `verify_all_<stamp>.log` |
| `unity_gate.ps1` | 公共函数：live bridge 调用、超时、判据判定 |
| `verifiers.map.json` | 验证器 → 菜单/方法/代码/判据 tag/监控文件的映射（**判据定义在此**） |
| `secret_scan.ps1` | 凭据扫描，`-Strict` 时任何命中即失败 |
| `entropy_check.ps1` | 单文件 ≤200 行、孤儿 .meta 等熵巡检 |

## 完整运行方式

脚本按工作区布局写路径，需要同时存在：

```
<workspace>/
├─ 04_Project/
│  ├─ ShotField/      # 本仓库
│  └─ 巡检/            # 脚本权威位置
└─ 05_验证记录/        # 门禁日志落点
```

```powershell
# 本机 Unity 装在 D:\unity（不在 Program Files）时必须显式指定，否则脚本回退到 PATH 里的 "Unity.exe"，
# 可能命中同名的第三方 CLI，报「未知选项 -batchmode」。
$env:UNITY_EXE = "D:\unity\6000.6.5f1\Editor\Unity.exe"

# 全套（编辑器开着时走 live bridge 8765，否则 headless batchmode）
powershell -NoProfile -ExecutionPolicy Bypass -File "04_Project/巡检/verify_all.ps1"

# 单项
powershell -NoProfile -ExecutionPolicy Bypass -File "04_Project/巡检/verify_all.ps1" -Only e2e_pipeline,console_clean

# 凭据与熵
powershell -NoProfile -ExecutionPolicy Bypass -File "04_Project/巡检/secret_scan.ps1" -Targets resources,tracked -Strict
powershell -NoProfile -ExecutionPolicy Bypass -File "04_Project/巡检/entropy_check.ps1"
```

## 判据层级（严格，沉默不算通过）

- `PASS`：观察到 `<tag>=PASS`，且本次运行无 error 日志。
- `NO-ERROR`：无 tag、无错误（最弱一级，仍计为绿）。
- `NO-VERDICT`：验证器拒绝运行或没有输出 tag → 按失败处理。
- `FAIL`：出现 `<tag>=FAIL`，或运行期 Console 出现任何 error。

## 已知第三方污染

1. `[KitWright MCP Server] Failed to start HTTP transport`（2026-11-24 记录，当时 `Errors=1`）。开发期 MCP 工具与 gate bridge 争用 8765 端口，**不是产品运行时错误**，且该包不进 APK。2026-11-25 复跑时该条已不再出现（`Errors=0`）；是否在交付工程中移除该开发期包仍待项目负责人决定。
2. `PXRLog [Build Check]RegisterBuildPlayerHandler ...`（2026-11-25 记录，`Warnings=9`）。属 PICO Unity SDK 自身的 build-check 日志，与产品代码无关。`console_clean` 的判据是 `errors == 0 && warnings == 0`，因此该项在当前基线里**如实 FAIL**，不做白名单豁免。

以上两条都不进 APK。门禁宁可真红，不改成假绿：`console_clean` 不得加 `Clear()` 前置（清屏后读 `entries=0` 属恒真自证，2026-11-24 已回退）。

## 基线（2026-11-25）

`34 run / 33 PASS / 1 FAIL`，唯一红项是 `console_clean`（上述 PXRLog 警告）。证据日志 `05_验证记录/2026-11/gate_logs/verify_all_20261125_084727.log`。

`locomotion`、`stage_preset`、`mr_tabletop_config` 三项在 **headless batchmode** 下曾因主场景未加载而假红（脚本不主动开场景）；2026-11-25 已让后两者在 batchmode 下先 `OpenScene(SC01_Main)`，`locomotion` 本来就会自己开场景。判据未放宽。

## 口径红线

- `e2e_pipeline` 的 ② 是**离线契约**（不调网络），⑥ 是**导出规格/编码器在位**（真编码 `PENDING_APK`）。
- 不得把这两步当作「AI 真调用」或「MP4 导出跑通」的证据。
- 历史预置 mp4（如 `SC01_Final.mp4`）**不作验收**，文件名亦禁用 `final`。

## 编码与快照规矩（2026-11-24）

**编码契约**：一方维护的文本文件统一按以下口径写入；第三方 `venv/`、`uv-cache/` 不属于维护范围。

| 文件类型 | 编码 | 理由 |
| --- | --- | --- |
| `.py` / `.json` / `.md` / `.bat` | UTF-8 无 BOM | Python 3、编辑器与 Git 默认按 UTF-8 解读；BOM 可能破坏 JSON/脚本首行 |
| `.ps1` | 优先纯 ASCII；必须含中文时用 UTF-8 **带 BOM** | Windows PowerShell 5.1 对无 BOM 的非 ASCII 脚本按 ANSI 解码，会乱码甚至解析失败 |

新增或修改一方维护的文本文件后，必须严格 UTF-8 解码通过、无 NUL、无 U+FFFD；不得用 shell heredoc/重定向写中文，也不得把 GBK 字节混进 UTF-8 文件。历史损坏字符不得猜测补全。

**镜像快照纪律**：`04_Project/巡检/` 是权威源，本目录只供评审查看，**不是第二源码**。

1. 只在权威源修 bug；禁止直接修改本目录的五个镜像文件。
2. 权威源改动后，同一次提交内逐字节同步 `entropy_check.ps1`、`secret_scan.ps1`、`unity_gate.ps1`、`verifiers.map.json`、`verify_all.ps1`。
3. 提交前逐个比对源文件与镜像的字节数和 SHA256；五项全部一致才算同步，漂移即失败，不手改镜像掩盖。
4. 当前基线：五项全部一致；本次扫描证据见 `05_验证记录/2026-11/text_integrity_20261124.json`。
