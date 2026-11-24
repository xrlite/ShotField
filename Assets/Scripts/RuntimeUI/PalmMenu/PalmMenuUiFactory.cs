using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 掌心菜单呈现层构建出的对象集合。<see cref="IsValid"/> 为 false 表示构建不完整，
    /// 调用方应保持 PalmMenuController 不渲染，而不是抛异常。
    /// </summary>
    public readonly struct PalmMenuVisuals
    {
        public readonly Canvas Canvas;
        public readonly RectTransform ButtonRoot;
        public readonly Button ButtonPrefab;
        public readonly TMP_Text StatusLabel;

        public PalmMenuVisuals(Canvas canvas, RectTransform buttonRoot, Button buttonPrefab, TMP_Text statusLabel)
        {
            Canvas = canvas;
            ButtonRoot = buttonRoot;
            ButtonPrefab = buttonPrefab;
            StatusLabel = statusLabel;
        }

        public bool IsValid => Canvas != null && ButtonRoot != null && ButtonPrefab != null;
    }

    /// <summary>
    /// 掌心菜单的世界空间 UI 构建器。只建结构与尺寸，不碰数据、不订阅事件。
    ///
    /// 尺寸口径（见 03_制作/镜场ShotField-P5界面版式规范_20261123.md §0.1）：
    /// 1 px = 1 mm，故 Canvas localScale = 0.001，板面 240×340 px 即世界 0.24×0.34 m。
    /// 按钮走 <see cref="RuntimeUiFactory"/> 的 TMP 通道，中文由 ShotFieldSansSC 渲染。
    ///
    /// buttonPrefab 是运行时生成的模板对象（inactive，挂在 Canvas 下作为模板），
    /// 不走 .prefab 资产 —— 避免为了接一次线去改场景/资源 YAML。
    /// </summary>
    public static class PalmMenuUiFactory
    {
        public const float PanelWidth = 240f;
        public const float PanelHeight = 340f;
        /// <summary>像素↔米换算：1 px = 1 mm。</summary>
        public const float PixelsPerMeter = 1000f;

        public const float ButtonHeight = 44f;
        public const float HeaderHeight = 40f;
        public const float StatusHeight = 32f;

        private static readonly Color PanelColor = new Color(0.110f, 0.118f, 0.133f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.165f, 0.176f, 0.200f, 0.95f);
        private static readonly Color TextColor = new Color(0.949f, 0.953f, 0.961f, 1f);
        private static readonly Color HintColor = new Color(0.608f, 0.631f, 0.671f, 1f);

        /// <summary>在 parent 下建一整套菜单视觉；parent 为空时自建根节点。</summary>
        public static PalmMenuVisuals Build(Transform parent, string canvasName = "PalmMenu_Canvas")
        {
            var root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas),
                typeof(Image), typeof(TrackedDeviceGraphicRaycaster));
            if (parent != null) root.transform.SetParent(parent, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rootRt.localScale = Vector3.one * (1f / PixelsPerMeter);
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            var bg = root.GetComponent<Image>();
            bg.color = PanelColor;
            bg.raycastTarget = true;

            CreateHeader(root.transform);
            var buttonRoot = CreateButtonRoot(root.transform);
            var status = RuntimeUiFactory.CreateTmpLabel(
                root.transform, "Status", "", 16,
                new Vector2(0f, -(PanelHeight - StatusHeight) * 0.5f),
                new Vector2(PanelWidth - 16f, StatusHeight), HintColor);
            status.alignment = TextAlignmentOptions.Center;

            var prefab = CreateButtonTemplate(root.transform);

            return new PalmMenuVisuals(canvas, buttonRoot, prefab, status);
        }

        private static void CreateHeader(Transform parent)
        {
            float y = (PanelHeight - HeaderHeight) * 0.5f;
            var title = RuntimeUiFactory.CreateTmpLabel(
                parent, "Title", "菜单", 20,
                new Vector2(0f, y), new Vector2(PanelWidth - 16f, HeaderHeight), TextColor);
            title.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static RectTransform CreateButtonRoot(Transform parent)
        {
            var go = new GameObject("ButtonRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(PanelWidth - 16f, PanelHeight - HeaderHeight - StatusHeight - 16f);
            rt.anchoredPosition = new Vector2(0f, -(HeaderHeight - StatusHeight) * 0.5f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return rt;
        }

        /// <summary>
        /// 按钮模板：一个带 TMP 子标签的 Button，建好即隐藏。
        /// PalmMenuController.InstantiateButton 会 Instantiate 它并按需改标签文字。
        /// </summary>
        private static Button CreateButtonTemplate(Transform parent)
        {
            var btn = RuntimeUiFactory.CreateTmpButton(
                parent, "ButtonTemplate", "条目", Vector2.zero,
                new Vector2(PanelWidth - 16f, ButtonHeight), ButtonColor);
            btn.gameObject.SetActive(false);
            return btn;
        }
    }
}
