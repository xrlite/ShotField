using System;
using UnityEngine;
using UnityEngine.UI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class ScriptSelectionPanel : MonoBehaviour
    {
        public static readonly Vector2 DefaultPosition = new Vector2(0f, 345f);
        public static readonly Vector2 DefaultSize = new Vector2(660f, 85f);
        public const string ScriptTag = "[可替换为你的剧本（待接入）]";

        public Text indicatorText, conflictText, charactersText, tagText;
        public Button prevActBtn, nextActBtn;
        public event Action<ProductionAct, ActSceneDescriptor> OnActSelected;

        public int CurrentActIndex { get; private set; } = 0;
        public ProductionAct CurrentAct => (ProductionAct)CurrentActIndex;

        private FiveActsProductionPipeline _pipeline;

        private void Awake()
        {
            _pipeline = new FiveActsProductionPipeline();
            EnsureUI();
            RefreshUI();
        }

        public void SelectAct(int index)
        {
            CurrentActIndex = Mathf.Clamp(index, 0, 4);
            RefreshUI();
            var desc = _pipeline.GetActDescriptor(CurrentAct);
            OnActSelected?.Invoke(CurrentAct, desc);
            DirectorWorkflowState.Instance.SetStep(DirectorWorkflowStep.FrameCamera);
        }

        public void NextAct() => SelectAct((CurrentActIndex + 1) % 5);
        public void PreviousAct() => SelectAct((CurrentActIndex + 4) % 5);

        public void RefreshUI()
        {
            if (_pipeline == null) _pipeline = new FiveActsProductionPipeline();
            int num = CurrentActIndex + 1;
            var desc = _pipeline.GetActDescriptor(CurrentAct);
            var analysis = _pipeline.ParseActScript(CurrentAct);

            if (indicatorText != null)
                indicatorText.text = $"「演示剧本 {num}/5 · {desc.actTitle}」";
            if (conflictText != null)
                conflictText.text = $"剧本冲突: {analysis.conflictSummary}";
            if (charactersText != null)
                charactersText.text = $"出场角色: {string.Join(", ", analysis.characters)}";
            if (tagText != null)
                tagText.text = ScriptTag;
        }

        public void EnsureUI()
        {
            if (indicatorText != null && prevActBtn != null && nextActBtn != null) return;
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchoredPosition = DefaultPosition;
            rt.sizeDelta = DefaultSize;
            var img = GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            indicatorText = RuntimeUiFactory.CreateLabel(transform, "Indicator", "", 14, new Vector2(0f, 24f), new Vector2(460f, 26f), Color.white, font, FontStyle.Bold);
            tagText = RuntimeUiFactory.CreateLabel(transform, "Tag", "", 11, new Vector2(230f, 24f), new Vector2(170f, 24f), new Color(0.17f, 0.40f, 1.00f, 1.0f), font, FontStyle.Normal);
            conflictText = RuntimeUiFactory.CreateLabel(transform, "Conflict", "", 12, new Vector2(-60f, 0f), new Vector2(500f, 22f), new Color(0.85f, 0.85f, 0.85f), font, FontStyle.Normal);
            charactersText = RuntimeUiFactory.CreateLabel(transform, "Characters", "", 11, new Vector2(-60f, -22f), new Vector2(500f, 20f), new Color(0.65f, 0.65f, 0.65f), font, FontStyle.Normal);

            prevActBtn = CreateBtn(transform, "PrevAct", "◀ 上一幕", new Vector2(-280f, 0f), new Vector2(80f, 40f), font);
            nextActBtn = CreateBtn(transform, "NextAct", "下一幕 ▶", new Vector2(280f, 0f), new Vector2(80f, 40f), font);
            prevActBtn.onClick.AddListener(PreviousAct);
            nextActBtn.onClick.AddListener(NextAct);
        }

        private static Button CreateBtn(Transform p, string n, string label, Vector2 pos, Vector2 sz, Font f)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(p, false);
            go.GetComponent<Image>().color = new Color(0.24f, 0.26f, 0.30f, 0.95f);
            var r = (RectTransform)go.transform;
            r.anchoredPosition = pos; r.sizeDelta = sz;
            RuntimeUiFactory.CreateLabel(go.transform, "Lbl", label, 12, Vector2.zero, sz, Color.white, f);
            return go.GetComponent<Button>();
        }

        public static ScriptSelectionPanel AttachToCanvas(GameObject canvasGo)
        {
            var panelGo = new GameObject("ScriptSelectionPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            return panelGo.AddComponent<ScriptSelectionPanel>();
        }
    }
}
