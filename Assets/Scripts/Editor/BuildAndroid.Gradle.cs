using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Vibe Lab 提交用的 Android Gradle 工程导出。
///
/// 背景：平台不接收 Unity 原始工程，也不接收成品 APK。它要求提交
/// "Android Gradle 工程" 仓库 + 40 位 Commit ID，云端再用 Gradle 统一打包签名。
/// 因此这里必须走 exportAsGoogleAndroidProject = true 的构建路径。
///
/// 输出目录默认 Build/GradleProject；可用环境变量 SHOTFIELD_OUT_GRADLE 覆盖。
/// 导出后该目录本身就是可以直接 git init 的仓库根（含 build.gradle / settings.gradle / unityLibrary）。
/// </summary>
public static partial class BuildAndroid
{
    const string OutGradle = "Build/GradleProject";

    [MenuItem("ShotField/导出 Android Gradle 工程（Vibe Lab）")]
    public static void ExportGradleProjectMenu() => ExportGradleProject();

    /// <summary>命令行入口：-executeMethod BuildAndroid.ExportGradleProject</summary>
    public static void ExportGradleProject()
    {
        string outDir = Environment.GetEnvironmentVariable("SHOTFIELD_OUT_GRADLE");
        if (string.IsNullOrEmpty(outDir)) outDir = OutGradle;

        // 包名 / 后端 / 架构与 APK 构建保持完全一致，避免"平台打出来的包和本地不一样"。
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
        PlayerSettings.productName = ProductName;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        bool previous = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        try
        {
            if (Directory.Exists(outDir) &&
                Directory.GetFiles(outDir).Length + Directory.GetDirectories(outDir).Length > 0)
            {
                Debug.LogError("Gradle 输出目录非空，已停止以避免覆盖旧仓库: " + Path.GetFullPath(outDir));
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("GRADLE_EXPORT_RESULT=" + s.result
                + " ERRORS=" + s.totalErrors
                + " SIZE=" + s.totalSize
                + " PATH=" + Path.GetFullPath(outDir));

            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("Gradle 工程导出失败，错误数 " + s.totalErrors);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            VerifyExport(outDir);
            SanitizeGradleProject(outDir);
        }
        finally
        {
            EditorUserBuildSettings.exportAsGoogleAndroidProject = previous;
        }
    }

    /// <summary>导出后自检：缺任何一个关键文件都说明平台会打不出包。</summary>
    static void VerifyExport(string outDir)
    {
        string[] required =
        {
            "settings.gradle", "build.gradle", "gradle.properties",
            "launcher/build.gradle", "unityLibrary/build.gradle"
        };
        int missing = 0;
        foreach (var rel in required)
        {
            string p = Path.Combine(outDir, rel);
            bool ok = File.Exists(p);
            if (!ok) missing++;
            Debug.Log("[GRADLE_EXPORT] " + (ok ? "OK   " : "MISS ") + rel);
        }
        Debug.Log("GRADLE_EXPORT_CHECK=" + (missing == 0 ? "PASS" : "FAIL") + " missing=" + missing);
        if (missing == 0)
            Debug.Log("下一步：在该目录 git init → 提交 → 推送到**新的** GitHub 仓库（不要和 Unity 主工程混在一起），再填 40 位 Commit ID。");
        else
            Debug.LogWarning("导出目录不完整：确认 Build Settings 勾选 Export Project，且导出前没有编译错误。");
    }

    /// <summary>移除 Unity 导出时写入的本机路径，让平台从环境变量或 local.properties 解析 SDK/NDK。</summary>
    static void SanitizeGradleProject(string outDir)
    {
        var propsPath = Path.Combine(outDir, "gradle.properties");
        var forbiddenPrefixes = new[]
        {
            "unityProjectPath=", "unity.projectPath=", "unity.androidSdkPath=",
            "unity.androidNdkPath=", "unity.jdkPath="
        };
        var propsLines = File.ReadAllLines(propsPath)
            .Where(line => !forbiddenPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        File.WriteAllLines(propsPath, propsLines, new UTF8Encoding(false));

        RemoveNdkPathFromFile(Path.Combine(outDir, "launcher", "build.gradle"));
        var unityBuild = Path.Combine(outDir, "unityLibrary", "build.gradle");
        var text = File.ReadAllText(unityBuild);
        text = RemoveNdkPath(text);
        text = text.Replace("getProperty(\"unity.androidNdkPath\")", "getNdkPath()");
        text = text.Replace("getProperty(\"unity.androidSdkPath\")", "getSdkPath()");
        const string oldSdkResolver = @"def getSdkDir() {
    Properties local = new Properties()
    local.load(new FileInputStream(""${rootDir}/local.properties""))
    return local.getProperty('sdk.dir')
}";
        const string portableResolvers = @"def getSdkPath() {
    def envPath = System.getenv('ANDROID_SDK_ROOT') ?: System.getenv('ANDROID_HOME')
    if (envPath) return envPath
    def local = new Properties()
    def localFile = new File(""${rootDir}/local.properties"")
    if (localFile.exists()) localFile.withInputStream { local.load(it) }
    return local.getProperty('sdk.dir')
}
def getNdkPath() {
    def envPath = System.getenv('ANDROID_NDK_HOME') ?: System.getenv('ANDROID_NDK_ROOT')
    if (envPath) return envPath
    def local = new Properties()
    def localFile = new File(""${rootDir}/local.properties"")
    if (localFile.exists()) localFile.withInputStream { local.load(it) }
    return local.getProperty('ndk.dir')
}
def getSdkDir() { return getSdkPath() }";
        text = text.Replace(oldSdkResolver, portableResolvers);
        File.WriteAllText(unityBuild, text, new UTF8Encoding(false));
        SanitizeCompileData(outDir);

        bool portable = propsLines.All(line => !forbiddenPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            && !File.ReadAllText(Path.Combine(outDir, "launcher", "build.gradle")).Contains("ndkPath ")
            && !File.ReadAllText(unityBuild).Contains("ndkPath ");
        Debug.Log("GRADLE_PORTABLE_CHECK=" + (portable ? "PASS" : "FAIL"));
    }

    static void SanitizeCompileData(string outDir)
    {
        var path = Path.Combine(outDir, "unityLibrary", "src", "main", "Il2CppOutputProject",
            "Source", "il2cppOutput", "compile-data.json");
        if (!File.Exists(path)) return;
        var root = Path.GetFullPath(outDir);
        var ndk = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath,
            "PlaybackEngines", "AndroidPlayer", "NDK"));
        var text = File.ReadAllText(path)
            .Replace(root.Replace('\\', '/') + "/", "")
            .Replace(root + "\\", "")
            .Replace(ndk.Replace('\\', '/'), "");
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    static void RemoveNdkPathFromFile(string path)
    {
        var text = File.ReadAllText(path);
        File.WriteAllText(path, RemoveNdkPath(text), new UTF8Encoding(false));
    }

    static string RemoveNdkPath(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join("\n", lines.Where(line => !line.TrimStart().StartsWith("ndkPath ", StringComparison.Ordinal)));
    }
}
