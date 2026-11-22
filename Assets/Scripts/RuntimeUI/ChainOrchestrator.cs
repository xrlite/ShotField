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
        private const float SampleRadius = 0.6f;

        public ChainProposalDto Proposal { get; private set; }
        public TimelineChainModel TimelineModel { get; private set; }

        public PropBrowserUI browser;
        public DirectorTimeline timeline;
        public Camera filmCamera;
        public Transform anchorTransform;

        private AiScriptParser _parser;
        private ProposalConfirmWindow _window;

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
        }

        /// <summary>导入剧本文本并跑离线 mock 解析，产出待确认提案。</summary>
        public void ImportScript(string rawScript, string sourceInfo = "P1Chain")
        {
            if (string.IsNullOrWhiteSpace(rawScript))
            {
                Debug.LogWarning("[ChainOrchestrator] 剧本文本为空，未生成提案");
                return;
            }
            if (_parser == null) _parser = DeepSeekConfig.CreateParser(LabFeatureFlags.OfflineDemo);

            ScriptAnalysisResult result;
            try
            {
                result = ScriptTaskRunner.RunSync(_parser.ParseScriptAsync(rawScript));
            }
            catch (ScriptParseException ex)
            {
                Proposal = null;
                OnError?.Invoke($"AI 未接通：{ex.Message}（{ex.ErrorCode}）");
                Debug.LogWarning($"[ChainOrchestrator] 解析失败 {ex.ErrorCode}: {ex.Message}");
                return;
            }

            Proposal = ProposalModel.BuildFromAnalysis(result, rawScript);

            if (_window != null)
            {
                _window.Proposal = Proposal;
                _window.Visible = true;
            }
            OnProposalReady?.Invoke(Proposal);
        }

        /// <summary>把提案中已接受的条目落到场景，再生成时间轴分段。</summary>
        public ChainApplyReport ConfirmAndApply()
        {
            var report = new ChainApplyReport();
            if (Proposal == null) return report;
            EnsureWired();

            foreach (var item in Proposal.items)
            {
                if (item == null) continue;
                if (!item.IsAccepted)
                {
                    if (item.decision == ProposalDecision.Rejected) report.rejectedCount++;
                    continue;
                }
                report.acceptedCount++;
                ApplyItem(item, report);
            }

            TimelineModel = TimelineChainModel.FromProposal(Proposal);
            if (timeline != null) timeline.SetDuration(TimelineModel.TotalDuration);
            report.missingAssets.AddRange(Proposal.missingAssets);

            OnApplied?.Invoke(report);
            return report;
        }

        /// <summary>导出 JSON 清单 + 镜头意图，返回落盘绝对路径。</summary>
        public string ExportPackage(string packageId = "chain_p1")
        {
            if (TimelineModel == null) TimelineModel = TimelineChainModel.FromProposal(Proposal);
            var manifest = ChainExporter.BuildManifest(TimelineModel, Proposal, packageId);
            string path = ChainExporter.SaveManifest(manifest);
            OnExported?.Invoke(path);
            return path;
        }

        private void ApplyItem(ProposalItemDto item, ChainApplyReport report)
        {
            switch (item.kind)
            {
                case ProposalItemKind.Prop: ApplyProp(item, report); break;
                case ProposalItemKind.Actor: ApplyActor(item, report); break;
                default: ApplyCamera(item, report); break;
            }
        }

        private void ApplyProp(ProposalItemDto item, ChainApplyReport report)
        {
            if (browser == null)
            {
                report.placementFailures.Add($"道具 {item.propId}：未找到 PropBrowserUI");
                return;
            }
            var instance = browser.SpawnProp(item.propId, item.position.ToUnityVector3());
            if (instance == null) report.placementFailures.Add($"道具 {item.propId}：未绑定资产或放置失败");
            else report.placedPropIds.Add(item.propId);
        }

        private void ApplyCamera(ProposalItemDto item, ChainApplyReport report)
        {
            if (filmCamera == null) return;
            var actor = ResolveActor(item.actorId);
            Vector3 anchor = ResolveAnchor(actor);
            CameraPreset.Apply((CameraPresetId)item.cameraPresetId, filmCamera, anchor);
            report.cameraApplied = true;
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
        }
    }
}
