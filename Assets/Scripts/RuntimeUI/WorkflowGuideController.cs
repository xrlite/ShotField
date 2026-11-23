using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P1 引导旅程驱动层：把 Core 纯状态机 <see cref="WorkflowCoordinator"/> 接到既有
    /// <see cref="ChainOrchestrator"/> / <see cref="SpatialWorkbench"/> 的真实信号上。
    /// 阶段前进只由真实完成条件触发；判据不足的阶段在状态文案里如实标注临时口径，不假装有。
    /// 本类只做状态绑定与判定，场景交互仍由既有编排器/工作台承接；状态文案见分部 Status。
    /// </summary>
    public sealed partial class WorkflowGuideController : MonoBehaviour
    {
        private readonly WorkflowCoordinator _coordinator = new WorkflowCoordinator();

        public ChainOrchestrator Orchestrator { get; private set; }
        public SpatialWorkbench Workbench { get; private set; }
        public ChainTimelineController TimelineChain { get; private set; }

        public ChainApplyReport LastReport { get; private set; }
        public string LastExportPath { get; private set; }
        public string LastError { get; private set; }
        public string DraftScript { get; set; } = "";

        public WorkflowStage Stage => _coordinator.CurrentStage;
        public bool IsFinished => _coordinator.IsFinished;

        private void Awake()
        {
            Orchestrator = FindAnyObjectByType<ChainOrchestrator>();
            Workbench = FindAnyObjectByType<SpatialWorkbench>();
            TimelineChain = FindAnyObjectByType<ChainTimelineController>();
            if (Orchestrator == null) return;

            Orchestrator.OnProposalReady += _ => LastError = null;
            Orchestrator.OnApplied += report => LastReport = report;
            Orchestrator.OnExported += path => LastExportPath = path;
            Orchestrator.OnError += msg => LastError = msg;
        }

        private void OnDestroy()
        {
            if (Orchestrator == null) return;
            Orchestrator.OnProposalReady -= _ => LastError = null;
            Orchestrator.OnApplied -= report => LastReport = report;
            Orchestrator.OnExported -= path => LastExportPath = path;
            Orchestrator.OnError -= msg => LastError = msg;
        }

        /// <summary>当前阶段是否满足真实完成条件；不满足时按钮不得前进。</summary>
        public bool StageComplete
        {
            get
            {
                switch (Stage)
                {
                    case WorkflowStage.Import: return HasProposal();
                    case WorkflowStage.Proposal: return LastReport != null && LastReport.acceptedCount > 0;
                    case WorkflowStage.Assets: return HasLanding();
                    case WorkflowStage.Rehearsal: return HasTimeline();
                    case WorkflowStage.Edit: return Workbench != null && Workbench.library != null && Workbench.library.Count > 0;
                    case WorkflowStage.Export: return !string.IsNullOrEmpty(LastExportPath);
                    default: return false;
                }
            }
        }

        private bool HasProposal()
        {
            return Orchestrator != null && Orchestrator.Proposal != null
                && Orchestrator.Proposal.items != null && Orchestrator.Proposal.items.Count > 0;
        }

        private bool HasLanding()
        {
            if (LastReport == null) return false;
            return LastReport.placedPropIds.Count + LastReport.placedActorIds.Count > 0 || LastReport.cameraApplied;
        }

        private bool HasTimeline()
        {
            return Orchestrator != null && Orchestrator.TimelineModel != null
                && Orchestrator.TimelineModel.Count > 0 && Orchestrator.TimelineModel.TotalDuration > 0f;
        }

        public void ImportDraft()
        {
            LastError = null;
            if (string.IsNullOrWhiteSpace(DraftScript)) { LastError = "先粘贴或输入剧本"; return; }
            if (Orchestrator == null) { LastError = "未找到 ChainOrchestrator"; return; }
            Orchestrator.ImportScript(DraftScript, "GuidePanel");
        }

        public void ApplyProposal() => Orchestrator?.ConfirmAndApply();

        public void PlayRehearsal()
        {
            if (Orchestrator?.TimelineModel == null) return;
            if (TimelineChain != null) TimelineChain.Bind(Orchestrator.TimelineModel);
            TimelineChain?.Play();
        }

        public void CaptureShot() => Workbench?.CaptureCurrentViewToShot();

        public void ExportPackage()
        {
            if (Orchestrator == null) { LastError = "未找到 ChainOrchestrator"; return; }
            LastExportPath = Orchestrator.ExportPackage("guide_p1");
        }

        /// <summary>仅在完成条件满足时前进；否则原地不动并保留当前状态文案。</summary>
        public void TryAdvance()
        {
            if (StageComplete) _coordinator.Advance();
        }

        public void ResetJourney()
        {
            _coordinator.Reset();
            LastReport = null;
            LastExportPath = null;
            LastError = null;
            DraftScript = "";
        }
    }
}
