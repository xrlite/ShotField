# 变更日志 · ShotField

本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/) 规范。
验证状态与门禁日志索引详见 [VERIFICATION.md](VERIFICATION.md)。

## [0.9.0-lab.20261124] - 2026-11-24

### 新增 (Added)
- **Phase 1: MR 透视沙盘桌面吸附与双尺度丝滑过渡**
  - 新增 `PicoPlaneTabletopAligner.cs`：对接 PICO Plane 空间水平面检测事件（`PlaneDetectionDataUpdated`）与 0.8m 距离 / 0.75m 高度虚拟创作台优雅回退机制。
  - 优化 `StudioFadeTransition.cs`：在 1:20 沙盘与 1:1 片场切换期间提供 FOV 呼吸过渡与空间坐标系缩放插值，杜绝瞬移与画面突变。
  - 扩充 `TabletopScaleVerifier.cs` 覆盖吸附提供者契约与回退断言（14/14 PASS）。
- **Phase 2: 运行时 GLB 骨骼模型加载器 (Runtime GLB Skeletal Importer)**
  - 实现 `RuntimeGltfImporter.cs`：解析 Tripo 生成的 .glb 模型，自动挂载 `SkinnedMeshRenderer`、局部几何缩放对齐的 `CapsuleCollider` 与启用 `Carving` 动态雕刻的 `NavMeshObstacle`；非骨骼道具自愈回退为 `BoxCollider`。
  - 扩充 `TripoRuntimeImportVerifier.cs` 至 7 项断言，覆盖骨骼胶囊体与动态雕刻避障（7/7 PASS）。
- **Phase 3: 六阶段端到端一镜到底全链路驱动器 (E2E Full Pipeline Runner)**
  - 实现 `E2EWorkflowRunner.cs`：完整串联 ①剧本导入解析 -> ②AI拆解与DeepSeek四道闸（Gate 1 Schema / Gate 2 Boundary / Gate 3 Ghost / Gate 4 Readback） -> ③资产对账与运行时GLB加载 -> ④沙盘推演与1:1双尺度过渡 -> ⑤斯坦尼康手持运镜与5轨剪辑 -> ⑥1080p广播级MP4导出准备。
  - 新增专用门禁断言器 `E2EPipelineVerifier.cs`，并在 `verifiers.map.json` 登记 `e2e_pipeline` 门禁（6/6 PASS）。
- **Phase 4: 大赛交付材料与工程治理**
  - 创建 `CHANGELOG.md`，完整记录四大阶段全链路演进与各里程碑。
  - 更新 `README.md`，完善 PICO Space Program 参赛交付（方案 A · 最佳沉浸内容创作赛道）与对外 Topics 标签。
  - 更新 `VERIFICATION.md`，索引 33 项全绿自动化门禁与全链路 E2E 证据。

### 修复与加固 (Fixed & Hardened)
- 修复 `PicoPlaneTabletopAligner` 未注册 PICO MR 平面检测回调与 Edit 模式下锚点位姿未即时同步的问题。
- 修复 `RuntimeGltfImporter` 在模型缩放时直接用世界 Bounds 导致 Collider 和 NavMeshObstacle 尺寸倍率放大的局部坐标换算缺陷。
- 补齐 `E2EWorkflowRunner` 在 Step 2 中漏检的 Gate 2 边界与合理性校验。
- 修复 `VERIFICATION.md` 索引版本提交哈希与提交链断层。
- 全量 323 个单文件严格守住 ≤200 行上限，0 孤儿 meta，Console 保持 0 error / 0 warning。
