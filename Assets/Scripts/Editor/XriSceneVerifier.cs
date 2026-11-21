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
            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity");

            var hands = Object.FindObjectsByType<TrackedPoseDriver>();
            var directs = Object.FindObjectsByType<XRDirectInteractor>();
            var rays = Object.FindObjectsByType<XRRayInteractor>();
            Check("two tracked controller poses", ControllerPoseCount(hands) == 2);
            Check("two direct interactors", directs.Length == 2);
            Check("two ray interactors", rays.Length == 2);
            Check("direct grip bindings", AllDirectBindings(directs));
            Check("ray trigger UI bindings", AllRayBindings(rays));
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
                if (!n.StartsWith("XRController_")) continue;
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
                if (!XriInputBindings.HasBinding(items[i].selectInput.inputActionPerformed, "/gripPressed")) return false;
            return true;
        }

        private static bool AllRayBindings(XRRayInteractor[] items)
        {
            if (items.Length != 2) return false;
            for (int i = 0; i < items.Length; i++)
                if (!XriInputBindings.HasBinding(items[i].uiPressInput.inputActionPerformed, "/triggerPressed")) return false;
            return true;
        }

        private static bool MoveBound()
        {
            var p = Object.FindAnyObjectByType<ContinuousMoveProvider>();
            return p && XriInputBindings.HasBinding(p.leftHandMoveInput.inputAction, "{LeftHand}/primary2DAxis");
        }

        private static bool TurnBound()
        {
            var p = Object.FindAnyObjectByType<SnapTurnProvider>();
            return p && XriInputBindings.HasBinding(p.rightHandTurnInput.inputAction, "{RightHand}/primary2DAxis");
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
