using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// VR 舒适度与防晕设置悬浮面板（挂载工作台或随身空间）：
    /// 包含三档移动模式切换、暗角强度步进、身高自适应、一键"我晕了"急救按钮以及新手首问卡片。
    /// </summary>
    public class ComfortSettingsPanel : MonoBehaviour
    {
        public LocomotionComfortController controller;

        [Header("UI 文本组件")]
        public TMP_Text locomotionModeText;
        public TMP_Text vignetteText;
        public TMP_Text snapAngleText;
        public TMP_Text heightModeText;
        public TMP_Text statusFeedbackText;
        public GameObject firstQuestionDialog;

        private void Start()
        {
            if (controller == null)
                controller = FindAnyObjectByType<LocomotionComfortController>();
            RefreshUI();
        }

        public void SelectLocomotion(int modeIndex)
        {
            if (controller == null) return;
            controller.SetLocomotionMode((LocomotionMode)modeIndex);
            RefreshUI();
            ShowFeedback($"已切换移动模式: {controller.locomotionMode}");
        }

        public void StepVignette(float delta)
        {
            if (controller == null) return;
            controller.SetVignetteIntensity(controller.vignetteIntensity + delta);
            RefreshUI();
            ShowFeedback($"暗角强度: {Mathf.RoundToInt(controller.vignetteIntensity * 100f)}%");
        }

        public void SelectSnapAngle(float angle)
        {
            if (controller == null) return;
            controller.SetSnapTurnAngle(angle);
            RefreshUI();
            ShowFeedback($"瞬转角度: {angle}°");
        }

        public void SelectHeightMode(int heightIndex)
        {
            if (controller == null) return;
            controller.SetHeightMode((HeightMode)heightIndex);
            RefreshUI();
            ShowFeedback($"身高模式: {controller.heightMode}");
        }

        public void OnClickPanicButton()
        {
            if (controller == null) return;
            controller.TriggerPanic();
            RefreshUI();
            ShowFeedback("已触发急救：瞬移+满暗角+最低速！");
        }

        public void AnswerFirstQuestion(int answerIndex)
        {
            if (controller == null) return;
            controller.AnswerSicknessQuestion((ProneToSicknessAnswer)answerIndex);
            if (firstQuestionDialog != null) firstQuestionDialog.SetActive(false);
            RefreshUI();
            ShowFeedback("已根据您的体质自动配置舒适度方案");
        }

        public void RefreshUI()
        {
            if (controller == null) return;
            if (locomotionModeText != null)
                locomotionModeText.text = $"移动: {controller.locomotionMode}";
            if (vignetteText != null)
                vignetteText.text = $"暗角: {Mathf.RoundToInt(controller.vignetteIntensity * 100f)}%";
            if (snapAngleText != null)
                snapAngleText.text = $"转向: {Mathf.RoundToInt(controller.snapTurnAngle)}°";
            if (heightModeText != null)
                heightModeText.text = $"身高: {controller.heightMode}";
        }

        private void ShowFeedback(string msg)
        {
            if (statusFeedbackText != null) statusFeedbackText.text = msg;
        }

        public static ComfortSettingsPanel CreateComfortPanel(Transform parent, LocomotionComfortController ctrl)
        {
            var host = new GameObject("ComfortSettingsPanel");
            if (parent != null) host.transform.SetParent(parent, false);
            var panel = host.AddComponent<ComfortSettingsPanel>();
            panel.controller = ctrl;

            var bg = host.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var rt = host.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(360f, 320f);

            panel.locomotionModeText = RuntimeUiFactory.CreateTmpLabel(host.transform, "LocoLabel", "移动: Teleport", 14, new Vector2(0f, 100f));
            panel.vignetteText = RuntimeUiFactory.CreateTmpLabel(host.transform, "VignetteLabel", "暗角: 60%", 14, new Vector2(0f, 60f));
            panel.snapAngleText = RuntimeUiFactory.CreateTmpLabel(host.transform, "SnapLabel", "转向: 45°", 14, new Vector2(0f, 20f));
            panel.heightModeText = RuntimeUiFactory.CreateTmpLabel(host.transform, "HeightLabel", "身高: Auto", 14, new Vector2(0f, -20f));
            panel.statusFeedbackText = RuntimeUiFactory.CreateTmpLabel(host.transform, "StatusLabel", "就绪", 12, new Vector2(0f, -60f));

            var panicGo = new GameObject("PanicButton");
            panicGo.transform.SetParent(host.transform, false);
            var panicImg = panicGo.AddComponent<Image>();
            panicImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
            var panicBtn = panicGo.AddComponent<Button>();
            panicBtn.onClick.AddListener(panel.OnClickPanicButton);
            var panicRt = panicGo.GetComponent<RectTransform>();
            if (panicRt != null)
            {
                panicRt.sizeDelta = new Vector2(160f, 36f);
                panicRt.anchoredPosition = new Vector2(0f, -110f);
            }
            RuntimeUiFactory.CreateTmpLabel(panicGo.transform, "PanicText", "我晕了！(一键防晕)", 13, Vector2.zero);

            return panel;
        }
    }
}
