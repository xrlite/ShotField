#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.EditorTools
{
    /// <summary>Ray interactor binding half of XriInputBindings (line-budget split).</summary>
    internal static partial class XriInputBindings
    {
        internal static void BindHandTrackingRay(GameObject hand, XRRayInteractor ray, bool left)
        {
            string map = left ? "XRI Left" : "XRI Right";
            string intMap = left ? "XRI Left Interaction" : "XRI Right Interaction";
            string usage = left ? "LeftHand" : "RightHand";
            var handedness = left ? InteractorHandedness.Left : InteractorHandedness.Right;
            var pose = hand.GetComponent<TrackedPoseDriver>() ?? hand.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.positionInput = PropRef(map, "Aim Position", $"<HandInteraction>{{{usage}}}/pointer/position", "Vector3");
            pose.rotationInput = PropRef(map, "Aim Rotation", $"<HandInteraction>{{{usage}}}/pointer/rotation", "Quaternion");
            var ts = FindRef(map, "Tracking State");
            if (ts != null) pose.trackingStateInput = new InputActionProperty(ts);
            BindRayInteractor(ray, intMap, usage, handedness);
        }

        private static void BindRayInteractor(XRRayInteractor ray, string intMap, string usage, InteractorHandedness handedness)
        {
            if (ray == null) return;
            ray.handedness = handedness;
            ray.maxRaycastDistance = 8f;
            ray.enableUIInteraction = true;
            ray.blockUIOnInteractableSelection = true;
            ray.selectInput = ButtonReader(intMap, "Select", "Select Value", $"<XRController>{{{usage}}}/gripPressed");
            ray.activateInput = ButtonReader(intMap, "Activate", "Activate Value", $"<XRController>{{{usage}}}/triggerPressed");
            ray.uiPressInput = ButtonReader(intMap, "UI Press", "UI Press Value", $"<XRController>{{{usage}}}/triggerPressed");
            ray.uiScrollInput = ValueReader(intMap, "UI Scroll", $"<XRController>{{{usage}}}/primary2DAxis");
        }
    }
}
#endif
