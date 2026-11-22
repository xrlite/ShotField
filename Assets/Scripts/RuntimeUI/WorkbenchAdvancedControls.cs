using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    public partial class SpatialWorkbench
    {
        public Button storyBtn, exportBtn, playReviewBtn, compareBtn;
        public Slider reviewProgressSlider;
        public string reviewVideoSource;
        private VideoReviewPanel reviewPanel;
        private bool advancedControlsBound;

        public void EnsureAdvancedControls(Canvas canvas)
        {
            if (canvas == null) return;
            if (storyBtn == null) storyBtn = RuntimeUiFactory.CreateButton(canvas.transform, "StoryBtn", "Act2 Outline", new Vector2(-220f, -310f));
            if (exportBtn == null) exportBtn = RuntimeUiFactory.CreateButton(canvas.transform, "ExportBtn", "Export Ref", new Vector2(-110f, -310f));
            if (playReviewBtn == null) playReviewBtn = RuntimeUiFactory.CreateButton(canvas.transform, "ReviewBtn", "Play Video", new Vector2(0f, -310f));
            if (compareBtn == null) compareBtn = RuntimeUiFactory.CreateButton(canvas.transform, "CompareBtn", "Compare", new Vector2(110f, -310f));
            if (reviewProgressSlider == null) reviewProgressSlider = MakeProgressSlider(canvas.transform);
            if (advancedControlsBound) return;
            storyBtn.onClick.AddListener(ParseStoryBible);
            if (exportBtn.onClick.GetPersistentEventCount() == 0)
                exportBtn.onClick.AddListener(ExportReferencePackage);
            playReviewBtn.onClick.AddListener(() => PlayReviewVideo(reviewVideoSource));
            compareBtn.onClick.AddListener(CycleCompareMode);
            reviewProgressSlider.onValueChanged.AddListener(SeekReview);
            advancedControlsBound = true;

            // M3 闭环前隐藏：Act2 大纲 / 参考包导出 / 成片播放 / 对照模式 / 进度滑杆对应能力
            // 在冻结四支柱表中均为 🔴未做。控件与监听器保留不删，待 M3 关口再启用。
            storyBtn.gameObject.SetActive(false);
            exportBtn.gameObject.SetActive(false);
            playReviewBtn.gameObject.SetActive(false);
            compareBtn.gameObject.SetActive(false);
            reviewProgressSlider.gameObject.SetActive(false);
        }

        public byte[] CaptureReferencePng() => RuntimeClayCapture.Capture(monitor);

        private VideoComparisonPlayer EnsureReview()
        {
            if (reviewPlayer == null) reviewPlayer = GetComponent<VideoComparisonPlayer>();
            if (reviewPlayer == null) reviewPlayer = gameObject.AddComponent<VideoComparisonPlayer>();
            reviewPlayer.Initialize();
            if (reviewPanel == null) reviewPanel = GetComponent<VideoReviewPanel>();
            if (reviewPanel == null) reviewPanel = gameObject.AddComponent<VideoReviewPanel>();
            reviewPanel.Initialize(monitor, reviewPlayer);
            return reviewPlayer;
        }

        private static Slider MakeProgressSlider(Transform parent)
        {
            var go = new GameObject("ReviewProgress", typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(0f, -342f);
            rt.sizeDelta = new Vector2(220f, 16f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(go.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.17f, 0.40f, 1f, 1f);
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            return slider;
        }
    }
}
