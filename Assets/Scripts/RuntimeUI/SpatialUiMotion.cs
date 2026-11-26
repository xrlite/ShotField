using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间 UI 统一动效组件：面板展开/收起的 EaseOutBack 弹性缩放与 CanvasGroup 渐显，
    /// 以及按钮 UguiHoverEffect 自动挂载。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SpatialUiMotion : MonoBehaviour
    {
        public float openDuration = 0.22f;
        public float startScale = 0.88f;

        private CanvasGroup _group;
        private Vector3 _targetScale = Vector3.one;
        private float _elapsed;
        private bool _opening;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _targetScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
        }

        private void OnEnable()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _elapsed = 0f;
            _opening = true;
            _group.alpha = 0f;
            transform.localScale = _targetScale * startScale;
        }

        private void Update()
        {
            if (!_opening) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, openDuration));
            float ease = EaseOutBack(t);
            if (_group != null) _group.alpha = Mathf.Clamp01(t * 1.4f);
            transform.localScale = Vector3.LerpUnclamped(_targetScale * startScale, _targetScale, ease);
            if (t >= 1f)
            {
                transform.localScale = _targetScale;
                if (_group != null) _group.alpha = 1f;
                _opening = false;
            }
        }

        public static void AttachButtonMotion(GameObject root)
        {
            if (root == null) return;
            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                if (btn.GetComponent<UguiHoverEffect>() == null)
                    btn.gameObject.AddComponent<UguiHoverEffect>();
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm1 = x - 1f;
            return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
        }
    }
}
