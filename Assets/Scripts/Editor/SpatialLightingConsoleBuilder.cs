#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ShotField.RuntimeUI;
using ShotField.Core;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 把 SpatialLightingConsole 真正装配进 SC01 场景（WindowD 面板 + 3 个槽位灯 + UI 绑定）。
    /// 此前该类只有单元测试（验证器自建临时 GameObject），场景内 0 引用，头显里看不到面板。
    /// </summary>
    internal static class SpatialLightingConsoleBuilder
    {
        private static Color Card => PrevisPalette.PicoCard;
        private static Color Accent => PrevisPalette.PicoAccent;

        internal static GameObject Build(GameObject canvasGo)
        {
            var host = new GameObject("LightingConsole");
            host.transform.SetParent(canvasGo.transform, false);
            var console = host.AddComponent<SpatialLightingConsole>();
            console.InitializeDefaultSlots();

            var panel = RuntimeUiFactory.CreatePanel(canvasGo.transform, "WindowD_Lighting",
                Card, new Vector2(430f, -200f), new Vector2(200f, 250f));
            console.panelRoot = panel;
            RuntimeUiFactory.CreateLabel(panel.transform, "Title", "灯光控制台", 15,
                new Vector2(0f, 105f), color: Color.white, style: FontStyle.Bold);
            console.slotTitleText = RuntimeUiFactory.CreateLabel(panel.transform, "SlotTitle", "槽位1", 14,
                new Vector2(0f, 74f), color: Accent, style: FontStyle.Bold);

            for (int i = 0; i < 3; i++)
            {
                int picked = i;
                var slotBtn = RuntimeUiFactory.CreateButton(panel.transform, "SlotPicker" + i,
                    "槽 " + (i + 1), new Vector2(-62f + i * 62f, 44f));
                Size(slotBtn.gameObject, 56f, 28f);
                slotBtn.onClick.AddListener(() => console.SelectSlot(picked));
            }

            console.innerAngleText = Stepper(panel.transform, "角度", 10f,
                () => console.SetInnerSpotAngle(console.CurrentSlot.innerSpotAngle - 5f),
                () => console.SetInnerSpotAngle(console.CurrentSlot.innerSpotAngle + 5f), "-", "+");

            var modeText = RuntimeUiFactory.CreateLabel(panel.transform, "ModeRow", "环境  3 / 3", 12,
                new Vector2(0f, -26f), color: Color.white, style: FontStyle.Normal);
            console.modeText = modeText;
            var modeL = RuntimeUiFactory.CreateButton(panel.transform, "ModePrev", "<", new Vector2(-72f, -26f));
            Size(modeL.gameObject, 28f, 24f);
            modeL.onClick.AddListener(() => console.CycleMode(-1));
            var modeR = RuntimeUiFactory.CreateButton(panel.transform, "ModeNext", ">", new Vector2(72f, -26f));
            Size(modeR.gameObject, 28f, 24f);
            modeR.onClick.AddListener(() => console.CycleMode(1));

            console.rangeText = Stepper(panel.transform, "范围", -62f,
                () => console.SetRangePercent(console.CurrentSlot.rangePercent - 0.05f),
                () => console.SetRangePercent(console.CurrentSlot.rangePercent + 0.05f), "-", "+");

            console.hueText = Stepper(panel.transform, "色相", -96f,
                () => console.SetHue(console.CurrentSlot.hue - 10f),
                () => console.SetHue(console.CurrentSlot.hue + 10f), "-", "+");

            console.saturationText = Stepper(panel.transform, "饱和", -128f,
                () => console.SetSaturation(console.CurrentSlot.saturation - 0.05f),
                () => console.SetSaturation(console.CurrentSlot.saturation + 0.05f), "-", "+");

            var swatch = RuntimeUiFactory.CreatePanel(panel.transform, "Swatch", Color.white,
                new Vector2(-82f, -74f), new Vector2(18f, 18f)).GetComponent<Image>();
            console.colorSwatchImage = swatch;

            var warm = GameObject.Find("TableWarmLight") != null
                ? GameObject.Find("TableWarmLight").GetComponent<Light>() : null;
            var focus = GameObject.Find("Envelope") != null ? GameObject.Find("Envelope").transform : null;
            console.slots[0].lightSource = warm;
            console.slots[0].focusTarget = focus;
            console.slots[0].fixture = SpatialStudioLightFixture.AttachToLight(warm, "StudioFixture_Slot1");
            console.slots[1].lightSource = MakeSpot(host.transform, "FillLight_A", new Vector3(-2.0f, 2.3f, 1.4f), focus);
            console.slots[1].fixture = SpatialStudioLightFixture.AttachToLight(console.slots[1].lightSource, "StudioFixture_Slot2");
            console.slots[2].lightSource = MakeSpot(host.transform, "FillLight_B", new Vector3(2.4f, 2.2f, 3.0f), focus);
            console.slots[2].fixture = SpatialStudioLightFixture.AttachToLight(console.slots[2].lightSource, "StudioFixture_Slot3");

            console.focusTargetText = RuntimeUiFactory.CreateLabel(panel.transform, "FocusInfo",
                "灯光聚焦的对象。", 11, new Vector2(0f, -158f), color: new Color(0.6f, 0.65f, 0.72f), style: FontStyle.Normal);
            console.SelectSlot(0);
            return host;
        }

        private static Text Stepper(Transform parent, string label, float y, Action minus, Action plus, string minusText, string plusText)
        {
            var l = RuntimeUiFactory.CreateLabel(parent, label + "_Label", label, 12,
                new Vector2(-76f, y), color: new Color(0.6f, 0.65f, 0.72f), style: FontStyle.Normal);
            Size(l.gameObject, 40f, 24f);
            var m = RuntimeUiFactory.CreateButton(parent, label + "_Minus", minusText, new Vector2(-48f, y));
            Size(m.gameObject, 26f, 24f);
            m.onClick.AddListener(() => minus());
            var v = RuntimeUiFactory.CreateLabel(parent, label + "_Value", "--", 12,
                new Vector2(0f, y), color: Color.white, style: FontStyle.Bold);
            Size(v.gameObject, 56f, 24f);
            var p = RuntimeUiFactory.CreateButton(parent, label + "_Plus", plusText, new Vector2(48f, y));
            Size(p.gameObject, 26f, 24f);
            p.onClick.AddListener(() => plus());
            return v;
        }

        private static void Size(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        private static Light MakeSpot(Transform parent, string name, Vector3 pos, Transform focus)
        {
            // 去重保护：本工具重跑时若再无条件 new GameObject(name)，会凭空多出 FillLight_A /
            // FillLight_B 两盏默认 enabled=true / intensity=2.0 / spot=40 的重复灯，直接毁掉
            // WarmHall 的 A2（那两盏的 Light 组件已被有意禁用）。已存在则原样复用：不新建、
            // 不改它的 enabled / intensity / spotAngle / range / color。
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                var reused = existing.GetComponent<Light>();
                if (reused != null) return reused;
                reused = existing.AddComponent<Light>();
                ApplySpotDefaults(reused, focus);
                return reused;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            ApplySpotDefaults(light, focus);
            return light;
        }

        private static void ApplySpotDefaults(Light light, Transform focus)
        {
            light.type = LightType.Spot;
            light.color = Color.HSVToRGB(0.3f, 0.3f, 1f);
            light.intensity = 2.0f;
            light.range = 5.0f;
            light.spotAngle = 40f;
            light.shadows = LightShadows.Soft;
            if (focus != null) light.transform.LookAt(focus.position);
        }
    }
}
#endif
