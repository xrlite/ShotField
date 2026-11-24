#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>真实主场景四道闸：上下文、白名单、ghost 确认、执行回读与取消不变。</summary>
    public static class SceneCommandGateVerifier
    {
        private const int Expected = 15;

        [MenuItem("ShotField/Verifiers/Run Scene Command Gate Verifier")]
        public static void Run()
        {
            int passed = 0;
            SceneCommandPipeline pipeline = null;
            SceneCommandPreviewPanel panel = null;
            PropBrowserUI browser = null;
            GameObject testSpawned = null;
            ActorNavigator actor = null;
            Vector3 originalPosition = Vector3.zero;
            Quaternion originalRotation = Quaternion.identity;
            Camera directorCamera = null;
            Vector3 originalCameraPosition = Vector3.zero;
            Quaternion originalCameraRotation = Quaternion.identity;
            float originalFov = 45f, originalAspect = ShotCameraState.DefaultAspect;
            bool oldOffline = LabFeatureFlags.OfflineDemo;
            try
            {
                var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
                VerifyAssert.Require("SC01 主场景加载", scene.IsValid(), ref passed);
                var orchestrator = UnityEngine.Object.FindAnyObjectByType<ChainOrchestrator>();
                browser = UnityEngine.Object.FindAnyObjectByType<PropBrowserUI>();
                VerifyAssert.Require("AI 编排器与资产浏览器存在", orchestrator != null && browser != null, ref passed);
                orchestrator.filmCamera = orchestrator.filmCamera != null ? orchestrator.filmCamera : Camera.main;
                directorCamera = orchestrator.filmCamera;
                originalCameraPosition = directorCamera.transform.position;
                originalCameraRotation = directorCamera.transform.rotation;
                originalFov = directorCamera.fieldOfView; originalAspect = directorCamera.aspect;
                pipeline = orchestrator.GetComponent<SceneCommandPipeline>();
                if (pipeline == null) pipeline = orchestrator.gameObject.AddComponent<SceneCommandPipeline>();
                pipeline.orchestrator = orchestrator; pipeline.browser = browser; pipeline.filmCamera = orchestrator.filmCamera;
                panel = pipeline.previewPanel != null ? pipeline.previewPanel : pipeline.gameObject.AddComponent<SceneCommandPreviewPanel>();
                pipeline.previewPanel = panel;

                LabFeatureFlags.SetOfflineDemo(true);
                orchestrator.ImportScript("内景 客厅 夜\n男人拿起桌上的信，女人退后一步", "GateVerifier");
                VerifyAssert.Require("Gate1 上行上下文来自真实场景", orchestrator.LastSceneContext != null &&
                    orchestrator.LastSceneContext.objects.Count > 0 && orchestrator.Proposal != null, ref passed);
                var prompt = SceneContextCapture.BuildPromptContext(orchestrator.LastSceneContext);
                VerifyAssert.Require("上行含真实演员/道具/约束清单", prompt.availableActorSlots.Count > 0 &&
                    prompt.availableProps.Count >= 10 && prompt.existingConstraints.Count > 0, ref passed);

                var badType = new SceneCommandEnvelopeDto("bad-type", (SceneCommandType)999, "x", "{}");
                VerifyAssert.Require("未知命令类型 Gate1 拒绝", SceneCommandPipeline.ValidateSchema(badType).failedGateStep == 1, ref passed);
                var unsupported = new SceneCommandEnvelopeDto("unsupported", SceneCommandType.AssignColor, "x", "{}");
                VerifyAssert.Require("未实现白名单类型明确拒绝", SceneCommandPipeline.ValidateSchema(unsupported).errorCode == "UNSUPPORTED_TYPE", ref passed);
                var context = SceneContextCapture.Capture(orchestrator.filmCamera);
                var missing = new SpawnCommandPayload { propId = "missing_asset", position = new Vector3(0f, 0f, 1f) };
                var missingCommand = new SceneCommandEnvelopeDto("missing", SceneCommandType.Spawn, "spawn:missing_asset", JsonUtility.ToJson(missing));
                VerifyAssert.Require("未知资产 Gate2 拒绝", pipeline.ValidateBoundary(missingCommand, context).failedGateStep == 2, ref passed);

                actor = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                    .FirstOrDefault(a => a != null && a.gameObject.activeInHierarchy);
                VerifyAssert.Require("真实演员可用于 Move 验证", actor != null, ref passed);
                var actorMarker = actor.GetComponent<SemanticMarker>();
                string actorTargetId = actorMarker != null && !string.IsNullOrEmpty(actorMarker.markerId) ? actorMarker.markerId : actor.name;
                originalPosition = actor.transform.position; originalRotation = actor.transform.rotation;
                var invalidMove = new MoveCommandPayload { actorId = actorTargetId, targetPosition = new Vector3(1000f, 1000f, 1000f), baseOffset = 0f };
                var invalidMoveCommand = new SceneCommandEnvelopeDto("invalid-move", SceneCommandType.Move, actorTargetId, JsonUtility.ToJson(invalidMove));
                VerifyAssert.Require("越界/不可行走 Move Gate2 拒绝", pipeline.ValidateBoundary(invalidMoveCommand, context).errorCode == "UNWALKABLE", ref passed);

                var target = new Vector3(-1.6f, 0f, 1f);
                var proposal = new ChainProposalDto();
                proposal.items.Add(new ProposalItemDto
                {
                    itemId = "move-test", kind = ProposalItemKind.Actor, actorId = actorTargetId,
                    position = target, decision = ProposalDecision.Accepted
                });
                var report = new ChainApplyReport();
                pipeline.Enqueue(proposal, report);
                var ghost = GameObject.Find("AI_Ghost_Move");
                bool ghostIsolated = ghost != null && ghost.GetComponentInParent<GhostPreviewMarker>() != null &&
                    ghost.GetComponentsInChildren<SemanticMarker>(true).All(m => !m.enabled);
                VerifyAssert.Require("Gate3 未确认前不写场景且 ghost 可见", pipeline.IsRunning &&
                    pipeline.HasPendingCommand && panel.Visible && ghostIsolated &&
                    Vector3.Distance(actor.transform.position, originalPosition) < 0.001f, ref passed);
                pipeline.ConfirmCurrent();
                VerifyAssert.Require($"Gate4 确认执行后回读一致 executed={report.executedCount} readbackFail={report.readbackFailedCount} " +
                    $"distance={Vector3.Distance(actor.transform.position, target):F3} detail={string.Join(";", report.placementFailures)}",
                    !pipeline.IsRunning && report.executedCount == 1 && report.readbackFailedCount == 0 &&
                    Vector3.Distance(actor.transform.position, target) < 0.7f, ref passed);

                Vector3 beforeCancel = actor.transform.position;
                proposal = new ChainProposalDto();
                proposal.items.Add(new ProposalItemDto
                {
                    itemId = "cancel-test", kind = ProposalItemKind.Actor, actorId = actorTargetId,
                    position = new Vector3(-1.6f, 0f, 2f), decision = ProposalDecision.Accepted
                });
                report = new ChainApplyReport(); pipeline.Enqueue(proposal, report); pipeline.CancelCurrent();
                VerifyAssert.Require("Gate3 取消不改场景", report.cancelledCount == 1 && report.executedCount == 0 &&
                    Vector3.Distance(actor.transform.position, beforeCancel) < 0.001f, ref passed);

                proposal = new ChainProposalDto();
                proposal.items.Add(new ProposalItemDto
                {
                    itemId = "shot-test", kind = ProposalItemKind.Shot,
                    cameraPresetId = (int)CameraPresetId.Front, decision = ProposalDecision.Accepted
                });
                report = new ChainApplyReport(); pipeline.Enqueue(proposal, report); pipeline.ConfirmCurrent();
                VerifyAssert.Require("ProposeShot 确认后机位回读一致", report.executedCount == 1 &&
                    report.readbackFailedCount == 0 && Vector3.Distance(directorCamera.transform.position, originalCameraPosition) > 0.1f, ref passed);
                directorCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                directorCamera.fieldOfView = originalFov; directorCamera.aspect = originalAspect;

                var sofa = new SpawnCommandPayload { propId = "sofa", position = new Vector3(1.5f, 0f, 2.15f), rotation = Quaternion.identity };
                var sofaCommand = new SceneCommandEnvelopeDto("sofa", SceneCommandType.Spawn, "spawn:sofa", JsonUtility.ToJson(sofa));
                VerifyAssert.Require("真实 Spawn 通过 schema/boundary", SceneCommandPipeline.ValidateSchema(sofaCommand).isValid &&
                    pipeline.ValidateBoundary(sofaCommand, context).isValid, ref passed);

                int beforeCallback = SceneContextCapture.Capture(directorCamera).objects.Count;
                void ThrowCallback(GameObject _) => throw new InvalidOperationException("test callback failure");
                browser.OnPropSpawned += ThrowCallback;
                try
                {
                    proposal = new ChainProposalDto();
                    proposal.items.Add(new ProposalItemDto
                    {
                        itemId = "callback-test", kind = ProposalItemKind.Prop, propId = "sofa",
                        position = new Vector3(1.5f, 0f, 2.15f), decision = ProposalDecision.Accepted
                    });
                    report = new ChainApplyReport(); pipeline.Enqueue(proposal, report); pipeline.ConfirmCurrent();
                    testSpawned = pipeline.LastExecutedObject;
                }
                finally { browser.OnPropSpawned -= ThrowCallback; }
                VerifyAssert.Require("Spawn 回调异常仍留精确实例并完成回读", report.executedCount == 1 &&
                    report.readbackFailedCount == 0 && testSpawned != null &&
                    SceneContextCapture.Capture(directorCamera).objects.Count == beforeCallback + 1, ref passed);
            }
            catch (Exception ex) { Debug.LogError("[SCENE-COMMAND-GATE] " + ex); }
            finally
            {
                if (pipeline != null) pipeline.CancelAll();
                if (actor != null)
                {
                    if (actor.agent != null && actor.agent.isOnNavMesh) actor.agent.Warp(originalPosition);
                    actor.transform.position = originalPosition;
                    actor.transform.rotation = originalRotation;
                }
                if (directorCamera != null)
                {
                    directorCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                    directorCamera.fieldOfView = originalFov; directorCamera.aspect = originalAspect;
                }
                if (panel != null && panel.PanelRoot != null) UnityEngine.Object.DestroyImmediate(panel.PanelRoot.gameObject);
                if (testSpawned != null)
                {
                    if (browser != null) browser.RemoveSpawnedProp(testSpawned);
                    UnityEngine.Object.DestroyImmediate(testSpawned);
                }
                if (pipeline != null) UnityEngine.Object.DestroyImmediate(pipeline);
                LabFeatureFlags.SetOfflineDemo(oldOffline);
            }
            VerifyAssert.Finish("SCENE-COMMAND-GATE-RESULT", passed, Expected);
        }
    }
}
#endif
