using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// AI 凭据与开放服务配置的唯一读写口径。
    /// 优先级：环境变量 Key &gt; persistentDataPath/ai_credentials.json
    /// &gt; 旧 persistentDataPath/ai_config.local.json &gt; 编辑器 Tools 基线 &gt; Resources 非敏感模板。
    /// Resources 模板的 apiKey 永远清空；凭据文件只落 persistentDataPath，绝不写回 Assets/Git。
    /// 大模型段沿用 deepseek 字段名以兼容历史配置，但内容已是 Provider 无关的 OpenAI 兼容接口。
    /// </summary>
    public static partial class LocalAiConfig
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
            string endpoint = string.IsNullOrWhiteSpace(d.endpoint)
                ? "https://api.deepseek.com/chat/completions" : d.endpoint.Trim();
            string model = string.IsNullOrWhiteSpace(d.model) ? "deepseek-chat" : d.model.Trim();
            string providerId = string.IsNullOrWhiteSpace(d.providerId) ? "deepseek" : d.providerId.Trim();
            string providerName = string.IsNullOrWhiteSpace(d.providerName) ? "DeepSeek" : d.providerName.Trim();
            string apiKey = endpoint == "https://api.deepseek.com/chat/completions" ? ResolveKey(d.apiKey, "SHOTFIELD_LLM_KEY", "SHOTFIELD_DEEPSEEK_KEY") : d.apiKey ?? "";
            int timeout = d.timeoutSeconds > 0 ? d.timeoutSeconds : 60;
            int retries = d.maxRetries >= 0 ? d.maxRetries : 2;
            return new DeepSeekConfig(endpoint, model, apiKey, timeout, retries,
                providerId, providerName, d.useJsonMode);
        }

        public static TripoConfig LoadTripo()
        {
            var file = LoadMerged();
            var t = file?.tripo ?? new TripoSection();
            string endpoint = string.IsNullOrWhiteSpace(t.endpoint) ? "https://openapi.tripo3d.ai/v3" : t.endpoint.Trim();
            string balance = string.IsNullOrWhiteSpace(t.balanceEndpoint)
                ? "https://api.tripo3d.ai/v2/openapi/user/balance" : t.balanceEndpoint.Trim();
            string textVersion = string.IsNullOrWhiteSpace(t.textToModelVersion)
                ? "v3.1-20260211" : t.textToModelVersion.Trim();
            string rigVersion = string.IsNullOrWhiteSpace(t.rigModelVersion)
                ? "v3.0-20260909" : t.rigModelVersion.Trim();
            int faces = t.faceLimit > 0 ? t.faceLimit : 5000;
            int interval = t.pollIntervalSeconds > 0 ? t.pollIntervalSeconds : 2;
            int pollTimeout = t.pollTimeoutSeconds > 0 ? t.pollTimeoutSeconds : 300;
            string apiKey = ResolveKey(t.apiKey, "SHOTFIELD_TRIPO_KEY");
            return new TripoConfig(endpoint, balance, apiKey, textVersion, rigVersion, faces, interval, pollTimeout);
        }

        /// <summary>兼容旧调用：只改 Key，不改 Provider、端点和模型。</summary>
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
            WritePersistent(file);
        }

        /// <summary>保存开放大模型设置；apiKey 为 null 时保留当前密钥，空字符串表示清除。</summary>
        public static void SaveLlmSettings(string providerId, string providerName, string endpoint,
            string model, string apiKey, bool useJsonMode, int timeoutSeconds = 60, int maxRetries = 2)
        {
            var file = LoadMerged() ?? new AiConfigFile();
            var section = file.deepseek ??= new DeepSeekSection();
            section.providerId = string.IsNullOrWhiteSpace(providerId) ? "custom" : providerId.Trim();
            section.providerName = string.IsNullOrWhiteSpace(providerName) ? "大模型服务" : providerName.Trim();
            section.endpoint = (endpoint ?? "").Trim();
            section.model = (model ?? "").Trim();
            section.useJsonMode = useJsonMode;
            section.timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 60;
            section.maxRetries = maxRetries >= 0 ? maxRetries : 2;
            if (apiKey != null) section.apiKey = apiKey.Trim();
            WritePersistent(file);
        }

        /// <summary>保存 3D 服务设置；apiKey 为 null 时保留当前密钥，空字符串表示清除。</summary>
        public static void SaveTripoSettings(string endpoint, string balanceEndpoint, string apiKey)
        {
            var file = LoadMerged() ?? new AiConfigFile();
            var section = file.tripo ??= new TripoSection();
            section.endpoint = (endpoint ?? "").Trim();
            section.balanceEndpoint = (balanceEndpoint ?? "").Trim();
            if (apiKey != null) section.apiKey = apiKey.Trim();
            WritePersistent(file);
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

        private static void WritePersistent(AiConfigFile file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PersistentPath));
            File.WriteAllText(PersistentPath, JsonUtility.ToJson(file, true));
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

        private static string ResolveKey(string fileKey, params string[] envVars)
        {
            for (int i = 0; i < envVars.Length; i++)
            {
                var env = Environment.GetEnvironmentVariable(envVars[i]);
                if (!string.IsNullOrEmpty(env)) return env;
            }
            return fileKey ?? "";
        }

        private static AiConfigFile Merge(AiConfigFile baseline, AiConfigFile over)
        {
            if (baseline == null) return over;
            if (over?.deepseek != null) baseline.deepseek = over.deepseek;
            if (over?.tripo != null) baseline.tripo = over.tripo;
            return baseline;
        }

    }
}
