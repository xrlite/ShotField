using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>卡片右缘缩放手柄：拉远/拉近时改变卡片与头部的距离，松手吸附。</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class CardScaleHandle : MonoBehaviour
    {
        private SpatialBlockCard _card;
        private XRGrabInteractable _grab;
        private bool _scaling;

        public void Bind(SpatialBlockCard card)
        {
            _card = card;
            _grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            _grab.selectEntered.AddListener(OnSelectEnter);
            _grab.selectExited.AddListener(OnSelectExit);
        }

        private void OnDisable()
        {
            if (_grab == null) return;
            _grab.selectEntered.RemoveListener(OnSelectEnter);
            _grab.selectExited.RemoveListener(OnSelectExit);
        }

        private void Update()
        {
            if (!_scaling || _card == null || _card.HeadAnchor == null) return;
            _card.SetDistance(Vector3.Distance(_card.HeadAnchor.position, transform.position));
        }

        private void OnSelectEnter(SelectEnterEventArgs args) => _scaling = true;
        private void OnSelectExit(SelectExitEventArgs args)
        {
            _scaling = false;
            if (_card != null) _card.Snap();
        }
    }
}
