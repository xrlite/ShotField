#if UNITY_EDITOR
using System.Collections.Generic;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.EditorTools
{
    public static class Sprint1PanelBuilder
    {
        private static readonly Color PanelBg = new Color(0.13f, 0.14f, 0.17f, 0.94f);
        private static readonly Color BtnBg = new Color(0.24f, 0.26f, 0.30f, 0.95f);
        private static readonly Color AccentBg = new Color(0.17f, 0.40f, 1.00f, 1.0f);

        public static void Attach(GameObject canvasGo, SpatialWorkbench wb, DirectorTimeline timeline)
        {
            if (canvasGo == null) return;
            var rt = canvasGo.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta.y < 860f) rt.sizeDelta = new Vector2(1140f, 860f);

            BuildPropBrowser(canvasGo.transform, wb);
            BuildSceneInspector(canvasGo.transform);
            BuildTimelineControls(canvasGo.transform, timeline);
        }

        private static void BuildPropBrowser(Transform root, SpatialWorkbench wb)
        {
            var panel = MakePanel(root, "WindowD_PropBrowser", PanelBg, new Vector2(-455f, 40f), new Vector2(205f, 420f));
            var browser = root.gameObject.AddComponent<PropBrowserUI>();
            browser.spawnAnchor = wb != null && wb.monitor != null && wb.monitor.sourceCamera != null
                ? wb.monitor.sourceCamera.transform : root;
            browser.sceneRoot = GameObject.Find("SC01_ArtScene")?.transform;
            MakeText(panel.transform, "Title", "资产库道具", 14, new Vector2(0f, 185f), Color.white);
            browser.statusText = MakeText(panel.transform, "Status", "点击放置道具", 12, new Vector2(0f, -185f), AccentBg);

            string[] ids = { "sofa", "chair", "coffeeTable", "shoeCabinet", "envelope", "plant" };
            browser.bindings = new List<PropPrefabBinding>();
            for (int i = 0; i < ids.Length; i++)
            {
                var entry = PropLibrary.Find(ids[i]);
                if (entry == null) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
                browser.bindings.Add(new PropPrefabBinding
                {
                    propId = entry.id,
                    displayName = entry.displayName,
                    prefab = prefab
                });
                float x = (i % 2 == 0) ? -48f : 48f;
                float y = 125f - (i / 2) * 58f;
                var btn = MakeButton(panel.transform, $"Spawn_{entry.id}", entry.displayName, new Vector2(x, y), new Vector2(88f, 44f), BtnBg);
                browser.BindButton(btn, entry.id);
            }
        }

        private static void BuildSceneInspector(Transform root)
        {
            var panel = MakePanel(root, "WindowE_Inspector", PanelBg, new Vector2(455f, -165f), new Vector2(205f, 250f));
            var inspector = root.gameObject.AddComponent<SceneObjectInspector>();
            MakeText(panel.transform, "Title", "场景对象列表", 14, new Vector2(0f, 102f), Color.white);
            inspector.selectedLabel = MakeText(panel.transform, "Selected", "选中: -", 12, new Vector2(0f, 70f), Color.white);
            inspector.statusText = MakeText(panel.transform, "Status", "就绪", 11, new Vector2(0f, -102f), AccentBg);

            var nextBtn = MakeButton(panel.transform, "Insp_Next", "切换对象", new Vector2(0f, 32f), new Vector2(175f, 34f), AccentBg);
            var visBtn = MakeButton(panel.transform, "Insp_Vis", "显/隐", new Vector2(-46f, -14f), new Vector2(84f, 34f), BtnBg);
            var lockBtn = MakeButton(panel.transform, "Insp_Lock", "锁/解", new Vector2(46f, -14f), new Vector2(84f, 34f), BtnBg);
            var resetBtn = MakeButton(panel.transform, "Insp_Reset", "一键复位", new Vector2(0f, -60f), new Vector2(175f, 34f), BtnBg);

            nextBtn.onClick.AddListener(inspector.CycleSelected);
            visBtn.onClick.AddListener(inspector.ToggleSelectedVisibility);
            lockBtn.onClick.AddListener(inspector.ToggleSelectedLock);
            resetBtn.onClick.AddListener(inspector.ResetSelected);
        }

        private static void BuildTimelineControls(Transform root, DirectorTimeline timeline)
        {
            var panel = MakePanel(root, "WindowF_Timeline", PanelBg, new Vector2(0f, -395f), new Vector2(760f, 76f));
            var ui = root.gameObject.AddComponent<TimelineControlsUI>();
            ui.timeline = timeline;
            ui.playPauseBtn = MakeButton(panel.transform, "TL_Play", "播放/暂停", new Vector2(-315f, 12f), new Vector2(100f, 34f), AccentBg);
            ui.motionPresetBtn = MakeButton(panel.transform, "TL_Motion", "运镜预设", new Vector2(-200f, 12f), new Vector2(100f, 34f), BtnBg);
            ui.posePresetBtn = MakeButton(panel.transform, "TL_Pose", "姿态预设", new Vector2(-85f, 12f), new Vector2(100f, 34f), BtnBg);
            ui.scrubber = MakeSlider(panel.transform, new Vector2(165f, 12f), new Vector2(360f, 16f));
            ui.presetStatusText = MakeText(panel.transform, "TL_Status", "运镜:DollyPush | 姿态:Idle", 12, new Vector2(0f, -20f), Color.white);
            ui.Wire();
        }

        private static GameObject MakePanel(Transform parent, string name, Color bg, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg)
        {
            var go = MakePanel(parent, name, bg, pos, size);
            var btn = go.AddComponent<Button>();
            MakeText(go.transform, "Label", label, 12, Vector2.zero, Color.white, size);
            return btn;
        }

        private static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = MakePanel(parent, "TL_Scrubber", new Color(0.08f, 0.09f, 0.11f, 1f), pos, size);
            var slider = go.AddComponent<Slider>();
            var fill = MakePanel(go.transform, "Fill", AccentBg, Vector2.zero, Vector2.zero);
            var fr = (RectTransform)fill.transform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            slider.fillRect = fr;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static Text MakeText(Transform parent, string name, string content, int size, Vector2 pos, Color color, Vector2? box = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box ?? new Vector2(190f, 30f);
            return txt;
        }
    }
}
#endif
