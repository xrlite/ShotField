#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.position = new Vector3(2.35f, 1.35f, 1.55f);
            canvasRt.rotation = Quaternion.Euler(15f, -145f, 0f);
            canvasRt.sizeDelta = new Vector2(1000f, 700f);
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
            var controlPanel = CreatePanel(canvasGo.transform, "WindowB_Controls", PicoCard, new Vector2(0f, -150f), new Vector2(660f, 90f));
            wb.prevBtn = CreateButton(controlPanel.transform, "PrevBtn", "◀ 上一镜", new Vector2(-220f, 0f));
            wb.nextBtn = CreateButton(controlPanel.transform, "NextBtn", "下一镜 ▶", new Vector2(-110f, 0f));
            wb.newShotBtn = CreateButton(controlPanel.transform, "NewBtn", "➕ 新机位", new Vector2(20f, 0f), PicoAccent);
            wb.toggleAspectBtn = CreateButton(controlPanel.transform, "AspectBtn", "📐 16:9/9:16", new Vector2(140f, 0f));
            wb.savePassportBtn = CreateButton(controlPanel.transform, "SaveBtn", "💾 存护照", new Vector2(245f, 0f));

            // 4. 窗口 C：约束质检抬头卡片（右侧浮窗）
            var hudPanel = CreatePanel(canvasGo.transform, "WindowC_HUD", PicoCard, new Vector2(440f, 80f), new Vector2(200f, 220f));
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
            return canvasGo;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
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

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color? bg = null)
        {
            var go = CreatePanel(parent, name, bg ?? new Color(0.24f, 0.26f, 0.30f, 0.95f), pos, new Vector2(95f, 45f));
            var btn = go.AddComponent<Button>();
            CreateText(go.transform, "Label", label, 13, FontStyle.Normal, Vector2.zero, Color.white);
            return btn;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
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
                esGo.AddComponent<InputSystemUIInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "创建 EventSystem");
                Debug.Log("[镜场 ShotField] 已自动补充 EventSystem 与 InputSystemUIInputModule");
            }
        }
    }
}
#endif
