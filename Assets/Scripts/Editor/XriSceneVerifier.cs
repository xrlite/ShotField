#if UNITY_EDITOR
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.EditorTools
{
    /// <summary>Verifies serialized input wiring in the saved SC01 scene.</summary>
    internal static class XriSceneVerifier
    {
        private static int _pass;
        private static int _total;
        private static readonly StringBuilder Report = new StringBuilder();

        public static void Run()
        {
            _pass = 0;
            _total = 0;
            Report.Length = 0;
            EditorSceneManager.OpenScene(BuildAndroid.ScenePath);

            var hands = Object.FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include);
            var directs = Object.FindObjectsByType<XRDirectInteractor>();
            var rays = Object.FindObjectsByType<XRRayInteractor>();
            Check("two tracked controller poses", ControllerPoseCount(hands) == 2);
            Check("controller ray aim poses", ControllerRayAimPoses());
            Check("two direct interactors", directs.Length == 2);
            Check("two ray interactors", rays.Length == 2);
            Check("two hand-tracking anchors", HandAnchorCount() == 2);
            Check("hand UI pinch bindings", AllHandRayBindings());
            Check("hand ray pointer poses", AllHandPoses());
            Check("direct grip bindings", AllDirectBindings(directs));
            Check("controller ray trigger UI bindings", AllRayBindings(rays));
            Check("two visible ray lines", Object.FindObjectsByType<XRInteractorLineVisual>().Length == 2);
            Check("XR UI input module", Object.FindAnyObjectByType<XRUIInputModule>() != null);
            Check("tracked-device canvas raycaster", Object.FindAnyObjectByType<TrackedDeviceGraphicRaycaster>() != null);
            Check("left thumbstick move", MoveBound());
            Check("right thumbstick snap turn", TurnBound());

            string verdict = _pass == _total ? "PASS" : "FAIL";
            string result = $"XRI-WIRING-RESULT={verdict} ({_pass}/{_total})";
            Report.AppendLine(result);
            Debug.Log("[XriSceneVerifier] " + result);
        }

        private static int ControllerPoseCount(TrackedPoseDriver[] poses)
        {
            int count = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                string n = poses[i].gameObject.name;
                if (!n.StartsWith("XRController_") || n.EndsWith("_Ray")) continue;
                bool pos = XriInputBindings.HasBinding(poses[i].positionInput.action, "/devicePosition");
                bool rot = XriInputBindings.HasBinding(poses[i].rotationInput.action, "/deviceRotation");
                if (pos && rot) count++;
            }
            return count;
        }

        private static bool AllDirectBindings(XRDirectInteractor[] items)
        {
            if (items.Length != 2) return false;
            for (int i = 0; i < items.Length; i++)
                if (!XriInputBindings.HasBinding(items[i].selectInput, "/gripPressed")) return false;
            return true;
        }

        private static bool AllRayBindings(XRRayInteractor[] items)
        {
            if (items.Length != 2) return false;
            for (int i = 0; i < items.Length; i++)
                if (!XriInputBindings.HasBinding(items[i].uiPressInput, "/triggerPressed")) return false;
            return true;
        }

        private static bool ControllerRayAimPoses()
        {
            var poses = Object.FindObjectsByType<TrackedPoseDriver>();
            int count = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                if (!poses[i].gameObject.name.StartsWith("XRController_") ||
                    !poses[i].gameObject.name.EndsWith("_Ray")) continue;
                count++;
                if (!XriInputBindings.HasBinding(poses[i].positionInput.action, "/pointerPosition")) return false;
                if (!XriInputBindings.HasBinding(poses[i].rotationInput.action, "/pointerRotation")) return false;
            }
            return count == 2;
        }

        private static int HandAnchorCount()
        {
            int count = 0;
            var rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include);
            for (int i = 0; i < rays.Length; i++)
                if (rays[i].gameObject.name.StartsWith("XRHand_")) count++;
            return count;
        }

        private static bool AllHandRayBindings()
        {
            var rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include);
            int count = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                if (!rays[i].gameObject.name.StartsWith("XRHand_")) continue;
                count++;
                if (!XriInputBindings.HasBinding(rays[i].selectInput, "/pinchReady")) return false;
                if (!XriInputBindings.HasBinding(rays[i].uiPressInput, "/pinchReady")) return false;
                if (!XriInputBindings.HasBinding(rays[i].uiPressInput, "/pointerActivated")) return false;
            }
            return count == 2;
        }

        private static bool AllHandPoses()
        {
            var poses = Object.FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include);
            int count = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                if (!poses[i].gameObject.name.StartsWith("XRHand_")) continue;
                count++;
                if (!XriInputBindings.HasBinding(poses[i].positionInput.action, "/pointer/position")) return false;
                if (!XriInputBindings.HasBinding(poses[i].rotationInput.action, "/pointer/rotation")) return false;
            }
            return count == 2;
        }

        private static bool MoveBound()
        {
            var p = Object.FindAnyObjectByType<ContinuousMoveProvider>();
            return p && XriInputBindings.HasBinding(p.leftHandMoveInput, "{LeftHand}/primary2DAxis");
        }

        private static bool TurnBound()
        {
            var p = Object.FindAnyObjectByType<SnapTurnProvider>();
            return p && XriInputBindings.HasBinding(p.rightHandTurnInput, "{RightHand}/primary2DAxis");
        }

        private static void Check(string label, bool ok)
        {
            _total++;
            if (ok) _pass++;
            string line = (ok ? "PASS: " : "FAIL: ") + label;
            Report.AppendLine(line);
            Debug.Log("[XriSceneVerifier] " + line);
        }
    }
}
#endif
