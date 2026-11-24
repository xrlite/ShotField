using System;
using System.IO;
using UnityEngine;
namespace ShotField.Core
{
    /// <summary>
    /// AI 凭据唯一读写口径。优先级：环境变量 Key &gt; persistentDataPath/ai_credentials.json
    /// &gt; 旧 persistentDataPath/ai_config.local.json &gt; 编辑器 Tools 基线 &gt; Resources 非敏感模板。
    /// Resources 模板的 apiKey 永远清空；凭据文件只落 persistentDataPath，绝不写回 Assets/Git。
    /// </summary>
    public static class LocalAiConfig
    {
        public const string CredentialFileName = "ai_credentials.json";
        public const string LegacyCredentialFileName = "ai_config.local.json";
        private const string ResourceName = "ai_config.local";
        public static string PersistentPath => Path.Combine(Application.persistentDataPath, CredentialFileName);
        public static string LegacyPersistentPath => Path.Combine(Application.persistentDataPath, LegacyCredentialFileName);
#if UNITY_EDITOR
        private static string EditorBaselinePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", LegacyCredentialFileName);
#endif
        public static DeepSeekConfig LoadDeepSeek()
        {
            var file = LoadMerged();
            var d = file?.deepseek ?? new DeepSeekSection();
            string endpoint = string.IsNullOrEmpty(d.endpoint) ? "https://api.deepseek.com/chat/completions" : d.endpoint;
            string model = string.IsNullOrEmpty(d.model) ? "deepseek-chat" : d.model;
            string apiKey = ResolveKey(d.apiKey, "SHOTFIELD_DEEPSEEK_KEY");
            int timeout = d.timeoutSeconds > 0 ? d.timeoutSeconds : 60;
            int retries = d.maxRetries >= 0 ? d.maxRetries : 2;
            return new DeepSeekConfig(endpoint, model, apiKey, timeout, retries);
        }
        public static TripoConfig LoadTripo()
        {
            var file = LoadMerged();
            var t = file?.tripo ?? new TripoSection();
            string endpoint = string.IsNullOrEmpty(t.endpoint) ? "https://openapi.tripo3d.ai/v3" : t.endpoint;
            string balance = string.IsNullOrEmpty(t.balanceEndpoint) ? "https://api.tripo3d.ai/v2/openapi/user/balance" : t.balanceEndpoint;
            string textVersion = string.IsNullOrEmpty(t.textToModelVersion) ? "v3.1-20260211" : t.textToModelVersion;
            string rigVersion = string.IsNullOrEmpty(t.rigModelVersion) ? "v3.0-20260909" : t.rigModelVersion;
            int faces = t.faceLimit > 0 ? t.faceLimit : 5000;
            int interval = t.pollIntervalSeconds > 0 ? t.pollIntervalSeconds : 2;
            int pollTimeout = t.pollTimeoutSeconds > 0 ? t.pollTimeoutSeconds : 300;
            string apiKey = ResolveKey(t.apiKey, "SHOTFIELD_TRIPO_KEY");
            return new TripoConfig(endpoint, balance, apiKey, textVersion, rigVersion, faces, interval, pollTimeout);
        }
        public static void SaveApiKey(string serviceId, string apiKey)
        {
            var file = LoadMerged() ?? new AiConfigFile();
            if (serviceId == "tripo")
            {
                file.tripo ??= new TripoSection();
                file.tripo.apiKey = apiKey ?? "";
            }
            else
            {
                file.deepseek ??= new DeepSeekSection();
                file.deepseek.apiKey = apiKey ?? "";
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PersistentPath));
            File.WriteAllText(PersistentPath, JsonUtility.ToJson(file, true));
        }
        public static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "未配置";
            if (key.Length <= 6) return new string('•', key.Length);
            return key.Substring(0, 3) + "••••" + key.Substring(key.Length - 4);
        }
        public static bool ShouldMigrateLegacy(bool legacyExists, bool targetExists) => legacyExists && !targetExists;
        public static bool MigrateLegacyPersistent()
        {
            if (!ShouldMigrateLegacy(File.Exists(LegacyPersistentPath), File.Exists(PersistentPath))) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistentPath));
                File.Copy(LegacyPersistentPath, PersistentPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalAiConfig] 旧凭据迁移失败: {ex.GetType().Name}");
                return false;
            }
        }
        private static AiConfigFile LoadMerged()
        {
            AiConfigFile file = LoadResourcesTemplate();
#if UNITY_EDITOR
            file = Merge(file, ReadJsonFile(EditorBaselinePath));
#endif
            MigrateLegacyPersistent();
            file = Merge(file, ReadJsonFile(LegacyPersistentPath));
            file = Merge(file, ReadJsonFile(PersistentPath));
            return file;
        }
        private static AiConfigFile LoadResourcesTemplate()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return null;
            AiConfigFile file;
            try { file = JsonUtility.FromJson<AiConfigFile>(asset.text); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalAiConfig] Resources 模板解析失败: {ex.Message}");
                return null;
            }
            if (file?.deepseek != null) file.deepseek.apiKey = "";
            if (file?.tripo != null) file.tripo.apiKey = "";
            return file;
        }
        private static AiConfigFile ReadJsonFile(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<AiConfigFile>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalAiConfig] 配置解析失败 {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }
        private static string ResolveKey(string fileKey, string envVar)
        {
            var env = Environment.GetEnvironmentVariable(envVar);
            return !string.IsNullOrEmpty(env) ? env : (fileKey ?? "");
        }
        private static AiConfigFile Merge(AiConfigFile baseline, AiConfigFile over)
        {
            if (baseline == null) return over;
            if (over?.deepseek != null) baseline.deepseek = over.deepseek;
            if (over?.tripo != null) baseline.tripo = over.tripo;
            return baseline;
        }
        [Serializable] private sealed class DeepSeekSection
        {
            public string endpoint = ""; public string model = ""; public string apiKey = "";
            public int timeoutSeconds = 60; public int maxRetries = 2;
        }
        [Serializable] private sealed class TripoSection
        {
            public string endpoint = ""; public string balanceEndpoint = ""; public string apiKey = "";
            public string textToModelVersion = ""; public string rigModelVersion = "";
            public int faceLimit = 5000; public int pollIntervalSeconds = 2; public int pollTimeoutSeconds = 300;
        }
        [Serializable] private sealed class AiConfigFile
        {
            public DeepSeekSection deepseek; public TripoSection tripo;
        }
    }
}
