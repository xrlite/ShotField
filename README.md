# 镜场 ShotField

镜场是在 VR 片场推演演员走位和摄影机调度的视觉预演（Previs）工具。创作者可以摆场、安排镜头、检查空间连续性，并导出参考素材。

## 打开工程

1. 安装项目记录的 Unity 版本 `6000.6.5f1`。Android 构建还需要 Unity Android Build Support、SDK、NDK 和 OpenJDK。
2. 通过 Unity Hub 打开仓库根目录，等待 Package Manager 完成依赖解析。
3. 打开 `Assets/Scenes/SC01_Final.unity` 检查室内预演场景。原 Android 构建入口仍使用 `Assets/Scenes/SC01_Blockout.unity`。
4. 从 `ShotField > Verifiers` 运行编辑器验证。编辑器结果、模拟器结果与头显实测应分别记录。

## 内容与状态

| 案例 | 内容 | 状态 |
| --- | --- | --- |
| A · 桌上那封信 | 室内、演员与桌边信封。计划用门洞缓推和桌边低角度横移展示走位与视线。 | 已有布景和编辑器预演，导演操作链路持续验收。 |
| B · 街巷追逃 | 街巷、五名演员、高空俯冲、贴地横移跟拍和巷口固定机位。 | 规划中。 |
| C · 宏大示意 | 楼群远景、一名演员和一次五秒长拉镜头。 | 规划中。 |

备件库规划为 54 件，含 12 件结构、10 件家具、20 件道具和 12 件风格件。现有资源和规划数量分别登记。发布版验收前，手势、眼追和真机帧率均须以设备证据为准。

## 工程结构

| 路径 | 内容 |
| --- | --- |
| `Assets/Scripts/Core` | 镜头数据、轨迹、保存、导出与联机基础 |
| `Assets/Scripts/Constraints` | 视线、空间和连续性校验 |
| `Assets/Scripts/RuntimeUI` | 工作台、演员与机位交互 |
| `Assets/Scripts/Editor` | 场景工具、抓图、构建和验证 |
| `Assets/Documentation/Previs` | 预演剧本、镜头表和视觉规范 |
| `Assets/Scenes`、`Assets/Prefabs` | 场景与可复用组件 |

## Android 构建

关闭使用同一工程的 Unity 编辑器，在仓库根运行。将 Unity 安装路径换成实际路径。

```powershell
$repo = (Get-Location).Path
$unity = 'D:\unity\6000.6.5f1\Editor\Unity.exe'
$env:SHOTFIELD_OUT_APK = Join-Path $repo 'Build/ShotField.apk'
New-Item -ItemType Directory -Path (Join-Path $repo 'Build') -Force | Out-Null
& $unity -batchmode -projectPath $repo `
  -executeMethod ShotField.EditorTools.BuildAndroid.BuildIl2Cpp `
  -logFile (Join-Path $repo 'Build/android.log')
```

现有入口使用 IL2CPP + ARM64，应用 ID 为 `com.haoyunlai.shotfield`。构建成功、Swan 运行和 PICO 真机体验分别验收。

## 展示内容与证据

| 维度 | 展示内容 |
| --- | --- |
| 创新 | 在 VR 中组织演员、机位和镜头意图，用连续性检查辅助预演 |
| 界面 | 将监视、镜头列表、质检、协作和时间轴放在空间工作台，现场以实际界面为准 |
| 交互 | 展示摆位、走位、机位记录与回放中的已验证操作 |
| 功能设计 | 展示可运行的案例、道具复用以及镜头参考导出 |
| 二次开发 | 通过源码、场景和文档说明如何扩展，引用外部资源时保留对应许可 |

## 许可证与来源

ShotField 团队拥有版权的原创代码与文档适用 [MIT](LICENSE)。Unity Starter Assets、PICO SDK、KitWright MCP 和其他第三方内容适用其各自的许可，发布前应核对对应版本的授权和声明。Poly Haven 家具为 CC0 来源资产。