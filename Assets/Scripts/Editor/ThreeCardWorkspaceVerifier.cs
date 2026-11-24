#if UNITY_EDITOR
using System;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ShotField.EditorTools
{
    /// <summary>三块空间积木的装配、交互契约与失败回退验收。</summary>
    public static class ThreeCardWorkspaceVerifier
    {
        private const int Expected = 13;
        [MenuItem("ShotField/Verifiers/Run Three Card Workspace Verifier")]
        public static void Run()
        {
            int passed = 0;
            var host = new GameObject("[ThreeCardVerifier]");
            var head = new GameObject("Head");
            var deps = new GameObject("Deps");
            try
            {
                head.transform.position = new Vector3(0f, 1.6f, 0f);
                var registry = host.AddComponent<BlockPanelRegistry>(); registry.headAnchor = head.transform;
                var cameraGo = new GameObject("Camera"); cameraGo.transform.SetParent(deps.transform, false);
                var camera = cameraGo.AddComponent<Camera>();
                var monitor = deps.AddComponent<SpatialMonitor>(); monitor.sourceCamera = camera;
                var recorder = deps.AddComponent<CameraMotionRecorder>();
                var raw = new CameraTrajectory(); raw.AddPoint(0f, Vector3.zero, Quaternion.identity); raw.AddPoint(10f, Vector3.forward, Quaternion.identity);
                recorder.rawTrajectory = raw; recorder.smoothedTrajectory = raw.Clone();
                var timeline = deps.AddComponent<DirectorTimeline>(); timeline.cameraRecorder = recorder;
                var controller = deps.AddComponent<ChainTimelineController>(); controller.timeline = timeline;
                var browser = deps.AddComponent<PropBrowserUI>(); browser.sceneRoot = deps.transform;
                var workspace = host.AddComponent<ThreeCardWorkspace>();
                workspace.useSceneDependencies = false; workspace.monitor = monitor; workspace.timeline = timeline;
                workspace.browser = browser; workspace.timelineController = controller; workspace.BuildCards();
                var cards = new[] { workspace.GetCard(ThreeCardWorkspace.MonitorId), workspace.GetCard(ThreeCardWorkspace.TimelineId), workspace.GetCard(ThreeCardWorkspace.DrawerId) };
                Check("三卡创建且默认隐藏", cards[0] != null && cards[1] != null && cards[2] != null && !workspace.AnyOpen && Array.TrueForAll(cards, c => c != null && !c.IsOpen), ref passed);
                Check("三卡各自独立 WorldSpace Canvas", Array.TrueForAll(cards, c => c != null && c.Canvas != null && c.Canvas.renderMode == RenderMode.WorldSpace && Mathf.Abs(c.transform.localScale.x - 0.001f) < 0.00001f), ref passed);
                Check("卡片世界宽度小于 1m，无巨墙", Array.TrueForAll(cards, c => c != null && ((RectTransform)c.transform).rect.width * c.transform.lossyScale.x < 1f), ref passed);
                Check("三卡都有抓取与边缘缩放", Array.TrueForAll(cards, c => c != null && c.GetComponent<XRGrabInteractable>() != null && c.GetComponentInChildren<CardScaleHandle>(true) != null), ref passed);
                workspace.ShowAll();
                Check("ShowAll 注册三卡且角间距 55°", registry.Count == 3 && workspace.AnyOpen && Mathf.Abs(registry.panelSpacingDeg - 55f) < 0.01f, ref passed);
                cards[0].SetDistance(0.1f); float min = cards[0].CurrentDistance; cards[0].SetDistance(9f); float max = cards[0].CurrentDistance; cards[0].SetDistance(1.1f); cards[0].Snap();
                Check("监看板缩放钳制 0.5–3m 并吸附", Mathf.Abs(min - 0.5f) < 0.001f && Mathf.Abs(max - 3f) < 0.001f && Mathf.Abs(cards[0].CurrentDistance - 1.2f) < 0.001f, ref passed);
                var timelineView = cards[1].GetComponent<TimelineCardView>();
                Check("时间轴至少三段且每段有拖动/裁切", timelineView != null && timelineView.ClipCount >= 3 && cards[1].GetComponentsInChildren<TimelineClipDrag>(true).Length == timelineView.ClipCount && cards[1].GetComponentsInChildren<TimelineTrimHandle>(true).Length == timelineView.ClipCount, ref passed);
                string first = timelineView.Model.clips[0].clipId; bool reordered = timelineView.ReorderClip(0, 2);
                Check("Clip 调序真实修改模型", reordered && timelineView.Model.clips[2].clipId == first && timelineView.ReorderCount == 1, ref passed);
                var trajectory = recorder.ActiveTrajectory; float before = timelineView.Model.clips[1].durationSeconds; bool trimmed = timelineView.TrimClip(1, before + 2f);
                Check("Clip 裁切真实改时长且轨迹引用不变", trimmed && timelineView.Model.clips[1].durationSeconds > before && ReferenceEquals(trajectory, recorder.ActiveTrajectory), ref passed);
                var drawer = cards[2].GetComponent<AssetDrawerCardView>();
                bool gray = drawer != null && drawer.CurrentSource == "graybox" && drawer.SpawnGraybox(0.5f);
                bool fallback = drawer != null && !drawer.ImportTripo("missing.glb") && drawer.FallbackCount > 0 && drawer.CurrentSource == "graybox";
                Check("抽屉默认灰盒，Tripo 失败保留灰盒", gray && fallback, ref passed);
                var menu = new PalmMenuState { Cards = workspace }; menu.SelectTab(PalmMenuTab.Cards); var entries = menu.BuildEntries();
                Check("PalmMenu 卡片分栏恰好三个呼出入口", entries.Count == 3 && Array.TrueForAll(entries.ToArray(), e => e.Kind == PalmMenuActionKind.ToggleCard && !string.IsNullOrEmpty(e.CardId)), ref passed);
                int legacy = 0, tmp = 0;
                foreach (var card in cards) { legacy += card.GetComponentsInChildren<Text>(true).Length; tmp += card.GetComponentsInChildren<TMPro.TMP_Text>(true).Length; }
                Check("三卡无 legacy Text 且中文 TMP 非空", legacy == 0 && tmp > 0, ref passed);
                workspace.HideAll();
                Check("HideAll 收纳三卡并清空注册", !workspace.AnyOpen && registry.Count == 0 && Array.TrueForAll(cards, c => c != null && !c.gameObject.activeSelf), ref passed);
            }
            catch (Exception ex) { Debug.LogError("[THREE-CARD] " + ex); }
            finally
            {
                var ws = host.GetComponent<ThreeCardWorkspace>(); if (ws != null) ws.DestroyCards();
                UnityEngine.Object.DestroyImmediate(deps); UnityEngine.Object.DestroyImmediate(head); UnityEngine.Object.DestroyImmediate(host);
            }
            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"THREE-CARD-RESULT={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }
        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[THREE-CARD] PASS: {label}"); }
            else Debug.LogError($"[THREE-CARD] FAIL: {label}");
        }
    }
}
#endif
