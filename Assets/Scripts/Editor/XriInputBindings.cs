#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ShotField.EditorTools
{
    /// <summary>Wires ShotFieldXRIActions InputActionReferences onto XR rig components.</summary>
    internal static partial class XriInputBindings
    {
        internal const string ActionAssetPath = "Assets/Settings/Input/ShotFieldXRIActions.inputactions";

        internal static InputActionAsset LoadActionAsset() =>
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);

        internal static InputActionReference FindRef(string mapName, string actionName)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(ActionAssetPath);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is InputActionReference r && r.action != null &&
                    r.action.actionMap != null && r.action.actionMap.name == mapName &&
                    r.action.name == actionName)
                    return r;
            }
            return null;
        }

        internal static void AttachInputActionManager(GameObject originGo)
        {
            var asset = LoadActionAsset();
            if (asset == null) return;
            var iam = originGo.GetComponent<InputActionManager>() ?? originGo.AddComponent<InputActionManager>();
            var list = iam.actionAssets ?? new List<InputActionAsset>();
            if (!list.Contains(asset)) list.Add(asset);
            iam.actionAssets = list;
        }

        internal static void BindHeadset(GameObject headset)
        {
            if (headset == null) return;
            var cam = headset.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            var pose = headset.GetComponent<TrackedPoseDriver>() ?? headset.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.positionInput = PropRef("XRI Head", "Position", "<XRHMD>/centerEyePosition", "Vector3");
            pose.rotationInput = PropRef("XRI Head", "Rotation", "<XRHMD>/centerEyeRotation", "Quaternion");
            var ts = FindRef("XRI Head", "Tracking State");
            if (ts != null) pose.trackingStateInput = new InputActionProperty(ts);
        }
        internal static void BindHand(GameObject hand, XRDirectInteractor direct,
            XRRayInteractor ray, bool left)
        {
            string map = left ? "XRI Left" : "XRI Right";
            string intMap = left ? "XRI Left Interaction" : "XRI Right Interaction";
            string usage = left ? "LeftHand" : "RightHand";
            var handedness = left ? InteractorHandedness.Left : InteractorHandedness.Right;
            var pose = hand.GetComponent<TrackedPoseDriver>() ?? hand.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.positionInput = PropRef(map, "Position", $"<XRController>{{{usage}}}/devicePosition", "Vector3");
            pose.rotationInput = PropRef(map, "Rotation", $"<XRController>{{{usage}}}/deviceRotation", "Quaternion");
            var ts = FindRef(map, "Tracking State");
            if (ts != null) pose.trackingStateInput = new InputActionProperty(ts);
            var haptics = hand.GetComponent<HapticImpulsePlayer>() ?? hand.AddComponent<HapticImpulsePlayer>();
            var hapticRef = FindRef(map, "Haptic Device");
            if (hapticRef != null)
            {
                haptics.hapticOutput.inputSourceMode = XRInputHapticImpulseProvider.InputSourceMode.InputActionReference;
                haptics.hapticOutput.inputActionReference = hapticRef;
            }
            if (direct != null)
            {
                direct.handedness = handedness;
                direct.selectInput = ButtonReader(intMap, "Select", "Select Value", $"<XRController>{{{usage}}}/gripPressed");
                direct.activateInput = ButtonReader(intMap, "Activate", "Activate Value", $"<XRController>{{{usage}}}/triggerPressed");
            }
            if (ray != null)
            {
                var rayPose = ray.GetComponent<TrackedPoseDriver>() ?? ray.gameObject.AddComponent<TrackedPoseDriver>();
                // Position comes from the hand parent (compensated on emulator); aim supplies rotation only,
                // otherwise the emulator's floor-absolute aim pose double-counts the camera offset.
                rayPose.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
                rayPose.positionInput = PropRef(map, "Aim Position", $"<XRController>{{{usage}}}/pointerPosition", "Vector3");
                rayPose.rotationInput = PropRef(map, "Aim Rotation", $"<XRController>{{{usage}}}/pointerRotation", "Quaternion");
                var rayTs = FindRef(map, "Tracking State");
                if (rayTs != null) rayPose.trackingStateInput = new InputActionProperty(rayTs);
            }
            BindRayInteractor(ray, intMap, usage, handedness);
        }
        internal static void BindLocomotion(SnapTurnProvider snap, ContinuousMoveProvider move)
        {
            if (snap != null)
                snap.rightHandTurnInput = ValueReader("XRI Right Locomotion", "Turn", "<XRController>{RightHand}/primary2DAxis");
            if (move != null)
                move.leftHandMoveInput = ValueReader("XRI Left Locomotion", "Move", "<XRController>{LeftHand}/primary2DAxis");
        }

        internal static bool HasBinding(XRInputButtonReader reader, string fragment)
        {
            if (reader == null) return false;
            var act = reader.inputSourceMode == XRInputButtonReader.InputSourceMode.InputActionReference
                ? reader.inputActionReferencePerformed?.action
                : reader.inputActionPerformed;
            return HasBinding(act, fragment);
        }

        internal static bool HasBinding(XRInputValueReader<Vector2> reader, string fragment)
        {
            if (reader == null) return false;
            var act = reader.inputSourceMode == XRInputValueReader.InputSourceMode.InputActionReference
                ? reader.inputActionReference?.action
                : reader.inputAction;
            return HasBinding(act, fragment);
        }

        internal static bool HasBinding(InputAction action, string fragment)
        {
            if (action == null) return false;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path.Contains(fragment)) return true;
            return false;
        }

        private static InputActionProperty PropRef(string map, string act, string fallbackBinding, string ctrlType)
        {
            var r = FindRef(map, act);
            if (r != null) return new InputActionProperty(r);
            return new InputActionProperty(new InputAction(act, InputActionType.Value, fallbackBinding, expectedControlType: ctrlType));
        }

        private static XRInputButtonReader ButtonReader(string map, string perfAct, string valAct, string fallbackBinding)
        {
            var perfRef = FindRef(map, perfAct);
            var valRef = FindRef(map, valAct);
            if (perfRef != null)
            {
                var r = new XRInputButtonReader(perfAct, inputSourceMode: XRInputButtonReader.InputSourceMode.InputActionReference);
                r.inputActionReferencePerformed = perfRef;
                r.inputActionReferenceValue = valRef;
                return r;
            }
            var fallback = new XRInputButtonReader(perfAct, inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);
            fallback.inputActionPerformed = new InputAction(perfAct, InputActionType.Button, fallbackBinding);
            return fallback;
        }

        private static XRInputValueReader<Vector2> ValueReader(string map, string act, string fallbackBinding)
        {
            var rRef = FindRef(map, act);
            if (rRef != null)
            {
                var r = new XRInputValueReader<Vector2>(act, XRInputValueReader.InputSourceMode.InputActionReference);
                r.inputActionReference = rRef;
                return r;
            }
            var fallback = new XRInputValueReader<Vector2>(act, XRInputValueReader.InputSourceMode.InputAction);
            fallback.inputAction = new InputAction(act, InputActionType.Value, fallbackBinding, expectedControlType: "Vector2");
            return fallback;
        }
    }
}
#endif
