using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘 ⇄ 片场 的模式过渡：先淡出到实色，
    /// 切换 <see cref="TabletopDirector"/> 模式并调演员预算，最后淡入。
    /// 遵循「同数据」原则，不重建对象、不重载场景。
    /// </summary>
    public sealed partial class StudioFadeTransition : MonoBehaviour
    {
        [Header("绑定")]
        public TabletopDirector tabletop;
        public TabletopActorBudget actorBudget;
        public Transform headAnchor;

        [Header("过渡")]
        [Tooltip("单程淡入或淡出时长，秒。总时长约为它的两倍。")]
        [Range(0.05f, 2f)] public float fadeDuration = 0.14f;
        public Color fadeColor = Color.black;
        [Range(0.05f, 0.5f)] public float overlayDistance = 0.12f;

        private CanvasGroup _group;
        private Canvas _fadeCanvas;
        private bool _running;

        public bool IsRunning => _running;

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            if (actorBudget == null) actorBudget = FindAnyObjectByType<TabletopActorBudget>();
            if (headAnchor == null)
            {
                var cam = Camera.main;
                headAnchor = cam != null ? cam.transform : null;
            }
            EnsureOverlay();
        }

        public void Bind(TabletopDirector tabletop, TabletopActorBudget actorBudget, Transform headAnchor)
        {
            this.tabletop = tabletop;
            this.actorBudget = actorBudget;
            this.headAnchor = headAnchor;
            EnsureOverlay();
            if (_group == null) return;
            var parent = headAnchor != null ? headAnchor : transform;
            if (_group.transform.parent != parent) _group.transform.SetParent(parent, false);
        }

        public void TransitionTo(TabletopDirector.ScaleMode target)
        {
            if (_running || tabletop == null) return;
            if (tabletop.currentMode == target) return;
            StartCoroutine(Run(target));
        }

        public void ToggleMode()
        {
            if (tabletop == null) return;
            var target = tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini
                ? TabletopDirector.ScaleMode.Immersive1to1
                : TabletopDirector.ScaleMode.TabletopMini;
            TransitionTo(target);
        }

        private IEnumerator Run(TabletopDirector.ScaleMode target)
        {
            _running = true;
            try
            {
                yield return Fade(0f, 1f);
                tabletop.SwitchMode(target);
                if (actorBudget != null)
                    actorBudget.Apply(target == TabletopDirector.ScaleMode.TabletopMini);
                yield return Fade(1f, 0f);
            }
            finally
            {
                _running = false;
                ReleaseOverlay();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _running = false;
            ReleaseOverlay();
        }

        /// <summary>强制解锁全屏遮罩：alpha 归零、关 Canvas、放开射线。</summary>
        public void ReleaseOverlay()
        {
            if (_group == null) return;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            if (_fadeCanvas != null) _fadeCanvas.enabled = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_group == null) yield break;

            float elapsed = 0f;
            ApplyFadeAlpha(from);
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            ApplyFadeAlpha(to);
        }

        /// <summary>alpha 归零时连 Canvas 一起关，避免空转的全屏 quad 被 XR 合成器反复抓取。</summary>
        private void ApplyFadeAlpha(float a)
        {
            if (_group == null) return;
            _group.alpha = a;
            if (_fadeCanvas != null) _fadeCanvas.enabled = a > 0.001f;
            // 透明就等于不挡射线：这是设备端「点了没反应」最常见的一类原因。
            if (a <= 0.001f) _group.blocksRaycasts = false;
        }

    }
}
