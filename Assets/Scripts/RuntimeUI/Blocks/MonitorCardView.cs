using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>监看卡内容：直接消费 SpatialMonitor 的真实 RenderTexture，不抢相机。</summary>
    public sealed class MonitorCardView : MonoBehaviour
    {
        private SpatialBlockCard _card;
        private SpatialMonitor _monitor;
        private RawImage _display;
        private TMPro.TMP_Text _status;

        public bool FeedBound { get; private set; }
        public string SourceCameraName { get; private set; } = "";

        public void Bind(SpatialBlockCard card, SpatialMonitor monitor)
        {
            _card = card;
            _monitor = monitor;
            Build();
        }

        private void Build()
        {
            if (_card == null || _card.ContentRoot == null) return;
            var root = _card.ContentRoot;
            var displayGo = new GameObject("MonitorFeed", typeof(RectTransform), typeof(RawImage));
            displayGo.transform.SetParent(root, false);
            _display = displayGo.GetComponent<RawImage>();
            var rt = (RectTransform)displayGo.transform;
            rt.anchorMin = new Vector2(0f, 0.20f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _display.color = Color.white;
            _display.raycastTarget = false;

            RuntimeUiFactory.CreateTmpButton(root, "AspectWide", "16:9",
                new Vector2(-150f, -112f), new Vector2(140f, 48f),
                onClick: () => _monitor?.SetAspect(false));
            RuntimeUiFactory.CreateTmpButton(root, "AspectPortrait", "9:16",
                new Vector2(0f, -112f), new Vector2(140f, 48f),
                onClick: () => _monitor?.SetAspect(true));
            RuntimeUiFactory.CreateTmpButton(root, "RefreshFeed", "刷新",
                new Vector2(150f, -112f), new Vector2(140f, 48f),
                onClick: () => _monitor?.RequestRender());

            _status = RuntimeUiFactory.CreateTmpLabel(root, "Status", "监看板", 18,
                new Vector2(0f, -164f), new Vector2(560f, 32f),
                new Color(0.61f, 0.63f, 0.67f));
            if (_monitor != null && _monitor.sourceCamera != null)
                SourceCameraName = _monitor.sourceCamera.name;
        }

        private void LateUpdate()
        {
            if (_display == null || _monitor == null) return;
            _display.texture = _monitor.Feed;
            FeedBound = _display.texture != null;
            if (_status != null)
                _status.text = FeedBound ? $"监看板 | 相机 {SourceCameraName}" : "监看板 | 等待画面";
        }
    }
}
