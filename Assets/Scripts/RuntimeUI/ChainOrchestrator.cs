using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P1 离线链编排器：剧本文本 → AiScriptParser 离线 mock 解析 → 结构化提案 →
    /// 确认 UI 处置 → 经既有公开 API 落场景 → 生成时间轴分段 → 导出 JSON 清单。
    /// 在线段（DeepSeek / 即梦）未接 key 前，解析固定走离线 mock；不发任何在线请求。
    /// </summary>
    public class ChainOrchestrator : MonoBehaviour
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
            if (_parser == null) _parser = new AiScriptParser { IsOfflineMock = true };

            ScriptAnalysisResult result = _parser.ParseScriptAsync(rawScript).GetAwaiter().GetResult();
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

        private void ApplyActor(ProposalItemDto item, ChainApplyReport report)
        {
            var actor = ResolveActor(item.actorId);
            if (actor == null)
            {
                report.placementFailures.Add($"演员 {item.actorId}：场景内无已登记演员");
                return;
            }
            Vector3 pos = item.position.ToUnityVector3();
            if (actor.TryPlace(pos, SampleRadius, out Vector3 snapped))
                report.placedActorIds.Add(actor.name);
            else
                report.placementFailures.Add($"演员 {actor.name}：落点不可行走，未落位");
        }

        private void ApplyCamera(ProposalItemDto item, ChainApplyReport report)
        {
            if (filmCamera == null) return;
            var actor = ResolveActor(item.actorId);
            Vector3 anchor = ResolveAnchor(actor);
            CameraPreset.Apply((CameraPresetId)item.cameraPresetId, filmCamera, anchor);
            report.cameraApplied = true;
        }

        // 解析器角色名（ActorA / Director）与场景 GameObject 名（PrevisCast_*）不一致，
        // 在此做别名映射；未命中再按名字包含匹配，最后回退 Current。
        private static readonly Dictionary<string, string> ActorAliases = new Dictionary<string, string>
        {
            ["ActorA"] = "PrevisCast_A_Red",
            ["actorA"] = "PrevisCast_A_Red",
            ["actorRed"] = "PrevisCast_A_Red",
            ["ActorB"] = "PrevisCast_B_Blue",
            ["actorB"] = "PrevisCast_B_Blue",
            ["actorBlue"] = "PrevisCast_B_Blue",
            ["Director"] = "PrevisCast_B_Blue",
            ["actorYellow"] = "PrevisCast_C_Yellow",
            ["actorGreen"] = "PrevisCast_D_Green",
            ["actorViolet"] = "PrevisCast_E_Violet",
            ["actorMagenta"] = "PrevisCast_F_Magenta"
        };

        private ActorNavigator ResolveActor(string actorId)
        {
            var all = ActorSelection.All;
            if (all != null)
            {
                string sceneName = ActorAliases.TryGetValue(actorId ?? "", out var mapped) ? mapped : actorId;
                foreach (var nav in all)
                {
                    if (nav == null) continue;
                    if (nav.name == sceneName) return nav;
                }
                foreach (var nav in all)
                {
                    if (nav != null && !string.IsNullOrEmpty(actorId) && nav.name.Contains(actorId)) return nav;
                }
            }
            return ActorSelection.Current;
        }

        private Vector3 ResolveAnchor(ActorNavigator fallbackActor)
        {
            if (anchorTransform != null) return anchorTransform.position;
            var marker = SemanticMarker.Find("envelope");
            if (marker != null) return marker.transform.position;
            if (fallbackActor != null) return fallbackActor.transform.position;
            return new Vector3(0.8f, 1.25f, 1.25f);
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
