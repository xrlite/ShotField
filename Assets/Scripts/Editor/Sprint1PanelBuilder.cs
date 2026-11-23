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
        private static Color PanelBg => PrevisPalette.PicoCard;
        private static Color BtnBg => PrevisPalette.PicoButton;
        private static Color AccentBg => PrevisPalette.PicoAccent;

        public static void AttachPanels(GameObject canvasGo, SpatialWorkbench wb, DirectorTimeline timeline)
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
            RuntimeUiFactory.CreateTmpLabel(panel.transform, "Title", "资产库道具", 14, new Vector2(0f, 185f), color: Color.white);
            browser.statusText = RuntimeUiFactory.CreateTmpLabel(panel.transform, "Status", "点击放置道具", 12, new Vector2(0f, -185f), color: AccentBg);

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
                var btn = RuntimeUiFactory.CreateTmpButton(panel.transform, $"Spawn_{entry.id}", entry.displayName, new Vector2(x, y), new Vector2(88f, 44f), BtnBg);
                browser.BindButton(btn, entry.id);
            }
        }

        private static void BuildSceneInspector(Transform root)
        {
            var panel = MakePanel(root, "WindowE_Inspector", PanelBg, new Vector2(455f, -165f), new Vector2(205f, 250f));
            var inspector = root.gameObject.AddComponent<SceneObjectInspector>();
            RuntimeUiFactory.CreateTmpLabel(panel.transform, "Title", "场景对象列表", 14, new Vector2(0f, 102f), color: Color.white);
            inspector.selectedLabel = RuntimeUiFactory.CreateTmpLabel(panel.transform, "Selected", "选中: -", 12, new Vector2(0f, 70f), color: Color.white);
            inspector.statusText = RuntimeUiFactory.CreateTmpLabel(panel.transform, "Status", "就绪", 11, new Vector2(0f, -102f), color: AccentBg);

            var nextBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "Insp_Next", "切换对象", new Vector2(0f, 32f), new Vector2(175f, 34f), AccentBg);
            var visBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "Insp_Vis", "显/隐", new Vector2(-46f, -14f), new Vector2(84f, 34f), BtnBg);
            var lockBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "Insp_Lock", "锁/解", new Vector2(46f, -14f), new Vector2(84f, 34f), BtnBg);
            var resetBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "Insp_Reset", "一键复位", new Vector2(0f, -60f), new Vector2(175f, 34f), BtnBg);

            nextBtn.onClick.AddListener(inspector.CycleSelected);
            visBtn.onClick.AddListener(inspector.ToggleSelectedVisibility);
            lockBtn.onClick.AddListener(inspector.ToggleSelectedLock);
            resetBtn.onClick.AddListener(inspector.ResetSelected);
        }

        private static void BuildTimelineControls(Transform root, DirectorTimeline timeline)
        {
            var panel = MakePanel(root, "WindowF_Timeline", PanelBg, new Vector2(0f, -395f), new Vector2(880f, 76f));
            var ui = root.gameObject.AddComponent<TimelineControlsUI>();
            ui.timeline = timeline;
            ui.playPauseBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "TL_Play", "播放/暂停", new Vector2(-375f, 12f), new Vector2(96f, 34f), AccentBg);
            ui.motionPresetBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "TL_Motion", "运镜预设", new Vector2(-270f, 12f), new Vector2(96f, 34f), BtnBg);
            ui.posePresetBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "TL_Pose", "姿态预设", new Vector2(-165f, 12f), new Vector2(96f, 34f), BtnBg);
            var lighting = root.gameObject.AddComponent<SceneLightingPresets>();
            var moodBtn = RuntimeUiFactory.CreateTmpButton(panel.transform, "TL_Mood", "光影氛围", new Vector2(-60f, 12f), new Vector2(96f, 34f), BtnBg);
            moodBtn.onClick.AddListener(lighting.CycleMood);
            ui.scrubber = MakeSlider(panel.transform, new Vector2(185f, 12f), new Vector2(360f, 16f));
            ui.presetStatusText = RuntimeUiFactory.CreateTmpLabel(panel.transform, "TL_Status", "运镜:DollyPush | 姿态:Idle", 12, new Vector2(0f, -20f), color: Color.white);
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
    }
}
#endif
