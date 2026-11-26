using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Glass scene orb hovering at the director table edge. Selecting it hands control to
    /// SceneOrbDirector, which expands the matching tabletop map through the arch transition.
    /// The orb interior is a single merged micro-core mesh (one draw call), never a live portal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneOrb : MonoBehaviour
    {
        public string orbId = "living";
        public string label = "桌上的信";
        public SceneOrbDirector director;

        private XRSimpleInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            if (_interactable == null) _interactable = gameObject.AddComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(OnSelect);
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.selectEntered.RemoveListener(OnSelect);
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            Debug.Log("[SceneOrb] Select " + orbId);
            if (director != null) director.Activate(this);
        }
    }
}
