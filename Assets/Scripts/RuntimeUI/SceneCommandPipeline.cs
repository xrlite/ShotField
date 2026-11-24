using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    internal sealed class PendingSceneCommand
    {
        public SceneCommandEnvelopeDto command;
        public ProposalItemDto proposalItem;
        public string expectedObjectId;
        public Vector3 expectedPosition;
        public Quaternion expectedRotation = Quaternion.identity;
        public float expectedFieldOfView;
        public float preFieldOfView, preAspect;
        public SceneContextDto preContext;
    }

    /// <summary>AI 提案执行队列：Gate 1/2 校验 → Gate 3 ghost 确认 → Gate 4 回读，失败不静默。</summary>
    public partial class SceneCommandPipeline : MonoBehaviour
    {
        public PropBrowserUI browser;
        public Camera filmCamera;
        public Transform anchorTransform;
        public SceneCommandPreviewPanel previewPanel;
        public ChainOrchestrator orchestrator;

        public event Action<ChainApplyReport> OnCompleted;
        public bool IsRunning { get; private set; }
        public int PendingCount => _queue.Count;
        public bool HasPendingCommand => _current != null;
        public string CurrentCommandId => _current != null ? _current.command.commandId : "";
        public SceneCommandType CurrentCommandType => _current != null ? (SceneCommandType)_current.command.commandType : SceneCommandType.Spawn;
        public ChainProposalDto ProposalSnapshot => _proposalSnapshot;
        public GameObject LastExecutedObject { get; private set; }
        public SceneContextDto LastInitialContext { get; private set; }
        public CommandExecutionResultDto LastExecutionResult { get; private set; }
        public ChainApplyReport LastReport { get; private set; }

        private readonly Queue<PendingSceneCommand> _queue = new Queue<PendingSceneCommand>();
        private PendingSceneCommand _current;
        private ChainApplyReport _report;
        private ChainProposalDto _proposalSnapshot;
        private GameObject _ghost, _ghostRoot;
        private Material _ghostMaterial;

        private void Awake()
        {
            EnsureWired();
            if (previewPanel != null)
            {
                previewPanel.OnConfirm += ConfirmCurrent;
                previewPanel.OnCancel += CancelCurrent;
            }
        }

        public bool Enqueue(ChainProposalDto proposal, ChainApplyReport report)
        {
            if (IsRunning || proposal == null || report == null) return false;
            EnsureWired();
            _report = report; _queue.Clear(); LastReport = report;
            _proposalSnapshot = JsonUtility.FromJson<ChainProposalDto>(JsonUtility.ToJson(proposal));
            LastExecutedObject = null;
            LastInitialContext = SceneContextCapture.Capture(filmCamera);
            report.initialContextJson = JsonUtility.ToJson(LastInitialContext);
            foreach (var item in proposal.AcceptedItems)
            {
                if (TryMapItem(item, out var command, out string error)) _queue.Enqueue(command);
                else report.placementFailures.Add(error);
            }
            IsRunning = true;
            ShowNext();
            return true;
        }

        public void ConfirmCurrent()
        {
            if (_current == null || previewPanel == null || !previewPanel.Visible) return;
            var schema = ValidateSchema(_current.command);
            if (!schema.isValid) { RejectAndAdvance(schema, 1); return; }
            var boundary = ValidateBoundary(_current.command, SceneContextCapture.Capture(filmCamera));
            if (!boundary.isValid) { RejectAndAdvance(boundary, 2); return; }
            ExecuteCurrent();
        }

        public void CancelCurrent()
        {
            if (_current == null) return;
            _report.cancelledCount++;
            _report.placementFailures.Add("导演取消: " + _current.command.commandId);
            DestroyGhost(); _current = null;
            if (previewPanel != null) previewPanel.Hide();
            ShowNext();
        }

        public void CancelAll()
        {
            while (_current != null) CancelCurrent();
            _queue.Clear();
            FinishIfDone();
        }

        private void ShowNext()
        {
            DestroyGhost();
            if (previewPanel != null) previewPanel.Hide();
            while (_queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _current.preContext = SceneContextCapture.Capture(filmCamera);
                if (filmCamera != null)
                { _current.preFieldOfView = filmCamera.fieldOfView; _current.preAspect = filmCamera.aspect; }
                var schema = ValidateSchema(_current.command);
                if (!schema.isValid) { Reject(schema, 1); continue; }
                var boundary = ValidateBoundary(_current.command, _current.preContext);
                if (!boundary.isValid) { Reject(boundary, 2); continue; }
                CreateGhost(_current);
                if (previewPanel != null)
                {
                    if (previewPanel.headAnchor == null && Camera.main != null) previewPanel.headAnchor = Camera.main.transform;
                    previewPanel.Show("AI 场景命令 | " + (SceneCommandType)_current.command.commandType,
                        $"{_current.command.targetId}\n{_current.proposalItem.actionText}\n确认后执行，并在同一帧回读。");
                }
                return;
            }
            _current = null;
            FinishIfDone();
        }

        private void RejectAndAdvance(CommandValidationResultDto result, int gate)
        {
            DestroyGhost();
            if (previewPanel != null) previewPanel.Hide();
            Reject(result, gate);
            ShowNext();
        }

        private void Reject(CommandValidationResultDto result, int gate)
        {
            if (gate == 1) _report.gate1RejectedCount++; else _report.gate2RejectedCount++;
            _report.placementFailures.Add($"Gate{gate} {result.errorCode}: {result.rejectionReason}");
            LastExecutionResult = new CommandExecutionResultDto(_current.command.commandId, false, result.rejectionReason);
            _current = null;
        }

        private void FinishIfDone()
        {
            if (_current != null || _queue.Count > 0 || !IsRunning) return;
            IsRunning = false;
            OnCompleted?.Invoke(_report);
        }

        private void EnsureWired()
        {
            if (browser == null) browser = FindAnyObjectByType<PropBrowserUI>();
            if (orchestrator == null) orchestrator = FindAnyObjectByType<ChainOrchestrator>();
            if (filmCamera == null && orchestrator != null) filmCamera = orchestrator.filmCamera;
            if (filmCamera == null)
            {
                var recorder = FindAnyObjectByType<CameraMotionRecorder>();
                filmCamera = recorder != null ? recorder.filmCamera : Camera.main;
            }
            if (anchorTransform == null && orchestrator != null) anchorTransform = orchestrator.anchorTransform;
            if (previewPanel == null) previewPanel = GetComponent<SceneCommandPreviewPanel>();
            if (previewPanel == null) previewPanel = gameObject.AddComponent<SceneCommandPreviewPanel>();
        }

        private void OnDestroy()
        {
            if (previewPanel != null)
            {
                previewPanel.OnConfirm -= ConfirmCurrent;
                previewPanel.OnCancel -= CancelCurrent;
            }
            DestroyGhost();
        }
    }
}
