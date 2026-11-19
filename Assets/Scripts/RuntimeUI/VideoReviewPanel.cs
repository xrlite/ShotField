using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    internal sealed class VideoReviewPanel : MonoBehaviour
    {
        private RawImage reference;
        private RawImage generated;
        private VideoComparisonPlayer player;

        internal void Initialize(SpatialMonitor monitor, VideoComparisonPlayer source)
        {
            if (monitor == null || monitor.monitorDisplay == null || source == null) return;
            reference = monitor.monitorDisplay;
            player = source;
            if (generated == null)
            {
                var go = new GameObject("GeneratedVideoDisplay", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(reference.transform.parent, false);
                generated = go.GetComponent<RawImage>();
                generated.raycastTarget = false;
            }
            generated.texture = player.comparisonOutput;
            ApplyMode(player.displayMode);
        }

        internal void ApplyMode(ComparisonDisplayMode mode)
        {
            if (reference == null || generated == null) return;
            var refRect = reference.rectTransform;
            var videoRect = generated.rectTransform;
            reference.gameObject.SetActive(mode != ComparisonDisplayMode.FullVideo);
            generated.gameObject.SetActive(true);
            if (mode == ComparisonDisplayMode.SideBySideSplit)
            {
                SetRect(refRect, new Vector2(-162f, 0f), new Vector2(315f, 360f));
                SetRect(videoRect, new Vector2(162f, 0f), new Vector2(315f, 360f));
            }
            else if (mode == ComparisonDisplayMode.PictureInPicture)
            {
                SetRect(refRect, Vector2.zero, new Vector2(640f, 360f));
                SetRect(videoRect, new Vector2(195f, -105f), new Vector2(220f, 124f));
                generated.transform.SetAsLastSibling();
            }
            else SetRect(videoRect, Vector2.zero, new Vector2(640f, 360f));
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
