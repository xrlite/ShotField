#if UNITY_EDITOR
using ShotField.RuntimeUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace ShotField.EditorTools
{
    /// <summary>Hand anchor / modality half of XriRigPrefabBuilder (line-budget split).</summary>
    internal static partial class XriRigPrefabBuilder
    {
        internal static void ConfigureVisuals(LineRenderer line, XRInteractorLineVisual vis)
        {
            if (line != null)
            {
                line.widthMultiplier = 0.004f;
                if (line.sharedMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader != null) line.sharedMaterial = new Material(shader);
                }
            }
            if (vis != null)
            {
                vis.lineWidth = 0.004f;
                vis.lineLength = 6f;
                vis.autoAdjustLineLength = true;
                vis.minLineLength = 0.25f;
                var invalid = new Gradient();
                invalid.SetKeys(
                    new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0f, 1f) });
                vis.invalidColorGradient = invalid;
            }
        }

        internal static GameObject EnsureHandAnchor(Transform origin, XRInteractionManager manager, string name, bool left)
        {
            var existing = origin.Find(name);
            var handGo = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) handGo.transform.SetParent(origin, false);
            var rayTf = handGo.transform.Find(name + "_Ray");
            var rayGo = rayTf != null ? rayTf.gameObject : new GameObject(name + "_Ray");
            if (rayTf == null) rayGo.transform.SetParent(handGo.transform, false);
            var ray = rayGo.GetComponent<XRRayInteractor>() ?? rayGo.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            var vis = rayGo.GetComponent<XRInteractorLineVisual>() ?? rayGo.AddComponent<XRInteractorLineVisual>();
            var line = rayGo.GetComponent<LineRenderer>();
            if (line == null) line = rayGo.AddComponent<LineRenderer>();
            if (line == null)
            {
                if (vis != null) vis.enabled = false;
            }
            else
            {
                ConfigureVisuals(line, vis);
                // Hand rays are interaction-only in OS 6 gaze mode; the system owns the visible gaze dot.
                line.enabled = false;
                if (vis != null) vis.enabled = false;
                if (vis != null)
                {
                    line.widthMultiplier = 0.0015f;
                    vis.lineWidth = 0.0015f;
                    vis.lineLength = 2.5f;
                    var valid = new Gradient();
                    valid.SetKeys(
                        new[] { new GradientColorKey(new Color(0.95f, 0.78f, 0.45f), 0f), new GradientColorKey(new Color(0.95f, 0.78f, 0.45f), 1f) },
                        new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.05f, 1f) });
                    vis.validColorGradient = valid;
                }
            }
            XriInputBindings.BindHandTrackingRay(handGo, ray, left);
            handGo.SetActive(false);
            return handGo;
        }

        internal static GameObject EnsureGazeRay(GameObject origin, XRInteractionManager manager)
        {
            var tf = origin.transform.Find("XREyeGaze_Ray");
            var go = tf != null ? tf.gameObject : new GameObject("XREyeGaze_Ray");
            if (tf == null) go.transform.SetParent(origin.transform, false);
            var ray = go.GetComponent<XRRayInteractor>() ?? go.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.handedness = InteractorHandedness.None;
            ray.maxRaycastDistance = 10f;
            ray.enableUIInteraction = true;
            var pose = go.GetComponent<TrackedPoseDriver>() ?? go.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.positionInput = new InputActionProperty(GazeInput("Gaze Position", InputActionType.Value, "Vector3", "<EyeGaze>/pose/position"));
            pose.rotationInput = new InputActionProperty(GazeInput("Gaze Rotation", InputActionType.Value, "Quaternion", "<EyeGaze>/pose/rotation"));
            pose.trackingStateInput = new InputActionProperty(GazeInput("Gaze Tracked", InputActionType.Value, "Boolean", "<EyeGaze>/pose/isTracked"));
            var pinch = GazeInput("Gaze Pinch", InputActionType.Button, "Button",
                "<HandInteraction>{LeftHand}/pinchReady", "<HandInteraction>{RightHand}/pinchReady");
            var reader = new XRInputButtonReader("Gaze Pinch", inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);
            reader.inputActionPerformed = pinch;
            ray.selectInput = reader;
            ray.uiPressInput = reader;
            ray.activateInput = reader;
            go.SetActive(false);
            return go;
        }

        private static InputAction GazeInput(string name, InputActionType type, string expected, params string[] paths)
        {
            var action = new InputAction(name, type);
            foreach (var path in paths) action.AddBinding(path);
            action.Enable();
            return action;
        }

        internal static void WireModality(GameObject originGo, GameObject ctrlL, GameObject ctrlR,
            GameObject handL, GameObject handR, GameObject gazeRay)
        {
            var mod = originGo.GetComponent<XRInputModalityManager>() ?? originGo.AddComponent<XRInputModalityManager>();
            mod.leftController = ctrlL;
            mod.rightController = ctrlR;
            mod.leftHand = handL;
            mod.rightHand = handR;
            var router = originGo.GetComponent<HandTrackingModeRouter>() ?? originGo.AddComponent<HandTrackingModeRouter>();
            router.leftController = ctrlL;
            router.rightController = ctrlR;
            router.leftHand = handL;
            router.rightHand = handR;
            router.gazeRay = gazeRay;
        }

        private static void AddCompensator(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<XrEmulatorHeightCompensator>() == null)
                go.AddComponent<XrEmulatorHeightCompensator>();
        }
    }
}
#endif
