using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Android 构建入口。只在 Editor 下编译，不会进 Player。
///
/// 变更记录（相对 ShotFieldBuild 时代的 BuildG0.cs）：
/// - 显式设置包名，不再沿用 URP 模板的 com.UnityTechnologies.com.unity.template.urpblank
/// - 显式设置 productName
/// - 主工程路径已改为纯英文，不再需要构建镜像
/// </summary>
public static class BuildAndroid
{
    // 主场景唯一入口。以前这里指向白模底稿场景，打出来的包里既没有六色演员也没有轨迹，
    // 属于「装上去第一分钟就穿帮」那类缺陷（全库问题清单 A1）。
    // 改名成 SC01_Main 时只改这一行 + EditorBuildSettings，别再往下面几处补字面量。
    public const string ScenePath = "Assets/Scenes/SC01_Main.unity";
    const string PackageId = "com.haoyunlai.shotfield";
    const string ProductName = "ShotField";
    // 默认输出到工程内 Build/。需要指定位置时，用环境变量 SHOTFIELD_OUT_APK 覆盖。
    const string OutApk = "Build/ShotField.apk";
    const string OutApkRev1 = "Build/ShotField_rev1.apk";
    const string OutApkD = "Build/ShotField_studioWiring.apk";

    /// <summary>只做构建：IL2CPP + ARM64。PICO SDK 6.2.0 强制要求这两项。</summary>
    public static void BuildIl2Cpp()
    {
        string targetPath = System.Environment.GetEnvironmentVariable("SHOTFIELD_OUT_APK");
        if (string.IsNullOrEmpty(targetPath)) targetPath = OutApkD;
        BuildToPath(targetPath);
    }

    public static void BuildD() => BuildToPath(OutApkD);
    public static void BuildIl2CppRev1() => BuildToPath(OutApkRev1);

    public static void BuildToPath(string path)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
        PlayerSettings.productName = ProductName;

        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        EditorBuildSettings.scenes = scenes;

        DoBuild(path, "IL2CPP-ARM64");
    }

    /// <summary>打印当前关键设置，不做任何修改，用来排查「构建出来了但包名不对」这类问题。</summary>
    public static void DumpSettings()
    {
        Debug.Log("=== 构建设置 ===");
        Debug.Log("productName          = " + PlayerSettings.productName);
        Debug.Log("applicationIdentifier= " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        Debug.Log("scriptingBackend     = " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
        Debug.Log("targetArchitectures  = " + PlayerSettings.Android.targetArchitectures);
        Debug.Log("minSdkVersion        = " + PlayerSettings.Android.minSdkVersion);
        Debug.Log("targetSdkVersion     = " + PlayerSettings.Android.targetSdkVersion);
        EditorApplication.Exit(0);
    }

    static void DoBuild(string outPath, string tag)
    {
        var outDir = System.IO.Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(outDir) && !System.IO.Directory.Exists(outDir))
        {
            System.IO.Directory.CreateDirectory(outDir);
        }

        string[] scenes = new string[] { ScenePath };

        var o = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var r = BuildPipeline.BuildPlayer(o);
        Debug.Log("BUILD_RESULT[" + tag + "]=" + r.summary.result
            + " ERRORS=" + r.summary.totalErrors
            + " TOTAL_SIZE=" + r.summary.totalSize);

        if (r.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("构建失败，错误数 " + r.summary.totalErrors);
            EditorApplication.Exit(1);
        }
    }
}

// Change only the temporary build scene, preserving the checked-in baseline for the A/B control.
internal sealed class PicoManagerBuildProbe : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.Android ||
            scene.path != BuildAndroid.ScenePath) return;
        bool omit = System.Environment.GetEnvironmentVariable("SHOTFIELD_OMIT_PXR_MANAGER") == "1";
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var manager in root.GetComponentsInChildren<ByteDance.PICO.XR.PXR_Manager>(true))
            {
                count++;
                if (omit) Object.DestroyImmediate(manager);
            }
        }
        Debug.Log($"[PICO_MANAGER_AB] original={count} packaged={(omit ? 0 : count)}");
    }
}

/// <summary>
/// batchmode 自动构建触发器。
/// 与 SmokeTest 用同一套思路：-executeMethod 在有脚本变更时会被 -quit 抢先，
/// 所以改成域重载后由环境变量触发。
///
/// 但 delayCall 不够——它在域重载后立刻执行，此时 AssetDatabase 可能还在导入，
/// 构建会在 "Compiling Scripts" 阶段被打断。所以改成 update 轮询，
/// 等到不再编译、不再导入时才真正开始。
///
/// 触发条件：SHOTFIELD_BUILD=1
/// </summary>
[InitializeOnLoad]
internal static class BuildAutoRunner
{
    static BuildAutoRunner()
    {
        if (!Application.isBatchMode) return;
        if (System.Environment.GetEnvironmentVariable("SHOTFIELD_BUILD") != "1") return;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Tick;
        BuildAndroid.BuildIl2Cpp();
    }
}
