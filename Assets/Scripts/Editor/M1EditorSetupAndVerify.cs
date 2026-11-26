#if UNITY_EDITOR
using System.Collections.Generic;
using ByteDance.PICO.XR.Editor;
using UnityEditor;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ShotField.EditorTools
{
    /// <summary>M1 Editor setup & verification runner for F4/F5/F6.</summary>
    public static class M1EditorSetupAndVerify
    {
        public static void SetupAndVerifyAll()
        {
            try
            {
                ConfigureOpenXrHands();
                XriRigPrefabBuilder.BuildPrefabAndUpgradeMainScene();
                bool openXrOk = VerifyOpenXrAndroid();

                XriSceneVerifier.Run();
                InteractionVerifier.Run();
                PlatformServicesVerifier.Run();
                Debug.Log($"[M1EditorSetupAndVerify] OPENXR-ANDROID-VALID={(openXrOk ? "PASS" : "FAIL")}");
                SC01RealShotVerifier.Run();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void ConfigureOpenXrHands()
        {
            PXR_Utils.EnableHandTrackingFeature();
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) return;
            foreach (var f in settings.GetFeatures<OpenXRFeature>())
            {
                if (f is HandCommonPosesInteraction || f is MicrosoftHandInteraction)
                {
                    if (f.enabled)
                    {
                        f.enabled = false;
                        EditorUtility.SetDirty(settings);
                    }
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static bool VerifyOpenXrAndroid()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            int errors = 0;
            foreach (var r in issues)
            {
                if (r.error)
                {
                    errors++;
                    Debug.LogError($"[OpenXRValidation] ERROR: {r.message}");
                }
                else
                {
                    Debug.LogWarning($"[OpenXRValidation] WARN: {r.message}");
                }
            }
            Debug.Log($"[OpenXRValidation] Android issues={issues.Count}, errors={errors}");
            return errors == 0;
        }
    }
}
#endif
