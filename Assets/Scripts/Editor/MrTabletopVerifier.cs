#if UNITY_EDITOR
using ByteDance.PICO.OpenXR;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace ShotField.EditorTools
{
    /// <summary>MR 首屏配置门：VST feature、场景组件、回退参数三项。</summary>
    public static class MrTabletopVerifier
    {
        public const string Tag = "MR-TABLETOP-CONFIG";

        [MenuItem("ShotField/Verifiers/Run MR Tabletop Verifier")]
        public static void Run()
        {
            int passed = 0;
            // headless 启动不会自动载入主场景，先显式打开，避免"找不到组件"的假红。
            if (Application.isBatchMode)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    "Assets/Scenes/SC01_Main.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var settings = OpenXRSettings.ActiveBuildTargetInstance;
            var passthrough = settings.GetFeature<PassthroughFeature>();
            Check("Android PassthroughFeature 已启用", passthrough != null && passthrough.enabled, ref passed);

            var bootstrap = Object.FindAnyObjectByType<MrTabletopBootstrap>(FindObjectsInactive.Include);
            Check("主场景已挂 MrTabletopBootstrap", bootstrap != null, ref passed);
            Check("虚拟创作台回退参数有效", bootstrap != null && bootstrap.virtualDistance > 0.5f &&
                bootstrap.virtualDistance < 1.5f && bootstrap.virtualHeight > 0.5f &&
                bootstrap.virtualHeight < 1.2f, ref passed);

            string result = passed == 3 ? "PASS" : "FAIL";
            Debug.Log($"{Tag}={result} ({passed}/3)");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[MR-CONFIG] PASS: {label}"); }
            else Debug.LogError($"[MR-CONFIG] FAIL: {label}");
        }
    }
}
#endif
