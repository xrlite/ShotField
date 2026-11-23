using System.Text;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// <see cref="WorkflowGuideController"/> 分部：阶段标签与状态文案、临时判据说明。
    /// 只做只读文本拼装，不改动状态；从主类拆出以守单类 150 行红线。
    /// </summary>
    public sealed partial class WorkflowGuideController
    {
        public static readonly string[] StageLabels =
            { "导入剧本", "提案确认", "资产落场", "排练预演", "剪辑质检", "导出镜头包" };

        /// <summary>当前阶段状态一句话（真实读数），供引导面板展示。</summary>
        public string StageStatus => BuildStatus();

        private string BuildStatus()
        {
            switch (Stage)
            {
                case WorkflowStage.Import:
                    return HasProposal() ? $"提案已生成：{Orchestrator.Proposal.sceneTitle}" : "等待导入剧本";
                case WorkflowStage.Proposal:
                    return LastReport != null ? $"已接受 {LastReport.acceptedCount} 条，拒绝 {LastReport.rejectedCount} 条" : "在提案窗口逐条确认后应用";
                case WorkflowStage.Assets: return BuildLandingStatus();
                case WorkflowStage.Rehearsal: return HasTimeline()
                    ? $"时间轴 {Orchestrator.TimelineModel.Count} 段 / {Orchestrator.TimelineModel.TotalDuration:F1}s" : "时间轴分段尚未生成";
                case WorkflowStage.Edit: return Workbench?.library != null ? $"已捕获分镜 {Workbench.library.Count} 个" : "尚未捕获机位分镜";
                case WorkflowStage.Export: return !string.IsNullOrEmpty(LastExportPath) ? $"已导出：{LastExportPath}" : "尚未导出镜头包";
                default: return "";
            }
        }

        private string BuildLandingStatus()
        {
            if (LastReport == null) return "等待应用提案";
            var sb = new StringBuilder();
            sb.Append($"道具 {LastReport.placedPropIds.Count} · 演员 {LastReport.placedActorIds.Count}");
            if (LastReport.cameraApplied) sb.Append(" · 机位已设");
            if (LastReport.placementFailures.Count > 0) sb.Append($" · 失败 {LastReport.placementFailures.Count}");
            return sb.ToString();
        }

        /// <summary>临时判据如实标注：哪些阶段目前用什么口径完成，避免假装已做真校验。</summary>
        public string StageCriterionNote => Stage switch
        {
            WorkflowStage.Rehearsal => "临时判据：时间轴分段已生成且总时长 > 0；走位/表演回放需在开发工具内手动触发。",
            WorkflowStage.Edit => "临时判据：已在工作台捕获至少一个机位分镜；约束质检以工作台保存护照时为准。",
            WorkflowStage.Assets => "落场口径：提案条目已落到场景（道具/演员/机位任一成功）；缺资产见失败清单。",
            _ => ""
        };
    }
}
