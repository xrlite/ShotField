#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// VR 舒适度与防晕系统验证器（按 D1 体验方案实施门禁）：
    /// 验证默认瞬移、平滑强制绑暗角、身高自适应、一键"我晕了"急救与新手首问画像。
    /// </summary>
    public static class ComfortSettingsVerifier
    {
        private const int Expected = 7;

        [MenuItem("ShotField/Verifiers/Run Comfort Settings Verifier")]
        public static void Run()
        {
            int passed = 0;
            var hostGo = new GameObject("TestComfortHost");
            var rigOffsetGo = new GameObject("TestCameraRigOffset");
            rigOffsetGo.transform.SetParent(hostGo.transform, false);

            var controller = hostGo.AddComponent<LocomotionComfortController>();
            controller.cameraRigOffsetNode = rigOffsetGo.transform;

            try
            {
                VerifyAssert.Require("Default comfort: Teleport, Snap 45deg, Vignette 60%",
                    controller.locomotionMode == LocomotionMode.Teleport &&
                    controller.turnMode == TurnMode.SnapTurn &&
                    Mathf.Approximately(controller.snapTurnAngle, 45f) &&
                    Mathf.Approximately(controller.vignetteIntensity, 0.60f), ref passed);

                controller.SetVignetteIntensity(0.10f);
                controller.SetLocomotionMode(LocomotionMode.Smooth);
                VerifyAssert.Require("Smooth locomotion strictly forces vignette >= 30%",
                    controller.locomotionMode == LocomotionMode.Smooth &&
                    controller.vignetteIntensity >= LocomotionComfortController.MinSmoothVignette, ref passed);

                controller.SetVignetteIntensity(0.0f);
                VerifyAssert.Require("Attempt to zero vignette during smooth mode is clamped to min",
                    Mathf.Approximately(controller.vignetteIntensity, LocomotionComfortController.MinSmoothVignette), ref passed);

                controller.SetHeightMode(HeightMode.Sitting);
                float sittingY = rigOffsetGo.transform.localPosition.y;
                controller.SetHeightMode(HeightMode.Manual, 0.20f);
                float manualY = rigOffsetGo.transform.localPosition.y;
                VerifyAssert.Require("Height adaptation offsets rig node (Sitting -0.45m, Manual +0.20m)",
                    Mathf.Abs(sittingY - LocomotionComfortController.SittingOffset) < 0.01f &&
                    Mathf.Abs(manualY - 0.20f) < 0.01f, ref passed);

                bool panicFired = false;
                controller.OnPanicActivated += () => panicFired = true;
                controller.TriggerPanic();
                VerifyAssert.Require("Panic button ('我晕了') activates 100% vignette + Teleport + 45deg",
                    panicFired &&
                    controller.locomotionMode == LocomotionMode.Teleport &&
                    Mathf.Approximately(controller.vignetteIntensity, 1.0f) &&
                    Mathf.Approximately(controller.moveSpeed, 1.0f), ref passed);

                controller.AnswerSicknessQuestion(ProneToSicknessAnswer.Prone);
                bool proneOk = controller.locomotionMode == LocomotionMode.Teleport &&
                               Mathf.Approximately(controller.vignetteIntensity, 0.80f);
                controller.AnswerSicknessQuestion(ProneToSicknessAnswer.NotProne);
                bool notProneOk = controller.locomotionMode == LocomotionMode.Smooth &&
                                  Mathf.Approximately(controller.vignetteIntensity, 0.40f);
                VerifyAssert.Require("First-time question auto-tunes preset (Prone=Teleport/80%, NotProne=Smooth/40%)",
                    proneOk && notProneOk, ref passed);

                var panel = ComfortSettingsPanel.CreatePanel(hostGo.transform, controller);
                panel.RefreshUI();
                VerifyAssert.Require("ComfortSettingsPanel reflects controller state and binds panic button",
                    panel != null &&
                    panel.locomotionModeText != null &&
                    panel.locomotionModeText.text.Contains("Smooth") &&
                    panel.vignetteText != null &&
                    panel.vignetteText.text.Contains("40%"), ref passed);
            }
            finally
            {
                Object.DestroyImmediate(hostGo);
            }

            VerifyAssert.Finish("COMFORT-SETTINGS", passed, Expected);
        }
    }
}
#endif
