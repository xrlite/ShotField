using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 监看板缩放独立模块：不改 <see cref="SpatialMonitor"/> 本体，
    /// 只调整其视口 RectTransform 的尺寸缩放，大小由用户自调。
    /// 基准尺寸在绑定时抓取；画幅切换（16:9 / 9:16）后调用 <see cref="ResetBaseSize"/> 重新取基准。
    /// </summary>
    public class MonitorScaler : MonoBehaviour
    {
        public RectTransform viewportFrame;

        [Range(0.5f, 2.0f)] public float scale = 1.0f;
        public bool showSlider = true;

        private Vector2 _baseSize;
        private bool _hasBase;

        void Start() => CaptureBaseSize();

        /// <summary>抓取当前视口尺寸作为缩放基准。</summary>
        public void CaptureBaseSize()
        {
            if (viewportFrame == null) return;
            _baseSize = viewportFrame.sizeDelta;
            _hasBase = true;
            ApplyScale();
        }

        /// <summary>画幅切换后重新抓取基准尺寸。</summary>
        public void ResetBaseSize() => CaptureBaseSize();

        public void SetScale(float value)
        {
            scale = Mathf.Clamp(value, 0.5f, 2.0f);
            ApplyScale();
        }

        private void ApplyScale()
        {
            if (!_hasBase || viewportFrame == null) return;
            viewportFrame.sizeDelta = _baseSize * scale;
        }

        void OnGUI()
        {
            if (!showSlider) return;
            var rect = new Rect(Screen.width * 0.5f - 100f, 8f, 200f, 40f);
            GUI.Box(rect, "监看板缩放");
            float next = GUI.HorizontalSlider(
                new Rect(rect.x + 10f, rect.y + 22f, rect.width - 20f, 16f), scale, 0.5f, 2.0f);
            if (!Mathf.Approximately(next, scale)) SetScale(next);
        }
    }
}
