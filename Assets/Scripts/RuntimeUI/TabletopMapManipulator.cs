using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Pinch-to-scale and one-hand drag-pan for the expanded tabletop map.
    /// Two selecting interactors scale by their distance ratio; one selects and pans on the table plane.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopMapManipulator : MonoBehaviour
    {
        public float minScale = 0.5f;
        public float maxScale = 3f;
        public float panLimit = 1.2f;

        private Transform _map;
        private XRSimpleInteractable _grab;
        private readonly List<IXRSelectInteractor> _selected = new List<IXRSelectInteractor>();
        private float _lastPinch;
        private Vector3 _lastPan;
        private Vector3 _basePos;

        public void Bind(Transform map)
        {
            if (map == null || _map == map) return;
            _map = map;
            _basePos = map.localPosition;
            _grab = map.GetComponent<XRSimpleInteractable>();
            if (_grab == null) _grab = map.gameObject.AddComponent<XRSimpleInteractable>();
            _grab.selectEntered.AddListener(OnEnter);
            _grab.selectExited.AddListener(OnExit);
        }

        private void OnEnter(SelectEnterEventArgs args)
        {
            var interactor = args.interactorObject as IXRSelectInteractor;
            if (interactor == null) return;
            if (!_selected.Contains(interactor)) _selected.Add(interactor);
            _lastPinch = PinchDistance();
            _lastPan = PrimaryPose();
        }

        private void OnExit(SelectExitEventArgs args)
        {
            var interactor = args.interactorObject as IXRSelectInteractor;
            if (interactor == null) return;
            _selected.Remove(interactor);
            _lastPinch = PinchDistance();
            _lastPan = PrimaryPose();
        }

        private void Update()
        {
            if (_map == null || _selected.Count == 0) return;
            if (_selected.Count >= 2)
            {
                float d = PinchDistance();
                if (_lastPinch > 0.001f && d > 0.001f)
                {
                    float k = Mathf.Clamp(_map.localScale.x * (d / _lastPinch), minScale, maxScale);
                    _map.localScale = Vector3.one * k;
                }
                _lastPinch = d;
            }
            else
            {
                Vector3 p = PrimaryPose();
                Vector3 delta = p - _lastPan;
                delta.y = 0f;
                Vector3 next = _map.localPosition + delta;
                Vector3 off = next - _basePos;
                off.y = 0f;
                if (off.magnitude > panLimit) next = _basePos + off.normalized * panLimit + Vector3.up * (next.y - _basePos.y);
                _map.localPosition = next;
                _lastPan = p;
            }
        }

        private float PinchDistance()
        {
            if (_selected.Count < 2) return 0f;
            return Vector3.Distance(PoseOf(_selected[0]), PoseOf(_selected[1]));
        }

        private Vector3 PrimaryPose() => _selected.Count > 0 ? PoseOf(_selected[0]) : Vector3.zero;

        private static Vector3 PoseOf(IXRSelectInteractor interactor)
        {
            var tf = interactor as Component;
            return tf != null ? tf.transform.position : Vector3.zero;
        }
    }
}
