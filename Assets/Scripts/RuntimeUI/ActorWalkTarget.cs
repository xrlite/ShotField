using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 地面走位目标：把当前选中的演员指到点击/射线落点。
    /// 选中状态统一来自 ActorSelection；inspector 绑定仅作回退。
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class ActorWalkTarget : MonoBehaviour
    {
        [SerializeField] private ActorNavigator actor;

        private XRSimpleInteractable floorInteractable;

        /// <summary>登记到 ActorSelection 并选为当前演员；保留旧调用签名。</summary>
        public void Bind(ActorNavigator navigator)
        {
            actor = navigator;
            if (navigator == null) return;
            ActorSelection.Register(navigator);
            ActorSelection.Select(navigator);
        }

        private void Awake()
        {
            floorInteractable = GetComponent<XRSimpleInteractable>();
            if (actor != null) ActorSelection.Register(actor);
        }

        private void OnEnable()
        {
            if (floorInteractable != null) floorInteractable.selectEntered.AddListener(OnFloorSelected);
        }

        private void OnDisable()
        {
            if (floorInteractable != null) floorInteractable.selectEntered.RemoveListener(OnFloorSelected);
        }

        private void OnDestroy()
        {
            if (actor != null) ActorSelection.Unregister(actor);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || !mouse.leftButton.wasPressedThisFrame) return;
            // 点到 UI 时不误触地面走位
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

            var camera = Camera.main;
            if (camera == null) return;
            var run = keyboard.leftShiftKey.isPressed; // Shift 仅作奔跑修饰，不再是指走前置条件
            if (Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()), out var hit,
                    20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                TryWalk(hit, run);
        }

        private void OnFloorSelected(SelectEnterEventArgs args)
        {
            if (floorInteractable == null) return;
            if (args.interactorObject is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit))
                TryWalk(hit, false);
        }

        private void TryWalk(RaycastHit hit, bool run)
        {
            var target = ActorSelection.Current != null ? ActorSelection.Current : actor;
            if (target == null || !hit.collider) return;
            if (hit.collider.gameObject != gameObject || hit.normal.y <= 0.8f) return;
            target.MoveTo(hit.point, run);
        }
    }
}
