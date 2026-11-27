using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P1 链编排器：剧本文本 → AiScriptParser（配置接通且未选离线演示时走在线 DeepSeek，否则离线结构化）
    /// → 结构化提案 → 确认 UI 处置 → 经既有公开 API 落场景 → 生成时间轴分段 → 导出 JSON 清单。
    /// 在线失败/超时/不可解析抛 ScriptParseException，不静默降级成假提案。
    /// </summary>
    public partial class ChainOrchestrator : MonoBehaviour
    {
        public ChainProposalDto Proposal { get; private set; }
        public TimelineChainModel TimelineModel { get; private set; }
        public SceneContextDto LastSceneContext { get; private set; }

        public PropBrowserUI browser;
        public DirectorTimeline timeline;
        public Camera filmCamera;
        public Transform anchorTransform;

        private AiScriptParser _parser;
        private ProposalConfirmWindow _window;
        private SceneCommandPipeline _pipeline;

        public event Action<ChainProposalDto> OnProposalReady;
        public event Action<ChainApplyReport> OnApplied;
        public event Action<string> OnExported;
        public event Action<string> OnError;

        void Awake()
        {
            EnsureWired();
            _window = GetComponent<ProposalConfirmWindow>();
            if (_window == null) _window = gameObject.AddComponent<ProposalConfirmWindow>();
            _window.OnApplyRequested += () => ConfirmAndApply();
            _pipeline = GetComponent<SceneCommandPipeline>();
            if (_pipeline == null) _pipeline = gameObject.AddComponent<SceneCommandPipeline>();
            _pipeline.orchestrator = this;
            _pipeline.OnCompleted += HandlePipelineCompleted;
            if (GetComponent<WorkflowGuidePanel>() == null) gameObject.AddComponent<WorkflowGuidePanel>();
        }

        /// <summary>导入剧本文本并跑离线 mock 解析，产出待确认提案。</summary>
        public void ImportScript(string rawScript, string sourceInfo = "P1Chain")
        {
            if (string.IsNullOrWhiteSpace(rawScript))
            {
                Debug.LogWarning("[ChainOrchestrator] 剧本文本为空，未生成提案");
                return;
            }
            if (_pipeline != null && _pipeline.IsRunning)
            {
                OnError?.Invoke("请先完成或取消当前 AI 命令预览，再导入新剧本");
                return;
            }
            if (_parser == null) _parser = DeepSeekConfig.CreateParser(LabFeatureFlags.OfflineDemo);
            LastSceneContext = SceneContextCapture.Capture(filmCamera);
            var promptContext = SceneContextCapture.BuildPromptContext(LastSceneContext);

            ScriptAnalysisResult result;
            try
            {
                result = ScriptTaskRunner.RunSync(_parser.ParseScriptAsync(rawScript, promptContext));
            }
            catch (ScriptParseException ex)
            {
                Proposal = null;
                OnError?.Invoke($"AI 未接通：{ex.Message}（{ex.ErrorCode}）");
                Debug.LogWarning($"[ChainOrchestrator] 解析失败 {ex.ErrorCode}: {ex.Message}");
                return;
            }

            PublishProposal(ProposalModel.BuildFromAnalysis(result, rawScript), true);
        }

        private void PublishProposal(ChainProposalDto proposal, bool openDesktopWindow)
        {
            Proposal = proposal;
            if (_window != null)
            {
                _window.Proposal = Proposal;
                _window.Visible = openDesktopWindow;
            }
            OnProposalReady?.Invoke(Proposal);
        }

        /// <summary>把已接受提案送入四道闸队列；返回时只表示已入队，不代表已执行。</summary>
        public ChainApplyReport ConfirmAndApply() => ConfirmAndApply(true);

        public ChainApplyReport ConfirmAndApplyInTabletop() => ConfirmAndApply(false);

        private ChainApplyReport ConfirmAndApply(bool enterStudio)
        {
            var report = new ChainApplyReport();
            if (Proposal == null) return report;
            var tabletop = FindAnyObjectByType<TabletopDirector>();
            if (enterStudio && tabletop != null && tabletop.currentMode != TabletopDirector.ScaleMode.Immersive1to1)
                tabletop.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
            EnsureWired();
            foreach (var item in Proposal.items)
            {
                if (item == null) continue;
                if (item.IsAccepted) report.acceptedCount++;
                else if (item.decision == ProposalDecision.Rejected) report.rejectedCount++;
            }
            report.missingAssets.AddRange(Proposal.missingAssets);
            if (!_pipeline.Enqueue(Proposal, report))
                OnError?.Invoke("已有 AI 命令正在预览或执行，未重复入队");
            return report;
        }

        private void HandlePipelineCompleted(ChainApplyReport report)
        {
            var snapshot = _pipeline != null ? _pipeline.ProposalSnapshot : Proposal;
            TimelineModel = TimelineChainModel.FromProposal(snapshot);
            if (timeline != null) timeline.SetDuration(TimelineModel.TotalDuration);
            OnApplied?.Invoke(report);
        }

        /// <summary>导出 JSON 清单 + 镜头意图，返回落盘绝对路径。</summary>
        public string ExportPackage(string packageId = "chain_p1")
        {
            if (_pipeline != null && _pipeline.IsRunning)
            {
                OnError?.Invoke("AI 命令预览/执行尚未完成，暂不导出");
                return "";
            }
            var snapshot = _pipeline != null ? _pipeline.ProposalSnapshot : Proposal;
            if (snapshot == null || snapshot.items == null || snapshot.items.Count == 0)
            {
                OnError?.Invoke("没有可导出的镜头，请先拆解并确认剧本");
                return "";
            }
            if (TimelineModel == null) TimelineModel = TimelineChainModel.FromProposal(snapshot);
            if (TimelineModel == null || TimelineModel.Count == 0)
            {
                OnError?.Invoke("没有已接受的镜头，请先在提案中确认镜头");
                return "";
            }
            var manifest = ChainExporter.BuildManifest(TimelineModel, snapshot, packageId);
            string path = ChainExporter.SaveManifest(manifest);
            OnExported?.Invoke(path);
            return path;
        }

        private void OnDestroy()
        {
            if (_pipeline != null) _pipeline.OnCompleted -= HandlePipelineCompleted;
        }

        private void EnsureWired()
        {
            if (browser == null) browser = FindAnyObjectByType<PropBrowserUI>();
            if (timeline == null) timeline = FindAnyObjectByType<DirectorTimeline>();
            if (filmCamera == null)
            {
                var recorder = FindAnyObjectByType<CameraMotionRecorder>();
                filmCamera = recorder != null ? recorder.filmCamera : Camera.main;
            }
            if (_pipeline != null)
            {
                _pipeline.orchestrator = this; _pipeline.browser = browser;
                _pipeline.filmCamera = filmCamera; _pipeline.anchorTransform = anchorTransform;
            }
        }
    }
}
