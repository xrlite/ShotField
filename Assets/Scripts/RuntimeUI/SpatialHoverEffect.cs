using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 全工程唯一的 Hover / Press 反馈实现，对应 UI 规格 §4.1。
    /// 官方把"每个控件各写一套 hover 缩放"列为反模式，所以这里集中一处：
    /// Hover scale 1.00→1.04 / 120ms，离开 90ms，按下 →0.97 / 80ms，抬起 140ms。
    /// 只改 localScale，不改 layout、不改 sizeDelta，避免撑开布局。
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class SpatialHoverEffect : MonoBehaviour
    {
        // 保留别名，避免外部已有引用；数值只在 HoverMotion 里定义一份。
        public const float HoverInDuration = HoverMotion.HoverInDuration;
        public const float HoverOutDuration = HoverMotion.HoverOutDuration;
        public const float PressDuration = HoverMotion.PressDuration;
        public const float ReleaseDuration = HoverMotion.ReleaseDuration;

        [Range(1f, 1.2f)] public float hoverScale = HoverMotion.HoverScale;
        [Range(0.8f, 1f)] public float pressScale = HoverMotion.PressScale;

        private XRSimpleInteractable interactable;
        private Vector3 baseScale = Vector3.one;
        private float weight;
        private bool hovering;
        private bool pressed;

        private void Awake()
        {
            baseScale = transform.localScale;
            interactable = GetComponent<XRSimpleInteractable>();
        }

        private void OnEnable()
        {
            if (interactable == null) return;
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
            interactable.selectEntered.AddListener(OnSelectEnter);
            interactable.selectExited.AddListener(OnSelectExit);
        }

        private void OnDisable()
        {
            if (interactable == null) return;
            // 只摘自己挂的委托。RemoveAllListeners 会连带清掉其他系统在同一事件上的订阅。
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
            interactable.selectEntered.RemoveListener(OnSelectEnter);
            interactable.selectExited.RemoveListener(OnSelectExit);
            hovering = false;
            pressed = false;
        }

        private void OnHoverEnter(HoverEnterEventArgs args) => hovering = true;
        private void OnHoverExit(HoverExitEventArgs args) => hovering = false;
        private void OnSelectEnter(SelectEnterEventArgs args) => pressed = true;
        private void OnSelectExit(SelectExitEventArgs args) => pressed = false;

        private void Update()
        {
            weight = HoverMotion.Step(weight, pressed, hovering, Time.deltaTime);
            transform.localScale = baseScale * (1f + HoverMotion.Offset(weight, hoverScale, pressScale));
        }
    }
}
