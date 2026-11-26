using System;
using ShotField.Core;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    public sealed partial class XrAiConnectionView
    {
        private GameObject _workflow;
        private TMP_Text _summary, _workflowStatus;
        private Button _confirm, _left, _right, _save, _review;
        private RawImage _feed;
        private ChainOrchestrator _chain;
        private ChainProposalDto _staged;
        private TabletopDirector _tabletop;
        private SpatialWorkbench _workbench;
        private ShotData _saved;
        private bool _laidOut;
        private bool _commandsComplete;
        private NavMeshDataInstance _tabletopNavigation;

        private void BuildWorkflow(RectTransform parent)
        {
            _workflow = RuntimeUiFactory.CreatePanel(parent, "DirectorWorkflow", PanelColor,
                new Vector2(0f, 178f), new Vector2(526f, 264f));
            var root = _workflow.transform;
            _summary = RuntimeUiFactory.CreateTmpLabel(root, "Shots", "", 16, new Vector2(0, 56), new Vector2(488, 136));
            _summary.alignment = TextAlignmentOptions.TopLeft;
            _workflowStatus = RuntimeUiFactory.CreateTmpLabel(root, "WorkflowStatus", "", 14,
                new Vector2(0, -63), new Vector2(488, 36));
            _confirm = ActionButton(root, "ConfirmProposal", "确认三镜头", -158, ConfirmProposal);
            _left = ActionButton(root, "MoveLeft", "演员 A 左移", -52, () => PlaceActor(-0.35f));
            _right = ActionButton(root, "MoveRight", "演员 A 右移", 54, () => PlaceActor(0.35f));
            _save = ActionButton(root, "SaveCamera", "保存机位", 160, SaveCamera);
            _review = RuntimeUiFactory.CreateTmpButton(root, "ReviewCamera", "回看已存机位", new Vector2(156, -20),
                new Vector2(144, 32), onClick: ReviewCamera);
            RuntimeUiFactory.CreateTmpButton(root, "EditScript", "编辑剧本与设置", new Vector2(-156, -20),
                new Vector2(144, 32), onClick: OpenSettings);
            var display = new GameObject("SavedCameraFeed", typeof(RectTransform), typeof(RawImage));
            display.transform.SetParent(root, false);
            _feed = display.GetComponent<RawImage>();
            _feed.rectTransform.sizeDelta = new Vector2(212, 119);
            _feed.rectTransform.anchoredPosition = new Vector2(-122, 49);
            _feed.raycastTarget = false;
            display.SetActive(false);
            _workflow.SetActive(false);
        }

        private static Button ActionButton(Transform root, string name, string caption, float x,
            UnityEngine.Events.UnityAction click) => RuntimeUiFactory.CreateTmpButton(root, name, caption,
                new Vector2(x, -107), new Vector2(100, 36), onClick: click);

        private string PresentProposal(ProposalContractDto result, string script)
        {
            _chain = FindAnyObjectByType<ChainOrchestrator>();
            if (_chain == null || !_chain.StageOnlineProposal(result, script))
                return $"收到 {result.shots.Count} 镜头。请完成当前预览，或重新生成三镜头提案。";
            _staged = _chain.Proposal;
            _summary.gameObject.SetActive(true);
            _feed.gameObject.SetActive(false);
            _saved = null;
            _laidOut = _commandsComplete = false;
            _summary.text = _staged.sceneTitle + "\n" +
                string.Join("\n", _staged.items.ConvertAll(x => $"{x.label}  {x.actionText}"));
            _workflowStatus.text = "请核对镜头，然后确认";
            _workflow.SetActive(true);
            _settings.SetActive(false);
            return "在线提案已进入导演工作流";
        }

        private void ConfirmProposal()
        {
            if (_chain == null || _chain.Proposal != _staged) return;
            var pipeline = _chain.GetComponent<SceneCommandPipeline>();
            if (pipeline == null || pipeline.IsRunning) return;
            _tabletop = FindAnyObjectByType<TabletopDirector>();
            if (_tabletop == null || _tabletop.currentMode != TabletopDirector.ScaleMode.TabletopMini)
            {
                _workflowStatus.text = "请先回到沙盘，再确认提案";
                return;
            }

            var surface = _tabletop.sceneRoot1to1 != null
                ? _tabletop.sceneRoot1to1.GetComponentInChildren<NavMeshSurface>(true) : null;
            if (!_tabletopNavigation.valid && surface != null && !surface.isActiveAndEnabled && surface.navMeshData != null)
                _tabletopNavigation = NavMesh.AddNavMeshData(surface.navMeshData, surface.transform.position, surface.transform.rotation);

            foreach (var item in _staged.items) item.decision = ProposalDecision.Accepted;
            _chain.OnApplied += Applied;
            _workflow.SetActive(false);
            _chain.ConfirmAndApplyInTabletop();
        }

        private void Applied(ChainApplyReport report)
        {
            _chain.OnApplied -= Applied;
            _commandsComplete = report.executedCount == _staged.items.Count && report.readbackFailedCount == 0;
            _workflow.SetActive(true);
            _workflowStatus.text = _commandsComplete
                ? "三镜头确认完成。排好演员，再进入片场"
                : $"仅 {report.executedCount} 镜头通过，请处理取消或失败项后重试";
        }

        private void PlaceActor(float offset)
        {
            if (!_commandsComplete || _tabletop == null || _tabletop.currentMode != TabletopDirector.ScaleMode.TabletopMini ||
                !_chain.TryResolveActorStrict("actorA", out var actor)) return;

            var current = actor.transform.position;
            var mini = _tabletop.WorldToTabletop(current);
            var target = _tabletop.TabletopToWorld(mini + _tabletop.TabletopAnchor.right * (offset * _tabletop.miniScale));
            if (!actor.CanStandAt(target, 0.6f, out var position))
            {
                _workflowStatus.text = "目标不在可排位区域，请试另一侧";
                return;
            }
            if (actor.gameObject.activeInHierarchy) actor.Teleport(position);
            else actor.transform.position = position;
            if (Vector3.Distance(actor.transform.position, current) < 0.02f)
            {
                _workflowStatus.text = "未能改变演员位置，请试另一侧";
                return;
            }
            Physics.SyncTransforms();
            _laidOut = true;
            _workflowStatus.text = "演员位置已更新，点击「进入 1:1 片场」";
        }

        private void SaveCamera()
        {
            if (!_laidOut || _tabletop == null || _tabletop.currentMode != TabletopDirector.ScaleMode.Immersive1to1) return;
            _workbench = FindAnyObjectByType<SpatialWorkbench>();
            if (_workbench?.library == null || _workbench.library.ShotCamera == null) return;
            int count = _workbench.library.Count;
            _workbench.CreateNewShot();
            if (_workbench.library.Count <= count) return;
            bool persisted = false;
            void Saved(ShotPassportDto _) => persisted = true;
            _workbench.PassportSaved += Saved;
            try { _workbench.SavePassport(); }
            finally { _workbench.PassportSaved -= Saved; }
            if (persisted) _saved = _workbench.SelectedShot;
            _workflowStatus.text = persisted ? $"已保存 {_saved.shotId}，可回看取景" : "机位数据未能写入";
        }

        private void ReviewCamera()
        {
            if (_saved == null || _workbench?.monitor == null || _workbench.library.ShotCamera == null) return;
            _saved.camera.ApplyTo(_workbench.library.ShotCamera);
            _workbench.monitor.RequestRender();
            _summary.gameObject.SetActive(false);
            _feed.gameObject.SetActive(true);
            _workflowStatus.text = $"正在监看：{_saved.shotId}";
        }

        private void LateUpdate()
        {
            bool onTable = _tabletop != null && _tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            if (!onTable && _tabletopNavigation.valid) _tabletopNavigation.Remove();
            if (_workflow == null || !_workflow.activeSelf) return;
            _confirm.interactable = !_commandsComplete;
            _left.interactable = _right.interactable = _commandsComplete && onTable;
            _save.interactable = _laidOut && !onTable;
            _review.interactable = _saved != null && !onTable;
            if (_feed.gameObject.activeSelf) _feed.texture = _workbench?.monitor?.Feed;
        }

        private void OnDestroy()
        {
            if (_chain != null) _chain.OnApplied -= Applied;
            if (_tabletopNavigation.valid) _tabletopNavigation.Remove();
        }
    }
}
