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

## 已知第三方污染（2026-11-24）

`console_clean` 当前可能读到 1 条 error：`[KitWright MCP Server] Failed to start HTTP transport`。
根因是开发期 MCP 工具与 gate bridge 争用 8765 端口，**不是产品运行时错误**，且该包不进 APK。
处置需项目负责人决定是否在交付工程中移除此开发期 MCP 包；在此之前，`console_clean` 如实报 FAIL，不做白名单豁免。

## 口径红线

- `e2e_pipeline` 的 ② 是**离线契约**（不调网络），⑥ 是**导出规格/编码器在位**（真编码 `PENDING_APK`）。
- 不得把这两步当作「AI 真调用」或「MP4 导出跑通」的证据。
- 历史预置 mp4（如 `SC01_Final.mp4`）**不作验收**，文件名亦禁用 `final`。
