using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘 ⇄ 片场 的模式过渡：先淡出到实色，切换 <see cref="TabletopDirector"/> 模式，
    /// 再淡入。过渡期间屏幕被完全遮住，所以模式的瞬切（相机位移、代理根显隐）不会被人眼看到。
    ///
    /// 「同数据」是什么意思：本类**不重载场景、不重建对象、不复制任何演员或道具**。
    /// 它只做两件事 —— 调 <see cref="TabletopDirector.SwitchMode"/> 和调
    /// <see cref="TabletopActorBudget.Apply"/>。因此沙盘里摆的演员、道具、机位在
    /// 走进片场之后是同一批实例，位姿由 TabletopDirector 的坐标换算保持一致。
    /// 这是本设计唯一的正确性来源，任何"过渡时重新实例化"的改法都会破坏它。
    ///
    /// 遮罩实现：挂在头显相机下的 WorldSpace Canvas + 纯色 Image + CanvasGroup。
    /// 不用 ScreenSpaceOverlay —— VR 里 overlay 不随头显渲染，会完全看不到遮罩。
    /// </summary>
    public sealed class StudioFadeTransition : MonoBehaviour
    {
        [Header("绑定")]
        public TabletopDirector tabletop;
        public TabletopActorBudget actorBudget;
        public Transform headAnchor;

        [Header("过渡")]
        [Tooltip("单程淡入或淡出时长，秒。总时长约为它的两倍。")]
        [Range(0.05f, 2f)] public float fadeDuration = 0.35f;

        public Color fadeColor = Color.black;

        [Tooltip("遮罩距眼睛的距离，米。太近会触发近裁剪，太远遮不住边缘。")]
        [Range(0.05f, 0.5f)] public float overlayDistance = 0.12f;

        private CanvasGroup _group;
        private bool _running;

        /// <summary>过渡是否正在进行。外部据此禁止重复触发。</summary>
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

        /// <summary>切换到目标模式并播放过渡。正在过渡时忽略重复调用。</summary>
        public void TransitionTo(TabletopDirector.ScaleMode target)
        {
            if (_running || tabletop == null) return;
            if (tabletop.currentMode == target) return;
            StartCoroutine(Run(target));
        }

        /// <summary>沙盘 ⇄ 片场 来回切一次。</summary>
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

            yield return Fade(0f, 1f);

            tabletop.SwitchMode(target);
            if (actorBudget != null)
                actorBudget.Apply(target == TabletopDirector.ScaleMode.TabletopMini);

            yield return Fade(1f, 0f);

            _running = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_group == null) yield break;

            float elapsed = 0f;
            _group.alpha = from;
            _group.blocksRaycasts = to > 0.5f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            _group.alpha = to;
            _group.blocksRaycasts = to > 0.5f;
        }

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
