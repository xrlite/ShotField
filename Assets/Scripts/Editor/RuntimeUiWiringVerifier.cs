#if UNITY_EDITOR
using System;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.EditorTools
{
    public static class RuntimeUiWiringVerifier
    {
        private const int Expected = 11;

        public static void Run()
        {
            int passed = 0;
            void Check(string label, bool condition)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log("[RuntimeUiWiringVerifier] PASS: " + label);
            }

            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity");
            var workbench = UnityEngine.Object.FindAnyObjectByType<SpatialWorkbench>();
            Check("saved SC01 contains SpatialWorkbench", workbench != null);
            var canvas = workbench.GetComponent<Canvas>();
            Check("workbench uses WorldSpace canvas", canvas && canvas.renderMode == RenderMode.WorldSpace);
            workbench.EnsureRuntimeButtons(canvas);
            workbench.EnsureAdvancedControls(canvas);
            Check("undo and redo controls exist", workbench.undoBtn && workbench.redoBtn);
            Check("AI, export and review controls exist", workbench.storyBtn && workbench.exportBtn &&
                workbench.playReviewBtn && workbench.compareBtn);
            Check("review progress control exists", workbench.reviewProgressSlider != null);
            Check("advanced controls do not overlap", !ControlsOverlap(workbench));

            workbench.storyBtn.onClick.Invoke();
            Check("story action is explicitly offline", workbench.AiState.StartsWith("Offline outline:"));
            workbench.compareBtn.onClick.Invoke();
            Check("comparison creates a visible generated-video surface",
                GameObject.Find("GeneratedVideoDisplay") != null && workbench.CompareModeName == "PictureInPicture");
            workbench.PlayReviewVideo(string.Empty);
            Check("empty video source leaves player stopped", !workbench.ReviewIsPlaying);
            Check("empty video source does not report playback", workbench.ReviewState != "成片播放中");
            byte[] png = workbench.CaptureReferencePng();
            Check("runtime reference capture returns PNG", png != null && png.Length > 8 &&
                png[0] == 137 && png[1] == 80 && png[2] == 78 && png[3] == 71);

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"RUNTIME-UI-WIRING-RESULT={result} ({passed}/{Expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }

        private static bool ControlsOverlap(SpatialWorkbench workbench)
        {
            var buttons = new[] { workbench.undoBtn, workbench.redoBtn, workbench.storyBtn,
                workbench.exportBtn, workbench.playReviewBtn, workbench.compareBtn };
            for (int i = 0; i < buttons.Length; i++)
            for (int j = i + 1; j < buttons.Length; j++)
                if (LocalRect(buttons[i]).Overlaps(LocalRect(buttons[j]))) return true;
            return false;
        }

        private static Rect LocalRect(Button button)
        {
            var rt = (RectTransform)button.transform;
            return new Rect(rt.anchoredPosition - Vector2.Scale(rt.sizeDelta, rt.pivot), rt.sizeDelta);
        }
    }
}
#endif
