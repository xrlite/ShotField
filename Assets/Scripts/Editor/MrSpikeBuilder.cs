#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// MR spike 场景的 Android 构建入口。只打 MR_Spike.unity，不动 SC01_Main 与主工程 XR 设置。
    /// 输出路径取环境变量 SHOTFIELD_OUT_APK，缺省落工程内 Build/ShotField_mr_spike.apk。
    /// </summary>
    public static class MrSpikeBuilder
    {
        public const string SpikeScenePath = "Assets/Scenes/MR_Spike.unity";
        private const string PackageId = "com.haoyunlai.shotfield";

        [MenuItem("ShotField/Verifiers/Build MR Spike APK")]
        public static void BuildMrSpike()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.productName = "ShotField MR Spike";

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SpikeScenePath, true) };

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SpikeScenePath },
                locationPathName = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SHOTFIELD_OUT_APK"))
                    ? "Build/ShotField_mr_spike.apk"
                    : System.Environment.GetEnvironmentVariable("SHOTFIELD_OUT_APK"),
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[MR-SPIKE-BUILD] result={report.summary.result} " +
                      $"errors={report.summary.totalErrors} size={report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
#endif
