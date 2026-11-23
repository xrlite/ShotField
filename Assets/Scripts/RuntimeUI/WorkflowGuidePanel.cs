using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P1 引导旅程桌面 IMGUI 面板：首进只呈现「导入剧本」一个动作；每个阶段突出单一下一步，
    /// 监看器/时间轴、既有桌面工具、联机高级参数、Labs 全部按需呼出，不常驻占屏。
    /// 本面板属开发/桌面验证工具；正式产品界面走空间 Canvas。阶段前进由
    /// <see cref="WorkflowGuideController"/> 的真实完成条件驱动，不设跳过按钮。
    /// </summary>
    public sealed class WorkflowGuidePanel : MonoBehaviour
    {
        private const float PanelW = 380f;

        private WorkflowGuideController _controller;
        private bool _showLabs;
        private bool _showAdvanced;

        private void Awake()
        {
            _controller = GetComponent<WorkflowGuideController>();
            if (_controller == null) _controller = gameObject.AddComponent<WorkflowGuideController>();
        }

        private void OnGUI()
        {
            float panelH = Mathf.Min(Screen.height - 24f, 780f);
            GUILayout.BeginArea(new Rect(12f, 12f, PanelW, panelH), GUI.skin.box);

            DrawHeader();
            DrawRail();
            DrawBody();
            DrawSecondary();

            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.Label("镜场 ShotField · 桌面开发工具（IMGUI）", GUI.skin.label);
            GUILayout.Label($"{LabFeatureFlags.ChannelLabel} {LabFeatureFlags.VersionLabel} · 产品界面为空间 Canvas，本面板用于桌面验证");
        }

        private void DrawRail()
        {
            GUILayout.BeginHorizontal();
            int current = (int)_controller.Stage;
            for (int i = 0; i < WorkflowGuideController.StageLabels.Length; i++)
            {
                bool done = i < current;
                bool active = i == current;
                GUI.color = done ? new Color(0.6f, 1f, 0.6f) : active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Button((done ? "✓ " : "") + WorkflowGuideController.StageLabels[i], GUILayout.Height(22f));
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawBody()
        {
            GUILayout.Space(4f);
            GUILayout.Label(WorkflowGuideController.StageLabels[(int)_controller.Stage], GUI.skin.label);
            GUILayout.Label(_controller.StageStatus);

            DrawStageControls();

            if (!string.IsNullOrEmpty(_controller.StageCriterionNote))
            {
                GUI.color = new Color(0.85f, 0.8f, 0.5f);
                GUILayout.Label(_controller.StageCriterionNote);
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(_controller.LastError))
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                GUILayout.Label(_controller.LastError);
                GUI.color = Color.white;
            }

            DrawAdvance();
        }

        private void DrawStageControls()
        {
            switch (_controller.Stage)
            {
                case WorkflowStage.Import:
                    GUILayout.Label("把剧本或剧情台词粘贴到下方，生成镜头提案。");
                    _controller.DraftScript = GUILayout.TextArea(_controller.DraftScript ?? "", GUILayout.Height(72f));
                    if (GUILayout.Button("导入剧本并生成提案")) _controller.ImportDraft();
                    break;
                case WorkflowStage.Proposal:
                    GUILayout.Label("在左侧提案窗口逐条接受 / 拒绝，再回到这里应用已接受条目。");
                    if (GUILayout.Button("应用已接受的提案")) _controller.ApplyProposal();
                    break;
                case WorkflowStage.Assets:
                    GUILayout.Label("提案条目已落场；核对道具、演员与机位结果后进入排练。");
                    break;
                case WorkflowStage.Rehearsal:
                    if (GUILayout.Button("播放走位预览")) _controller.PlayRehearsal();
                    break;
                case WorkflowStage.Edit:
                    if (GUILayout.Button("捕获当前机位为分镜")) _controller.CaptureShot();
                    break;
                case WorkflowStage.Export:
                    if (!_controller.IsFinished && GUILayout.Button("导出镜头包 JSON")) _controller.ExportPackage();
                    break;
            }
        }

        private void DrawAdvance()
        {
            if (_controller.Stage == WorkflowStage.Export) return;
            var next = WorkflowGuideController.StageLabels[(int)_controller.Stage + 1];
            GUI.enabled = _controller.StageComplete;
            if (GUILayout.Button($"下一步：{next}")) _controller.TryAdvance();
            GUI.enabled = true;
        }

        private void DrawSecondary()
        {
            GUILayout.Space(6f);
            DevToolPanels.MonitorFeedVisible = GUILayout.Toggle(DevToolPanels.MonitorFeedVisible, "监看器 / 时间轴画面（按需呼出）");
            DevToolPanels.LegacyDesktopVisible = GUILayout.Toggle(DevToolPanels.LegacyDesktopVisible, "桌面开发工具（既有工作室 / 编辑面板）");
            DevToolPanels.MonitorScalerVisible = GUILayout.Toggle(DevToolPanels.MonitorScalerVisible, "监看板缩放滑条（按需呼出）");
            DevToolGate.ExportRecorderVisible = GUILayout.Toggle(DevToolGate.ExportRecorderVisible, "成片视频导出小面板（开发工具）");

            _showAdvanced = GUILayout.Toggle(_showAdvanced, "更多设置（联机 / 高级参数）");
            if (_showAdvanced)
            {
                GUILayout.Label("联机与会话参数收在既有桌面开发工具内，主线旅程不展开。");
                DevToolPanels.LightingConsoleVisible = GUILayout.Toggle(DevToolPanels.LightingConsoleVisible, "灯光控制台（高级参数，按需呼出）");
            }

            _showLabs = GUILayout.Toggle(_showLabs, "Labs 实验功能（MR 透视 / 即梦在线生成）· 与主线分离");
            if (_showLabs)
            {
                GUILayout.Label("主线旅程不经过 Labs；实验开关与限制说明见既有桌面开发工具。");
                GUILayout.Label(LabFeatureFlags.MrLabLimitation);
                GUILayout.Label(LabFeatureFlags.OnlineLabLimitation);
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("重置旅程")) _controller.ResetJourney();
        }
    }
}
