#if UNITY_EDITOR
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
        private static readonly Color PicoBg = new Color(0.10f, 0.11f, 0.13f, 0.92f);
        private static readonly Color PicoCard = new Color(0.15f, 0.16f, 0.19f, 0.95f);
        private static readonly Color PicoAccent = new Color(0.17f, 0.40f, 1.00f, 1.0f);

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
            var monitorPanel = CreatePanel(canvasGo.transform, "WindowA_Monitor", PicoBg, new Vector2(0f, 100f), new Vector2(660f, 380f));
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
            var controlPanel = CreatePanel(canvasGo.transform, "WindowB_Controls", PicoCard, new Vector2(0f, -150f), new Vector2(760f, 90f));
            wb.prevBtn = CreateButton(controlPanel.transform, "PrevBtn", "◀ 上一镜", new Vector2(-280f, 0f));
            wb.nextBtn = CreateButton(controlPanel.transform, "NextBtn", "下一镜 ▶", new Vector2(-170f, 0f));
            wb.captureViewBtn = CreateButton(controlPanel.transform, "CaptureBtn", "抓取视角", new Vector2(-55f, 0f), PicoAccent);
            wb.newShotBtn = CreateButton(controlPanel.transform, "NewBtn", "新机位", new Vector2(55f, 0f));
            wb.toggleAspectBtn = CreateButton(controlPanel.transform, "AspectBtn", "画幅", new Vector2(165f, 0f));
            wb.savePassportBtn = CreateButton(controlPanel.transform, "SaveBtn", "存护照", new Vector2(275f, 0f));

            // 5. 窗口 D：片场灯光控制台（带 3D 四叶遮光斗白模与动态光锥）
            SpatialLightingConsoleBuilder.Build(canvasGo);

            // 6. 窗口 E：导演舒适度与防晕设置面板
            var cp = ComfortSettingsPanel.CreatePanel(canvasGo.transform, null);
            var rt = cp.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(-430f, -100f);

            // 4. 窗口 C：约束质检抬头卡片（右侧浮窗）
            var hudPanel = CreatePanel(canvasGo.transform, "WindowC_HUD", PicoCard, new Vector2(430f, 80f), new Vector2(200f, 220f));
            CreateText(hudPanel.transform, "HUD_Title", "空间质检扫描", 16, FontStyle.Bold, new Vector2(0f, 75f), Color.white);
            
            var visPill = CreatePanel(hudPanel.transform, "VisPill", new Color(0.2f, 0.7f, 0.2f, 0.9f), new Vector2(0f, 25f), new Vector2(180f, 40f)).GetComponent<Image>();
            var visTxt = CreateText(visPill.transform, "Txt", "信封正面 [安全]", 13, FontStyle.Normal, Vector2.zero, Color.white);
            
            var axisPill = CreatePanel(hudPanel.transform, "AxisPill", new Color(0.2f, 0.7f, 0.2f, 0.9f), new Vector2(0f, -35f), new Vector2(180f, 40f)).GetComponent<Image>();
            var axisTxt = CreateText(axisPill.transform, "Txt", "轴线连续 [正常]", 13, FontStyle.Normal, Vector2.zero, Color.white);

            wb.visibilityPill = visPill;
            wb.visibilityStatusText = visTxt;
            wb.axisPill = axisPill;
            wb.axisStatusText = axisTxt;

            // 信息提示
            var notifyTxt = CreateText(canvasGo.transform, "Notify", "", 18, FontStyle.Bold, new Vector2(0f, -220f), PicoAccent);
            wb.notificationText = notifyTxt;

            Undo.RegisterCreatedObjectUndo(canvasGo, "创建空间多窗口工作台");
            Debug.Log("[镜场 ShotField] PICO 空间 3D 悬浮多窗口工作台构建成功！");
            var head = Camera.main;
            var actorTarget = SemanticMarker.Find("actorA");
            if (!dirCam || !head || head == dirCam || !actorTarget)
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
            board.tabletopProxyRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            board.tabletopProxyRoot.name = "ActorTabletopProxy";
            board.tabletopProxyRoot.SetParent(board.tabletopAnchor);
            board.tabletopProxyRoot.localScale = actorTarget.transform.lossyScale * board.miniScale;
            board.SyncWorldToProxy(actorTarget.transform, board.tabletopProxyRoot);
            board.tabletopProxyRoot.gameObject.SetActive(false);
            board.observerCamera = head.transform;
            var actor = canvasGo.AddComponent<ActorTakePlayer>();
            actor.actorTransform = actorTarget.transform;
            var audience = canvasGo.AddComponent<AudienceLab>();
            audience.audienceHead = head.transform;
            audience.screeningCamera = dirCam;
            canvasGo.AddComponent<DesktopStudioPanel>();
            var timeline = canvasGo.AddComponent<DirectorTimeline>();
            timeline.cameraRecorder = motion;
            timeline.actorPlayer = actor;
            wb.playMotion = CreateButton(canvasGo.transform, "PlayMotion", "运镜回放", new Vector2(-180,-255));
            wb.toggleBoard = CreateButton(canvasGo.transform, "ToggleBoard", "沙盘", new Vector2(-60,-255));
            wb.playActor = CreateButton(canvasGo.transform, "PlayActor", "表演回放", new Vector2(60,-255));
            wb.screenShot = CreateButton(canvasGo.transform, "ScreenShot", "观众试看", new Vector2(180,-255));
            Sprint1PanelBuilder.Attach(canvasGo, wb, timeline);
            WorkbenchExportBinding.Attach(wb);
            return canvasGo;
        }

        internal static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        internal static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color? bg = null)
        {
            var go = CreatePanel(parent, name, bg ?? new Color(0.24f, 0.26f, 0.30f, 0.95f), pos, new Vector2(95f, 45f));
            var btn = go.AddComponent<Button>();
            var text = CreateText(go.transform, "Label", label, 13, FontStyle.Normal, Vector2.zero, Color.white);
            text.rectTransform.sizeDelta = new Vector2(95f, 45f);
            return btn;
        }

        internal static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.raycastTarget = false;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 40f);
            return txt;
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
