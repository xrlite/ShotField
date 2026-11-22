# PalmMenu 掌心菜单 · 集成说明（Staging）

> 状态：**编译 pending-集成**。本目录是暂存代码，尚未放进 `Assets/`，未经过 Unity 编译；
> 已逐文件静态自查（using、命名空间、公开 API 签名对齐现有源码）。集成进工程后才算可验证。
> 设计真源：`03_制作/产品定义冻结_四支柱与交互裁定_20261121.md` §2.2「掌心菜单 + 捏合选择」。

## 1. 文件清单与将来落点

集成后全部放入 `Assets/Scripts/RuntimeUI/PalmMenu/`，归入现有程序集
`ShotField.RuntimeUI`（**不新增 asmdef**；该 asmdef 已引用 Core / XRI / InputSystem / UI / PICO XR）。
命名空间统一 `ShotField.RuntimeUI`，类名与文件名一致。

| 暂存文件 | 将来路径 | 职责 | 行数 |
|---|---|---|---|
| `PalmMenuModels.cs` | `RuntimeUI/PalmMenu/PalmMenuModels.cs` | 动作类型枚举、分栏枚举、菜单项数据模型 | 30 |
| `PalmMenuState.cs` | `RuntimeUI/PalmMenu/PalmMenuState.cs` | 核心状态机：呼出/收起、分栏、条目构建、命令分发（纯逻辑，不碰呈现） | 173 |
| `PalmMenuSnapshotService.cs` | `RuntimeUI/PalmMenu/PalmMenuSnapshotService.cs` | 快照落盘/重放：DTO 组装、演员 TryPlace 重放、道具回收重放 | 93 |
| `PalmMenuHaptics.cs` | `RuntimeUI/PalmMenu/PalmMenuHaptics.cs` | 手柄触感封装：呼出/收起/选中/确认四档脉冲，控制器缺失静默降级 | 39 |
| `PalmMenuController.cs` | `RuntimeUI/PalmMenu/PalmMenuController.cs` | VR 呈现层：世界空间 Canvas 跟随左手、朝向锁定头部、建按钮、接触感 | 195 |
| `PalmMenuDesktopFallback.cs` | `RuntimeUI/PalmMenu/PalmMenuDesktopFallback.cs` | 桌面回退：无头显时 Tab 弹出同一状态机的 IMGUI 面板 | 95 |

## 2. 与冻结裁定 §2.2 的逐条对应

| 冻结裁定 | 本模块实现 | 边界 / pending |
|---|---|---|
| 抬起非惯用手，掌心朝向自己 | `menuAnchor` 挂左手（手腕/手柄）变换点 | **掌心朝向识别未实现**：手追姿态阈值按裁定必须设备测试后写死，本期不做 |
| 保持短暂稳定，呼出贴近手腕的主菜单 | `localOffset` 贴近手腕；`followLerp` 位置平滑防抖；`headAnchor` 锁定朝向，菜单不随手腕翻滚 | 稳定时长去抖阈值待真机标定 |
| 另一只手捏合选择摄影机/角色/道具/灯光 | 右手 XRRayInteractor + 扳机点选世界空间 UI 按钮（XRI `uiPressInput`，工程既有绑定） | 裸手捏合选择待手追接入；本期手柄扳机路径 |
| 开始抓取或拍摄时菜单自动收起 | `Execute` 返回 `closesAfterExecute=true`（选人/选道具）后 `Close()` | — |
| 无手部追踪时，用手柄菜单键打开**同一个界面** | 左控制器菜单键 InputAction 呼出；VR 与桌面回退共用同一个 `PalmMenuState` | 菜单键控制路径见 §5 存疑 2 |

菜单覆盖的功能（对齐 `DesktopStudioEditView` 右侧面板的 VR 等价物）：

- **选演员**：演员分栏，条目来自 `ActorSelection.All`，选中走 `ActorSelection.Select`；六色演员在场景内登记后自动出现。
- **加道具**：道具分栏，条目来自 `PropLibrary.All`（18 条），生成走 `PropBrowserUI.SpawnProp(id)`。
- **保存快照 / 重载**：走 `SceneEditStateStore.Save/Load`，演员重放走 `ActorNavigator.TryPlace`。
- **1:20 沙盘 ↔ 1:1 片场**：走 `TabletopDirector.SwitchMode`，标签按当前模式切换。

## 3. 场景装配（集成时在 Editor 里挂）

### 3.1 VR 路径 —— `PalmMenuController`

挂到 XROrigin 下左手控制器子物体（或左手腕挂载点）上。序列化引用：

| 字段 | 接什么 | 留空行为 |
|---|---|---|
| `menuAnchor` | 左手腕/左手柄 Transform | 留空则不跟随（菜单静止在本物体） |
| `headAnchor` | 头部（XR Head / MainCamera） | 留空则朝向跟随锚点，不做头部锁定 |
| `menuCanvas` | 世界空间 Canvas（World Space），挂菜单面板 | 留空用本物体 transform；`canvas.enabled` 控制显隐 |
| `buttonRoot` | Canvas 下按钮容器 RectTransform | 留空不建按钮 |
| `buttonPrefab` | 菜单按钮 prefab（含 `Button` + 子物体 `Text`） | 留空不建按钮 |
| `labelChildName` | prefab 内文字子物体名，默认 `Label` | 找不到则取 `GetComponentInChildren<Text>()` |
| `selectRay` | 右手射线 Interactor（`XRRayInteractor`） | 仅作引用保留；点选走 XRI UI 射线，悬停触感后续接 |
| `leftController` / `rightController` | 左右 `XRBaseController`（ActionBasedController） | 缺失时触感静默降级，不报错 |
| `browser` | 场景内 `PropBrowserUI` | Awake 自动 `FindAnyObjectByType` |
| `tabletop` | 场景内 `TabletopDirector` | Awake 自动 `FindAnyObjectByType` |

