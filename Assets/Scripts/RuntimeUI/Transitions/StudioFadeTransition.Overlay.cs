using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 转场全屏淡入淡出与拱门 overlay 的构建。从状态机拆分，守单文件 200 行预算。
    /// </summary>
    public sealed partial class StudioFadeTransition
    {
        private void EnsureOverlay()
        {
            if (_group != null) return;
            var parent = headAnchor != null ? headAnchor : transform;

            var go = new GameObject("StudioFadeOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 9000;
            canvas.enabled = false;
            _fadeCanvas = canvas;

            var rt = (RectTransform)go.transform;
            rt.localPosition = new Vector3(0f, 0f, overlayDistance);
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(1200f, 1200f);
            rt.localScale = Vector3.one * 0.001f;

            var img = go.GetComponent<Image>();
            img.color = fadeColor;
            img.raycastTarget = false;

            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

        }
    }
}
