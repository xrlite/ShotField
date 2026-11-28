using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 胶囊栏的收起/放回（B 刀）。单独成文件：主文件守 200 行预算。
    /// 收起期间不接射线、不可交互；放回走 0.25 秒淡入。
    /// </summary>
    public sealed partial class ScaleToggleCapsule
    {
        private CanvasGroup _group;
        private bool _suppressed;
        private Coroutine _fadeRoutine;

        /// <summary>收起/放回胶囊栏。由 LateUpdate 根据 WelcomeHubOverlay.IsOpen 驱动。</summary>
        public void SetSuppressed(bool on)
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }
            _suppressed = on;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeGroup(on ? 0f : 1f));
        }

        private System.Collections.IEnumerator FadeGroup(float target)
        {
            float from = _group.alpha;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, target, t / 0.25f);
                yield return null;
            }
            _group.alpha = target;
            // 淡入结束后才开射线：UiRaycastGuard 会把「alpha≈0 但仍挡射线」的组强制解锁且永不恢复。
            bool on = target > 0.5f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }
    }
}