Canvas 要求：`Render Mode = World Space`，挂载 `Graphic Raycaster`；按钮 prefab 需可被射线命中。

### 3.2 桌面回退 —— `PalmMenuDesktopFallback`

挂到场景任意常驻物体上（与 VR 控制器可并存；无头显时只靠它）。

| 字段 | 接什么 | 留空行为 |
|---|---|---|
| `browser` | `PropBrowserUI` | Awake 自动查找 |
| `tabletop` | `TabletopDirector` | Awake 自动查找 |

呼出/收起：键盘 **Tab**（新输入系统 `Keyboard.current.tabKey`，不用旧遗留输入 API）。面板固定在屏幕左上角 280×360，避开既有右侧编辑面板。

## 4. 验证步骤（集成后执行）

### 4.1 编辑器（PC 无头显）

前置：场景里已有 `PropBrowserUI`（prefab 绑定齐全）、`ActorSelection` 登记了六色演员、`TabletopDirector`。

1. Play 进入场景，按 **Tab**：左上角弹出「掌心菜单（PC 回退）」，默认停在「演员」栏。
2. 演员栏：六个在场演员各一条，当前选中带 `●`；点另一条 → 状态行显示「已选中: …」，菜单收起。
3. 道具栏：列出 `PropLibrary` 全部条目；点一条 → 状态行「已放置: …」，道具出现在浏览器锚点前 1.2m，菜单收起。
4. 场景栏：点「保存快照」→ 状态行「已保存: 演员 n / 道具 m」；移动/销毁后点「重新加载存档」→ 演员回到存档位（走 TryPlace，不可行走的落点不强行搬），道具按存档重放。
5. 点「进入 1:20 沙盘 / 回到 1:1 片场」→ 状态行显示 `TabletopDirector.LastModeNote`，沙盘代理显隐切换。
6. 再按 Tab 收起；反复呼出/收起无残留按钮、无重复登记。

### 4.2 PICO 模拟器（Swan x86_64 / PICO OS 6）

1. 同一场景挂 `PalmMenuController`，接好左手锚点、头部、世界 Canvas、右手射线、左右控制器引用。
2. 左手菜单键呼出菜单：菜单贴左手腕出现，位置随手平滑跟随，朝向始终面向头部（手腕翻滚时菜单不跟着转）。
3. 右手射线对准按钮、扳机点选：演员/道具条目执行后菜单自动收起；场景栏命令执行后菜单保留。
4. 呼出/选中/确认时左右手柄分别有脉冲触感；模拟器无触觉回传时不应报错或卡顿。
5. 模拟器无头显交互路径下，`PalmMenuDesktopFallback` 的 Tab 路径仍应可用作对照。

> 真机项（全沉浸立体首帧、90fps、裸手捏合、实体扳机手感）一律 **pending-真机**，不得在模拟器取证后宣称已验证。

## 5. 存疑点（集成时逐条裁定）

1. **编译 pending-集成**：暂存代码未进 `Assets/`，未经 Unity 编译与门禁冒烟；静态自查覆盖 using、命名空间、对现有公开 API 的签名调用。
2. **菜单键控制路径**：`menuPressed` 为主、`primaryButtonPressed` 兜底写在 `PalmMenuController.Awake`。PICO 控制器菜单键在 OpenXR 下的实际控制名需在模拟器/真机 Input Debugger 核验；路径不解析时在这一行改绑（候选：`secondaryButtonPressed`、`gripPressed`）。
3. **手追掌心朝向 + 捏合**：本期未接 PICO hand tracking 姿态判定。冻结裁定明确阈值与误触发判定必须设备测试后写死；`menuAnchor` 变换点已预留，后续接 `PXR_HandTracking` 腕关节位姿时替换锚点来源即可，状态机不用改。
4. **道具注册表两份**：本状态机自登记 `_placedProps`；`DesktopStudioEditView` 私有 `placedProps` 是另一份。跨面板生成的道具互不可见（与既有已知限制一致）；集成时若要统一，应把道具登记上提到一个全局注册表，不在本模块内私造第二份长期方案。
5. **快照逻辑重复**：`PalmMenuSnapshotService` 的 DTO 组装/重放与 `DesktopStudioEditView.SaveSnapshot/RestoreFromDisk` 同构。后者保存方法为私有、本任务不许改既有文件；待其提为 public 后两处应合并到一处。
6. **UI 资产不在本模块**：按钮 prefab、Canvas 背景、字体均由场景装配提供；本模块只负责运行时实例化与接线。
7. **`selectRay` 暂未被代码读取**：保留为右手射线引用位，供后续悬停高亮/悬停触感接入；当前点选完全走 XRI 既有 UI 射线链路。

## 6. 不许做的事（集成时复核）

- 不改 `ActorSelection` / `ActorNavigator` / `PropBrowserUI` / `PropLibrary` / `SceneEditStateStore` / `TabletopDirector` 的既有签名。
- 不引入旧遗留输入 API；输入只走 Input System。
- 单文件保持 ≤200 行；新增职责先拆文件。
- 暂存阶段不写 `Assets/`、不改场景、不动 `Staging/P1Chain/`、不碰验证器。
