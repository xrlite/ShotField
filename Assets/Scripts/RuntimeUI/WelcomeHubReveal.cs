using System.Collections;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 欢迎浮层的入场节奏（A-3）：静默 2 秒让大厅光影先说话，再用 0.35 秒淡入；
    /// 静默期内不接收射线、不拦截点击，避免"看不见却能点到"的怪状态。
    /// 单独成文件：WelcomeHubOverlay 已接近 200 行预算，节奏逻辑不塞进去。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WelcomeHubReveal : MonoBehaviour
    {
        public const float DefaultDelay = 2f;
        public const float DefaultFade = 0.35f;

        /// <summary>
        /// 淡入结束（射线刚打开）后的回调：C-1 用它把面板锁在世界坐标。
        /// 只加通知，不改「淡入结束才开射线」的时序。
        /// </summary>
        public System.Action OnRevealed;

        private CanvasGroup _group;

        public void Play(float delay = DefaultDelay, float fade = DefaultFade)
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            Debug.Log($"[HUBDIAG] reveal-start delay={delay:F2} fade={fade:F2} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
            StartCoroutine(Reveal(Mathf.Max(0f, delay), Mathf.Max(0.01f, fade)));
        }

        private IEnumerator Reveal(float delay, float fade)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Clamp01(t / fade);
                yield return null;
            }
            _group.alpha = 1f;
            // 最后才开射线：UiRaycastGuard 会把「alpha≤0.01 但仍挡射线」的 CanvasGroup 强制解锁且永不恢复，
            // 若与 alpha 同时起步会留下约 3.5ms 的窗口，一旦命中就是「看得见、永远点不动」。
            _group.blocksRaycasts = true;
            _group.interactable = true;
            Debug.Log($"[HUBDIAG] reveal-done alpha={_group.alpha:F2} raycast={_group.blocksRaycasts}");
            OnRevealed?.Invoke();
        }
    }
}
