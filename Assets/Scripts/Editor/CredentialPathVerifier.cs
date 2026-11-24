#if UNITY_EDITOR
using System;
using System.IO;
using ShotField.Core;
using UnityEditor;
using UnityEngine;
namespace ShotField.EditorTools
{
    /// <summary>凭据单路径、迁移优先级与构建闸契约验收；不读取或输出真实 Key。</summary>
    public static class CredentialPathVerifier
    {
        private const int Expected = 9;
        [MenuItem("ShotField/Verifiers/Run Credential Path Verifier")]
        public static void Run()
        {
            int passed = 0;
            Check("新凭据文件名固定为 ai_credentials.json", LocalAiConfig.CredentialFileName == "ai_credentials.json", ref passed);
            Check("旧凭据文件名保留为 ai_config.local.json", LocalAiConfig.LegacyCredentialFileName == "ai_config.local.json", ref passed);
            Check("persistentDataPath 新路径与旧路径分离",
                Path.GetFileName(LocalAiConfig.PersistentPath) == "ai_credentials.json" &&
                Path.GetFileName(LocalAiConfig.LegacyPersistentPath) == "ai_config.local.json", ref passed);
            Check("旧文件迁移只在目标不存在时发生",
                LocalAiConfig.ShouldMigrateLegacy(true, false) &&
                !LocalAiConfig.ShouldMigrateLegacy(true, true) &&
                !LocalAiConfig.ShouldMigrateLegacy(false, false), ref passed);
            string ds = Environment.GetEnvironmentVariable("SHOTFIELD_DEEPSEEK_KEY");
            string tp = Environment.GetEnvironmentVariable("SHOTFIELD_TRIPO_KEY");
            try
            {
                Environment.SetEnvironmentVariable("SHOTFIELD_DEEPSEEK_KEY", "verifier-deepseek-123");
                Environment.SetEnvironmentVariable("SHOTFIELD_TRIPO_KEY", "verifier-tripo-123");
                Check("DeepSeek 环境变量优先级最高", DeepSeekConfig.Load().ApiKey == "verifier-deepseek-123", ref passed);
                Check("Tripo 环境变量优先级最高", TripoConfig.Load().ApiKey == "verifier-tripo-123", ref passed);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SHOTFIELD_DEEPSEEK_KEY", ds);
                Environment.SetEnvironmentVariable("SHOTFIELD_TRIPO_KEY", tp);
            }
            string core = Path.Combine(Application.dataPath, "Scripts", "Core");
            string tripoSource = File.ReadAllText(Path.Combine(core, "Adapters", "TripoConfig.cs"));
            Check("TripoConfig 不再维护第二套路径实现",
                tripoSource.Contains("LocalAiConfig.LoadTripo()") && !tripoSource.Contains("ai_config.local.json"), ref passed);
            string localSource = File.ReadAllText(Path.Combine(core, "LocalAiConfig.cs"));
            Check("保存只写新持久化路径，不写旧路径",
                localSource.Contains("File.WriteAllText(PersistentPath") &&
                !localSource.Contains("File.WriteAllText(LegacyPersistentPath"), ref passed);
            string guard = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Editor", "CredentialBuildGuard.cs"));
            string scan = File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", "巡检", "secret_scan.ps1"));
            string ignore = File.ReadAllText(Path.Combine(Application.dataPath, "..", ".gitignore"));
            Check("构建闸、扫描器与 gitignore 都识别新凭据文件名",
                guard.Contains("LocalAiConfig.CredentialFileName") &&
                scan.Contains("forbidden-credential-file") &&
                ignore.Contains("ai_credentials.json"), ref passed);
            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"CREDENTIAL-PATH-RESULT={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }
        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[CREDENTIAL-PATH] PASS: {label}"); }
            else Debug.LogError($"[CREDENTIAL-PATH] FAIL: {label}");
        }
    }
}
#endif
