using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.RuntimeUI
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class ActorWalkTarget : MonoBehaviour
    {
        [SerializeField] private ActorNavigator actor;

        private XRSimpleInteractable floorInteractable;

        public void Bind(ActorNavigator navigator) => actor = navigator;

        private void Awake() => floorInteractable = GetComponent<XRSimpleInteractable>();

        private void OnEnable() => floorInteractable.selectEntered.AddListener(OnFloorSelected);

        private void OnDisable() => floorInteractable.selectEntered.RemoveListener(OnFloorSelected);

        private void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (!actor || mouse == null || keyboard == null ||
                !keyboard.leftShiftKey.isPressed || !mouse.leftButton.wasPressedThisFrame ||
                (EventSystem.current && EventSystem.current.IsPointerOverGameObject())) return;

            var camera = Camera.main;
            if (camera && Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()), out var hit,
                    20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                TryWalk(hit);
        }

        private void OnFloorSelected(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit))
                TryWalk(hit);
        }

        private void TryWalk(RaycastHit hit)
        {
            if (actor && hit.collider && hit.collider.gameObject == gameObject && hit.normal.y > .8f)
                actor.MoveTo(hit.point);
        }
    }
}
