#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ShotField.EditorTools
{
    /// <summary>Creates serialized OpenXR actions for the generated SC01 rig.</summary>
    internal static class XriInputBindings
    {
        internal static void BindHand(GameObject hand, XRDirectInteractor direct,
            XRRayInteractor ray, bool left)
        {
            string usage = left ? "LeftHand" : "RightHand";
            var pose = hand.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.positionInput = new InputActionProperty(Value(
                hand.name + " Position", "Vector3", $"<XRController>{{{usage}}}/devicePosition"));
            pose.rotationInput = new InputActionProperty(Value(
                hand.name + " Rotation", "Quaternion", $"<XRController>{{{usage}}}/deviceRotation"));

            direct.selectInput = ButtonReader(hand.name + " Grab", $"<XRController>{{{usage}}}/gripPressed");
            direct.activateInput = ButtonReader(hand.name + " Use", $"<XRController>{{{usage}}}/triggerPressed");
            ray.selectInput = ButtonReader(hand.name + " Ray Grab", $"<XRController>{{{usage}}}/gripPressed");
            ray.activateInput = ButtonReader(hand.name + " Ray Use", $"<XRController>{{{usage}}}/triggerPressed");
            ray.uiPressInput = ButtonReader(hand.name + " UI Press", $"<XRController>{{{usage}}}/triggerPressed");
        }

        internal static void BindLocomotion(SnapTurnProvider snap, ContinuousMoveProvider move)
        {
            snap.rightHandTurnInput = ValueReader("Right Snap Turn", "<XRController>{RightHand}/primary2DAxis");
            move.leftHandMoveInput = ValueReader("Left Move", "<XRController>{LeftHand}/primary2DAxis");
        }

        internal static bool HasBinding(InputAction action, string fragment)
        {
            if (action == null) return false;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path.Contains(fragment)) return true;
            return false;
        }

        private static InputAction Value(string name, string controlType, string binding) =>
            new InputAction(name, InputActionType.Value, binding, expectedControlType: controlType);

        private static XRInputButtonReader ButtonReader(string name, string binding)
        {
            var reader = new XRInputButtonReader(name, inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);
            reader.inputActionPerformed = new InputAction(name, InputActionType.Button, binding);
            return reader;
        }

        private static XRInputValueReader<Vector2> ValueReader(string name, string binding)
        {
            var reader = new XRInputValueReader<Vector2>(name, XRInputValueReader.InputSourceMode.InputAction);
            reader.inputAction = Value(name, "Vector2", binding);
            return reader;
        }
    }
}
#endif
