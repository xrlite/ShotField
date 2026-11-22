#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using ShotField.RuntimeUI;
using ShotField.Core;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 空间 3D 浮动监视器与工作台构建器（Spatial Workbench Builder）。
    /// 严格对照 PICO 官方 Spatial UI 规范，生成具有深色磨砂与多窗口布局的 WorldSpace 工作台。
    /// </summary>
    public static class SpatialWorkbenchBuilder
    {
        private static Color PicoBg => PrevisPalette.PicoBg;
        private static Color PicoCard => PrevisPalette.PicoCard;
        private static Color PicoAccent => PrevisPalette.PicoAccent;

        [MenuItem("ShotField/Workbench/Build Spatial 3D Floating Monitor and Workbench")]
        [MenuItem("镜场 ShotField/构建空间 3D 浮动监视器与工作台")]
        public static GameObject BuildWorkbench()
        {
            EnsureEventSystem();

            var old = GameObject.Find("Spatial_Workbench_Canvas");
            if (old != null) Object.DestroyImmediate(old);

            // 1. WorldSpace 主画布
            var canvasGo = new GameObject("Spatial_Workbench_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.position = new Vector3(2.35f, 1.35f, 1.55f);
            canvasRt.rotation = Quaternion.Euler(15f, -145f, 0f);
            canvasRt.sizeDelta = new Vector2(1080f, 700f);
            canvasRt.localScale = Vector3.one * 0.0018f;

            var wb = canvasGo.AddComponent<SpatialWorkbench>();

            // 2. 窗口 A：3D 悬浮监视器（中央）
            var monitorPanel = RuntimeUiFactory.CreatePanel(canvasGo.transform, "WindowA_Monitor", PicoBg, new Vector2(0f, 100f), new Vector2(660f, 380f));
            var displayGo = new GameObject("Display");
            displayGo.transform.SetParent(monitorPanel.transform, false);
            var displayImg = displayGo.AddComponent<RawImage>();
            var displayRt = displayGo.GetComponent<RectTransform>();
            displayRt.sizeDelta = new Vector2(640f, 360f);

            var mon = canvasGo.AddComponent<SpatialMonitor>();
            var dirCam = GameObject.Find("Director")?.GetComponent<Camera>();
            mon.sourceCamera = dirCam;
            mon.monitorDisplay = displayImg;
            mon.viewportFrame = displayRt;
            wb.monitor = mon;
            wb.library = GameObject.Find("Director")?.GetComponent<ShotLibrary>();

            // 3. 窗口 B：镜头时间轴与控制条（底部）
            var controlPanel = RuntimeUiFactory.CreatePanel(canvasGo.transform, "WindowB_Controls", PicoCard, new Vector2(0f, -150f), new Vector2(760f, 90f));
            wb.prevBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "PrevBtn", "◀ 上一镜", new Vector2(-280f, 0f));
            wb.nextBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "NextBtn", "下一镜 ▶", new Vector2(-170f, 0f));
            wb.captureViewBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "CaptureBtn", "抓取视角", new Vector2(-55f, 0f), null, PicoAccent);
            wb.newShotBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "NewBtn", "新机位", new Vector2(55f, 0f));
            wb.toggleAspectBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "AspectBtn", "画幅", new Vector2(165f, 0f));
            wb.savePassportBtn = RuntimeUiFactory.CreateButton(controlPanel.transform, "SaveBtn", "存护照", new Vector2(275f, 0f));

            // 5. 窗口 D：片场灯光控制台（带 3D 四叶遮光斗白模与动态光锥）
            SpatialLightingConsoleBuilder.Build(canvasGo);

            // 6. 窗口 E：导演舒适度与防晕设置面板
            var cp = ComfortSettingsPanel.CreateComfortPanel(canvasGo.transform, null);
            var rt = cp.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(-430f, -100f);

            // 4. 窗口 C：约束质检抬头卡片（右侧浮窗）
            var hudPanel = RuntimeUiFactory.CreatePanel(canvasGo.transform, "WindowC_HUD", PicoCard, new Vector2(430f, 80f), new Vector2(200f, 220f));
            RuntimeUiFactory.CreateLabel(hudPanel.transform, "HUD_Title", "空间质检扫描", 16, new Vector2(0f, 75f), style: FontStyle.Bold);
            
            var visPill = RuntimeUiFactory.CreatePanel(hudPanel.transform, "VisPill", new Color(0.2f, 0.7f, 0.2f, 0.9f), new Vector2(0f, 25f), new Vector2(180f, 40f)).GetComponent<Image>();
            var visTxt = RuntimeUiFactory.CreateLabel(visPill.transform, "Txt", "信封正面 [安全]", 13, Vector2.zero);
            
            var axisPill = RuntimeUiFactory.CreatePanel(hudPanel.transform, "AxisPill", new Color(0.2f, 0.7f, 0.2f, 0.9f), new Vector2(0f, -35f), new Vector2(180f, 40f)).GetComponent<Image>();
            var axisTxt = RuntimeUiFactory.CreateLabel(axisPill.transform, "Txt", "轴线连续 [正常]", 13, Vector2.zero);

            wb.visibilityPill = visPill;
            wb.visibilityStatusText = visTxt;
            wb.axisPill = axisPill;
            wb.axisStatusText = axisTxt;

            // 信息提示
            var notifyTxt = RuntimeUiFactory.CreateLabel(canvasGo.transform, "Notify", "", 18, new Vector2(0f, -220f), color: PicoAccent, style: FontStyle.Bold);
            wb.notificationText = notifyTxt;

            Undo.RegisterCreatedObjectUndo(canvasGo, "创建空间多窗口工作台");
            Debug.Log("[镜场 ShotField] PICO 空间 3D 悬浮多窗口工作台构建成功！");
            var head = Camera.main;
            var actorTargets = CollectActorTargets();
            if (!dirCam || !head || head == dirCam || actorTargets.Count == 0)
                throw new System.InvalidOperationException("Missing SC01 camera or actor binding.");
            var motion = canvasGo.AddComponent<CameraMotionRecorder>();
            motion.poseSource = head.transform;
            motion.filmCamera = dirCam;
            motion.hmdObserver = head;
            var board = canvasGo.AddComponent<TabletopDirector>();
            board.sceneRoot1to1 = dirCam.transform.parent;
            board.tabletopAnchor = new GameObject("TabletopAnchor").transform;
            board.tabletopAnchor.SetParent(canvasGo.transform.parent);
            board.tabletopAnchor.position = new Vector3(0, 0.8f, 1.2f);
            // 沙盘代理根：空节点挂在锚点下；1:1 模式下整体隐藏，切沙盘时由 TabletopDirector 激活
            var proxyRoot = new GameObject("TabletopProxyRoot");
            proxyRoot.transform.SetParent(board.tabletopAnchor);
            board.tabletopProxyRoot = proxyRoot.transform;
            BuildActorProxies(board, actorTargets);
            proxyRoot.SetActive(false);
            // 头显相机由 TabletopDirector 自行识别并拒绝搬动，这里不把它塞进 observerCamera
            board.observerCamera = null;
            var actor = canvasGo.AddComponent<ActorTakePlayer>();
            actor.actorTransform = actorTargets[0];
            var audience = canvasGo.AddComponent<AudienceLab>();
            audience.audienceHead = head.transform;
            audience.screeningCamera = dirCam;
            canvasGo.AddComponent<DesktopStudioPanel>();
            var timeline = canvasGo.AddComponent<DirectorTimeline>();
            timeline.cameraRecorder = motion;
            timeline.actorPlayer = actor;
            wb.playMotion = RuntimeUiFactory.CreateButton(canvasGo.transform, "PlayMotion", "运镜回放", new Vector2(-180,-255));
            wb.toggleBoard = RuntimeUiFactory.CreateButton(canvasGo.transform, "ToggleBoard", "沙盘", new Vector2(-60,-255));
            wb.playActor = RuntimeUiFactory.CreateButton(canvasGo.transform, "PlayActor", "表演回放", new Vector2(60,-255));
            wb.screenShot = RuntimeUiFactory.CreateButton(canvasGo.transform, "ScreenShot", "观众试看", new Vector2(180,-255));
            Sprint1PanelBuilder.AttachPanels(canvasGo, wb, timeline);
            WorkbenchExportBinding.Attach(wb);
            return canvasGo;
        }

        // 演员来源：场景内全部 ActorNavigator；一个都没有时回退语义标记 actorA。
        private static List<Transform> CollectActorTargets()
        {
            var targets = new List<Transform>();
            var navigators = Object.FindObjectsByType<ActorNavigator>(FindObjectsSortMode.None);
            foreach (var nav in navigators)
            {
                if (nav != null && nav.transform != null) targets.Add(nav.transform);
            }
            if (targets.Count == 0)
            {
                var fallback = SemanticMarker.Find("actorA");
                if (fallback != null) targets.Add(fallback.transform);
            }
            return targets;
        }

        // 每位演员一具沙盘 Capsule 代理；位姿跟随交给 TabletopProxyFollower，坐标换算归 TabletopDirector。
        private static void BuildActorProxies(TabletopDirector board, List<Transform> actors)
        {
            foreach (var actor in actors)
            {
                var proxyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                proxyGo.name = "TabletopProxy_" + actor.name;
                proxyGo.transform.SetParent(board.tabletopProxyRoot);
                proxyGo.transform.localScale = actor.lossyScale * board.miniScale;
                board.SyncWorldToProxy(actor, proxyGo.transform);
                proxyGo.AddComponent<TabletopProxyFollower>().Bind(actor, board);
            }
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<XRUIInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "创建 EventSystem");
                Debug.Log("[镜场 ShotField] 已自动补充 EventSystem 与 InputSystemUIInputModule");
            }
        }
    }
}
#endif
