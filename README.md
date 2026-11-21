# 镜场 ShotField

**面向 AI 影视创作者的多人空间导演与虚拟制片平台。**

[快速开始](#快速开始) · [案例与进度](#案例与进度) · [预演文档](#预演文档) · [源码结构](#源码结构) · [Android 构建](#android-构建) · [已知限制](#已知限制) · [许可证](#许可证与来源)

![License](https://img.shields.io/badge/license-MIT-green) ![Unity](https://img.shields.io/badge/Unity-6000.6.5f1-blue) ![Platform](https://img.shields.io/badge/target-PICO%20Android%20ARM64-orange)

创作者可以进入共享虚拟片场，通过身体、手势、摄影机和演员表演共同设计镜头。系统记录镜头意图、空间关系和团队决策，检查连续性并输出可复用的影视参考资料，再借助视频生成模型形成最终画面。创作并不在生成完成时结束——导演还可以邀请观众试看，通过反馈和审片记录持续修改作品。

产品由四个不可互相替代的支柱构成。**下表同时标明实现状态，未实现的不能当作现有能力宣传**：

| 支柱 | 内容 | 当前状态 |
| --- | --- | --- |
| **多人共创** | 导演、演员、制片人、观众进入同一项目空间；联机不是额外按钮，而是制作方式 | 局域网会话有代码、未联调；房间码、双端同看一镜、远端位置光标未做 |
| **空间导演** | 身体即摄影机、运镜路径录制、俯视沙盘、人物表演、实时监视器；VR 为主，PC 同等可用 | 机位摆放、走位、沙盘有代码；撤销栈与自动保存未做 |
| **意图可控** | 镜头护照、越轴/遮挡/构图检查、故事圣经、导演审批、生成结果对照 | 护照与三类约束检查已验证；故事圣经与生成对照表未做 |
| **观众反馈与 AI 生产** | 剧本辅助、资产生成、视频生成、观众试看与反馈、据此重改 | 白模帧导出已验证；运镜视频、导出包、观众模式未做 |

| 模块 | 仓库入口 |
| --- | --- |
| 镜头数据、联机与导出 | [Core](Assets/Scripts/Core) |
| 镜头连续性与空间约束 | [Constraints](Assets/Scripts/Constraints) |
| 导演工作台、演员与相机 | [RuntimeUI](Assets/Scripts/RuntimeUI) |
| 场景工具、构建与验证 | [Editor](Assets/Scripts/Editor) |

## 快速开始

```bash
git clone https://github.com/xrlite/ShotField.git
cd ShotField
```

1. 安装项目记录的 Unity 版本 `6000.6.5f1`。Android 构建还需要 Unity Android Build Support、SDK、NDK 和 OpenJDK。
2. 通过 Unity Hub 打开仓库根目录，等待 Package Manager 完成依赖解析。
3. 打开 `Assets/Scenes/SC01_Final.unity` 检查室内预演场景。原 Android 构建入口仍使用 `Assets/Scenes/SC01_Blockout.unity`。
4. 从 `ShotField > Verifiers` 运行编辑器验证。编辑器结果、模拟器结果与头显实测应分别记录。

## 案例与进度

| 案例 | 内容 | 状态 |
| --- | --- | --- |
| A · 桌上那封信 | 室内、演员与桌边信封。计划用门洞缓推和桌边低角度横移展示走位与视线。 | 已有布景和编辑器预演，导演操作链路持续验收。 |
| B · 街巷追逃 | 街巷、五名演员、高空俯冲、贴地横移跟拍和巷口固定机位。 | 规划中。 |
| C · 宏大示意 | 楼群远景、一名演员和一次五秒长拉镜头。 | 规划中。 |

备件库规划为 54 件，含 12 件结构、10 件家具、20 件道具和 12 件风格件。现有资源和规划数量分别登记。发布版验收前，手势、眼追和真机帧率均须以设备证据为准。

## 预演文档

案例 A《桌上那封信》的创作规格随功能一起提交，说明「输入什么、演出什么、验收什么」：

| 文档 | 内容 |
| --- | --- |
| [SC01_Previs_Script.md](Assets/Documentation/Previs/SC01_Previs_Script.md) | 预演剧本：人物、动机与 60 秒节拍 |
| [SC01_ShotList.md](Assets/Documentation/Previs/SC01_ShotList.md) | 七组分镜的机位、焦段与运镜规格 |
| [SC01_VisualPalette.md](Assets/Documentation/Previs/SC01_VisualPalette.md) | 视觉语言：景物两级灰、角色分色、机位琥珀色 |

## 源码结构

| 路径 | 内容 |
| --- | --- |
| `Assets/Scripts/Core` | 镜头数据、轨迹、保存、导出与联机基础 |
| `Assets/Scripts/Constraints` | 视线、空间和连续性校验 |
| `Assets/Scripts/RuntimeUI` | 工作台、演员与机位交互 |
| `Assets/Scripts/Editor` | 场景工具、抓图、构建和验证 |
| `Assets/Documentation/Previs` | 工作区中的预演剧本、镜头表与视觉规范，随对应功能提交 |
| `Assets/Scenes`、`Assets/Prefabs` | 场景与可复用组件 |

## Android 构建

关闭使用同一工程的 Unity 编辑器，在仓库根运行。将 Unity 安装路径换成实际路径。

```powershell
# 先把 X: 换成你自己的 Unity 安装位置（Unity Hub 里能看到）
$repo = (Get-Location).Path
$unity = 'X:\Unity\6000.6.5f1\Editor\Unity.exe'
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

## PICO Space Program 参赛交付

参赛入口：[PICO Space Program 中国区](https://spaceprogram-cn.picoxr.com/)。

按中国区赛事快照，参赛须关联 GitHub 源码仓库。私有仓库通过赛事流程配置 Deploy Key，开源并支持二次开发属于完成度加分项。评审版本应对应具体提交，验证记录应标明 Unity 编辑器、Swan 模拟器或真机。

## 已知限制

工程验证期的诚实清单，逐条对应证据状态：

- 编辑器验证（`ShotField > Verifiers`）与 Swan 模拟器结果分开记录；全沉浸立体首帧、90fps、手柄交互仍待 PICO 真机验收。
- 手势追踪、眼动追踪、面捕在模拟器上不可验证，相关能力按真机可用范围实现与宣传。
- 场景资产目前存在两个入口（`SC01_Final` 为预演主场景，Android 构建入口尚未切换），统一到单一场景前，APK 内容以构建脚本指向的场景为准。
- 编辑器结果截图与日志不进版本库，随提交材料单独提供。

## 许可证与来源

ShotField 团队拥有版权的原创代码与文档适用 [MIT](LICENSE)。Unity Starter Assets、PICO SDK、KitWright MCP 和其他第三方内容适用其各自的许可，发布前应核对对应版本的授权和声明。Poly Haven 家具为 CC0 来源资产。