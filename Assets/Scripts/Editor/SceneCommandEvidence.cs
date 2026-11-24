#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotField.EditorTools
{
    /// <summary>四道闸可读证据：确认前不写入、ghost/面板可见、确认后回读并清理测试物。</summary>
    public static class SceneCommandEvidence
    {
        public static string OutputDirectory;

        [Serializable]
        internal sealed class Report
        {
            public string status, commandType, targetId, ghostScreenshot, panelScreenshot;
            public int objectsBefore, objectsAfter, executedCount, readbackFailedCount;
            public bool previewVisible, ghostPresent, unchangedBeforeConfirm;
        }

        [MenuItem("ShotField/Evidence/Capture Scene Command Gates")]
        public static void Run()
        {
            string directory = OutputDirectory;
            if (string.IsNullOrEmpty(directory))
                directory = Environment.GetEnvironmentVariable("SHOTFIELD_SCENE_COMMAND_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                string month = Path.Combine(B5EvidenceSupport.FindEvidenceRoot(), DateTime.Now.ToString("yyyy-MM"));
                Directory.CreateDirectory(month);
                directory = Path.Combine(month, "scene_command_evidence_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            }
            Directory.CreateDirectory(directory);
            string ghostShot = Path.Combine(directory, "scene_command_ghost.png");
            string panelShot = Path.Combine(directory, "scene_command_preview_panel.png");
            string reportPath = Path.Combine(directory, "scene_command_evidence.json");
            if (File.Exists(ghostShot) || File.Exists(panelShot) || File.Exists(reportPath))
                throw new InvalidOperationException("Scene command evidence already exists; no files were overwritten.");

            EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var orchestrator = Object.FindAnyObjectByType<ChainOrchestrator>();
            var browser = Object.FindAnyObjectByType<PropBrowserUI>();
            if (orchestrator == null || browser == null) throw new InvalidOperationException("缺少编排器或资产浏览器");
            orchestrator.filmCamera = orchestrator.filmCamera != null ? orchestrator.filmCamera : Camera.main;
            var pipeline = orchestrator.GetComponent<SceneCommandPipeline>();
            if (pipeline == null) pipeline = orchestrator.gameObject.AddComponent<SceneCommandPipeline>();
            pipeline.orchestrator = orchestrator; pipeline.browser = browser; pipeline.filmCamera = orchestrator.filmCamera;
            var panel = pipeline.previewPanel != null ? pipeline.previewPanel : pipeline.gameObject.AddComponent<SceneCommandPreviewPanel>();
            pipeline.previewPanel = panel;
            Camera camera = null;
            GameObject executedObject = null;
            try
            {
                var before = SceneContextCapture.Capture(orchestrator.filmCamera);
                var target = new Vector3(1.5f, 0f, 2.15f);
                var proposal = new ChainProposalDto();
                proposal.items.Add(new ProposalItemDto
                {
                    itemId = "spawn-evidence", kind = ProposalItemKind.Prop, propId = "sofa",
                    position = target, decision = ProposalDecision.Accepted
                });
                var report = new ChainApplyReport();
                pipeline.Enqueue(proposal, report);
                var ghost = GameObject.Find("AI_Ghost_Spawn");
                var afterPreview = SceneContextCapture.Capture(orchestrator.filmCamera);
                if (ghost == null || !panel.Visible || afterPreview.objects.Count != before.objects.Count)
                    throw new InvalidOperationException("确认前 ghost/面板/对象数不符合预期");
                bool previewWasVisible = panel.Visible;
                bool ghostWasPresent = ghost != null;

                camera = new GameObject("SceneCommandEvidenceCamera").AddComponent<Camera>();
                camera.transform.position = target + new Vector3(1.5f, 1.3f, 1.8f);
                camera.transform.LookAt(target + Vector3.up * 0.5f);
                camera.fieldOfView = 48f; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
                B5EvidenceRenderer.RenderPng(camera, ghostShot, 1280, 720);

                panel.PanelRoot.position = new Vector3(0f, 1.6f, 1f);
                panel.PanelRoot.rotation = Quaternion.identity;
                foreach (var child in panel.PanelRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                camera.transform.position = new Vector3(0f, 1.6f, 0f);
                camera.transform.LookAt(panel.PanelRoot.position);
                camera.cullingMask = 1 << 5;
                B5EvidenceRenderer.RenderPng(camera, panelShot, 1240, 840);

                pipeline.ConfirmCurrent();
                executedObject = pipeline.LastExecutedObject;
                var after = SceneContextCapture.Capture(orchestrator.filmCamera);
                var dto = new Report
                {
                    status = "PASS", commandType = pipeline.LastExecutionResult != null ? "Spawn" : "None",
                    targetId = "sofa", ghostScreenshot = ghostShot, panelScreenshot = panelShot,
                    objectsBefore = before.objects.Count, objectsAfter = after.objects.Count,
                    executedCount = report.executedCount, readbackFailedCount = report.readbackFailedCount,
                    previewVisible = previewWasVisible, ghostPresent = ghostWasPresent,
                    unchangedBeforeConfirm = afterPreview.objects.Count == before.objects.Count
                };
                if (report.executedCount != 1 || report.readbackFailedCount != 0 || after.objects.Count != before.objects.Count + 1)
                    throw new InvalidOperationException("确认后执行/回读不符合预期");
                File.WriteAllText(reportPath, JsonUtility.ToJson(dto, true), new UTF8Encoding(false));
                Debug.Log($"SCENE-COMMAND-EVIDENCE=PASS objects={dto.objectsBefore}->{dto.objectsAfter} path={directory}");
            }
            finally
            {
                if (pipeline != null) pipeline.CancelAll();
                if (executedObject != null)
                {
                    if (browser != null) browser.RemoveSpawnedProp(executedObject);
                    Object.DestroyImmediate(executedObject);
                }
                if (panel != null && panel.PanelRoot != null) Object.DestroyImmediate(panel.PanelRoot.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                if (pipeline != null) Object.DestroyImmediate(pipeline);
            }
        }
    }
}
#endif
