#if UNITY_EDITOR
using System;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.EditorTools
{
    public static class Sprint1UiVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Sprint 1 UI Verifier")]
        public static void Run()
        {
            int passed = 0;
            void Check(string label, bool cond)
            {
                if (!cond) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log("[Sprint1UiVerifier] PASS: " + label);
            }

            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity");
            var wb = UnityEngine.Object.FindFirstObjectByType<SpatialWorkbench>();
            var browser = UnityEngine.Object.FindFirstObjectByType<PropBrowserUI>();
            var inspector = UnityEngine.Object.FindFirstObjectByType<SceneObjectInspector>();
            var tlUi = UnityEngine.Object.FindFirstObjectByType<TimelineControlsUI>();

            Check("workbench and Sprint1 UI panels exist in SC01", wb != null && browser != null && inspector != null && tlUi != null);
            Check("PropBrowserUI bound all 6 real SC01 prefabs", browser.BindingCount >= 6);

            var spawned = browser.SpawnProp("envelope", new Vector3(0.5f, 0f, 0.5f));
            Check("PropBrowserUI spawns grab-ready prop instance", spawned != null && spawned.GetComponent<XRGrabInteractable>() != null);

            inspector.RefreshSceneObjects();
            string targetId = inspector.objects.Count > 0 ? inspector.objects[0].id : string.Empty;
            bool selectOk = inspector.SelectObject(targetId);
            bool lockOk = inspector.ToggleLock(targetId);
            bool visOk = inspector.ToggleVisibility(targetId);
            bool resetOk = inspector.ResetObject(targetId);
            Check("SceneObjectInspector select/lock/visibility/reset work", selectOk && lockOk && visOk && resetOk);

            tlUi.Wire();
            tlUi.CycleMotionPreset();
            tlUi.CyclePosePreset();
            tlUi.timeline.SeekNormalized(0.5f);
            Check("TimelineControlsUI switches presets and drives scrubber",
                tlUi.currentMotion == CameraMotionType.DollyPull &&
                tlUi.currentPose == ActorPoseType.Sit &&
                Mathf.Abs(tlUi.timeline.NormalizedTime - 0.5f) < 0.01f);

            var wbRt = wb.GetComponent<RectTransform>();
            Check("Spatial_Workbench_Canvas expanded cleanly without overflow", wbRt != null && wbRt.sizeDelta.y >= 860f);

            if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"SPRINT1-UI-RESULT={result} ({passed}/{Expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }
    }
}
#endif
