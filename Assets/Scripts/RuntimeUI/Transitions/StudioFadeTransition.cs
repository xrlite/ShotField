using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘 ⇄ 片场 的模式过渡：先淡出到实色，执行 FOV 呼吸与尺度插值，
    /// 切换 <see cref="TabletopDirector"/> 模式并调演员预算，最后淡入。
    /// 遵循「同数据」原则，不重建对象、不重载场景。
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
        [Range(0.05f, 0.5f)] public float overlayDistance = 0.12f;

        [Header("平滑呼吸与尺度过渡")]
        public bool enableFovBreathing = true;
        [Range(0.5f, 8f)] public float fovBreathingDelta = 3f;
        public bool enableScaleInterpolation = true;

        private CanvasGroup _group;
        private Camera _headCamera;
        private float _baseFov = 60f;
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
            CacheCamera();
            EnsureOverlay();
        }

        public void Bind(TabletopDirector tabletop, TabletopActorBudget actorBudget, Transform headAnchor)
        {
            this.tabletop = tabletop;
            this.actorBudget = actorBudget;
            this.headAnchor = headAnchor;
            CacheCamera();
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
            CacheCamera();

            yield return Fade(0f, 1f, true);

            tabletop.SwitchMode(target);
            if (actorBudget != null)
                actorBudget.Apply(target == TabletopDirector.ScaleMode.TabletopMini);

            if (enableScaleInterpolation && tabletop.tabletopProxyRoot != null)
            {
                yield return InterpolateScale(target == TabletopDirector.ScaleMode.TabletopMini);
            }

            yield return Fade(1f, 0f, false);

            if (_headCamera != null) _headCamera.fieldOfView = _baseFov;
            _running = false;
        }

        private IEnumerator Fade(float from, float to, bool breathingIn)
        {
            if (_group == null) yield break;

            float elapsed = 0f;
            _group.alpha = from;
            _group.blocksRaycasts = to > 0.5f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                _group.alpha = Mathf.Lerp(from, to, t);

                if (enableFovBreathing && _headCamera != null)
                {
                    float factor = breathingIn ? t : (1f - t);
                    _headCamera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov - fovBreathingDelta, factor);
                }
                yield return null;
            }

            _group.alpha = to;
            _group.blocksRaycasts = to > 0.5f;
        }

        private IEnumerator InterpolateScale(bool enteringMini)
        {
            var proxy = tabletop.tabletopProxyRoot;
            if (proxy == null) yield break;

            Vector3 startScale = enteringMini ? Vector3.one * 0.001f : Vector3.one;
            Vector3 endScale = enteringMini ? Vector3.one : Vector3.one * 0.001f;
            float duration = 0.08f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                proxy.localScale = Vector3.Lerp(startScale, endScale, elapsed / duration);
                yield return null;
            }
            proxy.localScale = Vector3.one;
        }

        private void CacheCamera()
        {
            if (headAnchor != null) _headCamera = headAnchor.GetComponent<Camera>();
            if (_headCamera == null) _headCamera = Camera.main;
            if (_headCamera != null) _baseFov = _headCamera.fieldOfView;
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
