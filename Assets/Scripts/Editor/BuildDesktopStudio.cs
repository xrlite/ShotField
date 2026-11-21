using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ShotField.EditorTools
{
    public static class BuildDesktopStudio
    {
        public static void Run()
        {
            var output = System.Environment.GetEnvironmentVariable("SHOTFIELD_PC_BUILD_DIR");
            if (string.IsNullOrEmpty(output))
                throw new System.InvalidOperationException("Set SHOTFIELD_PC_BUILD_DIR before starting Unity.");
            Directory.CreateDirectory(output);
            var result = BuildPipeline.BuildPlayer(new[] { BuildAndroid.ScenePath },
                Path.Combine(output, "ShotField.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
            UnityEngine.Debug.Log("SHOTFIELD-PC-BUILD=" + result.summary.result);
            if (result.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}