using UnityEngine;
using UnityEngine.EventSystems;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// uGUI 按钮的统一 hover/press 反馈，与 XRI 路径共用 <see cref="HoverMotion"/> 的曲线。
    /// 补上这一条是因为首屏卡的「知道了」是 uGUI Button，
    /// 之前只覆盖 XRSimpleInteractable，导致第一印象上的按钮没有任何手感。
    /// 只改 localScale，不改 layout / sizeDelta。
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UguiHoverEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private Vector3 baseScale = Vector3.one;
        private float weight;
        private bool hovering;
        private bool pressed;

        private void Awake()
        {
            baseScale = transform.localScale;
            // 执行顺序不保证：若 XRI 的反馈已接管此对象，就不要两套同时写 localScale。
            if (GetComponent<SpatialHoverEffect>() != null) enabled = false;
        }

        private void OnDisable()
        {
            hovering = false;
            pressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovering = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            Debug.Log($"[UI-POINTER] Down {name} pointerId={eventData.pointerId} module={eventData.pointerCurrentRaycast.module}");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            Debug.Log($"[UI-POINTER] Up {name} pointerId={eventData.pointerId}");
        }

        public void OnPointerClick(PointerEventData eventData) =>
            Debug.Log($"[UI-POINTER] Click {name} pointerId={eventData.pointerId}");

        private void Update()
        {
            weight = HoverMotion.Step(weight, pressed, hovering, Time.deltaTime);
            transform.localScale = baseScale * (1f + HoverMotion.Offset(weight));
        }
    }
}
