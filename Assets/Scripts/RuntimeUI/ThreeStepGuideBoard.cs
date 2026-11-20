using UnityEngine;
using UnityEngine.UI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class ThreeStepGuideBoard : MonoBehaviour
    {
        public static readonly Vector3 DefaultPosition = new Vector3(1.20f, 2.35f, 2.10f);
        public static readonly Quaternion DefaultRotation = Quaternion.Euler(22f, -145f, 0f);
        public static readonly Vector3 DefaultScale = Vector3.one * 0.002f;
        public static readonly Color ActiveColor = new Color(0.17f, 0.40f, 1.00f, 0.95f);
        public static readonly Color InactiveColor = new Color(0.12f, 0.13f, 0.16f, 0.70f);
        public static readonly Color TextDimColor = new Color(0.70f, 0.70f, 0.70f, 0.80f);

        public Canvas canvas;
        public Image[] cardImages;
        public Text[] cardTitleTexts;
        public Text[] cardDescTexts;

        private void Awake()
        {
            EnsureCards();
            DirectorWorkflowState.Instance.OnStepChanged += UpdateStepHighlight;
        }

        private void Start() => UpdateStepHighlight(DirectorWorkflowState.Instance.CurrentStep);

        private void OnDestroy()
        {
            DirectorWorkflowState.Instance.OnStepChanged -= UpdateStepHighlight;
        }

        public void UpdateStepHighlight(DirectorWorkflowStep step)
        {
            if (cardImages == null || cardImages.Length < 3) return;
            for (int i = 0; i < 3; i++)
            {
                bool isActive = (int)step == i;
                if (cardImages[i] != null)
                    cardImages[i].color = isActive ? ActiveColor : InactiveColor;
                if (cardTitleTexts != null && i < cardTitleTexts.Length && cardTitleTexts[i] != null)
                    cardTitleTexts[i].color = isActive ? Color.white : TextDimColor;
                if (cardDescTexts != null && i < cardDescTexts.Length && cardDescTexts[i] != null)
                    cardDescTexts[i].color = isActive ? Color.white : TextDimColor;
            }
        }

        public void EnsureCards()
        {
            if (cardImages != null && cardImages.Length >= 3 && cardImages[0] != null) return;
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(920f, 220f);

            cardImages = new Image[3];
            cardTitleTexts = new Text[3];
            cardDescTexts = new Text[3];
            string[] titles = { "① 选剧本", "② 摆机位", "③ 截图导出" };
            string[] descs = { "5幕故事圣经", "体感微调与构图", "生成分镜护照" };
            float[] posX = { -290f, 0f, 290f };
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image));
                cardGo.transform.SetParent(transform, false);
                var cardRt = (RectTransform)cardGo.transform;
                cardRt.anchoredPosition = new Vector2(posX[i], 0f);
                cardRt.sizeDelta = new Vector2(270f, 180f);
                cardImages[i] = cardGo.GetComponent<Image>();

                var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
                titleGo.transform.SetParent(cardGo.transform, false);
                var titleTxt = titleGo.GetComponent<Text>();
                titleTxt.text = titles[i];
                titleTxt.font = font;
                titleTxt.fontSize = 24;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.alignment = TextAnchor.MiddleCenter;
                ((RectTransform)titleGo.transform).anchoredPosition = new Vector2(0f, 25f);
                ((RectTransform)titleGo.transform).sizeDelta = new Vector2(250f, 50f);
                cardTitleTexts[i] = titleTxt;

                var descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
                descGo.transform.SetParent(cardGo.transform, false);
                var descTxt = descGo.GetComponent<Text>();
                descTxt.text = descs[i];
                descTxt.font = font;
                descTxt.fontSize = 14;
                descTxt.alignment = TextAnchor.MiddleCenter;
                ((RectTransform)descGo.transform).anchoredPosition = new Vector2(0f, -25f);
                ((RectTransform)descGo.transform).sizeDelta = new Vector2(250f, 40f);
                cardDescTexts[i] = descTxt;
            }
        }

        public static ThreeStepGuideBoard CreateGuideBoard(Transform parent = null)
        {
            var go = new GameObject("ThreeStepGuideBoard", typeof(RectTransform), typeof(Canvas));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = DefaultPosition;
            go.transform.rotation = DefaultRotation;
            go.transform.localScale = DefaultScale;
            var board = go.AddComponent<ThreeStepGuideBoard>();
            board.EnsureCards();
            return board;
        }
    }
}
