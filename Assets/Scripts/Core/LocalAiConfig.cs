using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 本地 AI 配置唯一读写口径：Resources 基线（已 gitignore，只读）+ persistentDataPath 覆盖合并。
    /// Key 只落持久化目录，不回写 Resources；界面与日志一律走 <see cref="Mask"/> 掩码，不带明文。
    /// Tripo 配置装载复用既有 <see cref="TripoConfig.Load"/>；这里只补持久化 Key 写入。
    /// </summary>
    public static class LocalAiConfig
    {
        private const string ResourceName = "ai_config.local";

        private static string PersistentPath =>
            Path.Combine(Application.persistentDataPath, "ai_config.local.json");

        /// <summary>装载 DeepSeek 配置；环境变量 Key 兜底，Resources 配置优先。</summary>
        public static DeepSeekConfig LoadDeepSeek()
        {
            var file = LoadMerged();
            var d = file?.deepseek ?? new DeepSeekSection();
            string endpoint = string.IsNullOrEmpty(d.endpoint)
                ? "https://api.deepseek.com/chat/completions" : d.endpoint;
            string model = string.IsNullOrEmpty(d.model) ? "deepseek-chat" : d.model;
            string apiKey = Environment.GetEnvironmentVariable("SHOTFIELD_DEEPSEEK_KEY") ?? "";
            if (!string.IsNullOrEmpty(d.apiKey)) apiKey = d.apiKey;
            int timeout = d.timeoutSeconds > 0 ? d.timeoutSeconds : 60;
            int retries = d.maxRetries >= 0 ? d.maxRetries : 2;
            return new DeepSeekConfig(endpoint, model, apiKey, timeout, retries);
        }

        /// <summary>装载 Tripo 配置快照（复用既有装载口径）。</summary>
        public static TripoConfig LoadTripo() => TripoConfig.Load();

        /// <summary>把某服务商的 Key 写入持久化配置；Resources 基线不被改动。</summary>
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
            File.WriteAllText(PersistentPath, JsonUtility.ToJson(file, true));
        }

        /// <summary>Key 掩码：前 3 位 + 后 4 位，中间打码；空值显示「未配置」。</summary>
        public static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "未配置";
            if (key.Length <= 6) return new string('•', key.Length);
            return key.Substring(0, 3) + "••••" + key.Substring(key.Length - 4);
        }

        private static AiConfigFile LoadMerged()
        {
            AiConfigFile file = null;
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null)
            {
                try { file = JsonUtility.FromJson<AiConfigFile>(asset.text); }
                catch (Exception ex) { Debug.LogWarning($"[LocalAiConfig] Resources 配置解析失败: {ex.Message}"); }
            }

            if (File.Exists(PersistentPath))
            {
                try
                {
                    var persisted = JsonUtility.FromJson<AiConfigFile>(File.ReadAllText(PersistentPath));
                    if (persisted != null) file = Merge(file, persisted);
                }
                catch (Exception ex) { Debug.LogWarning($"[LocalAiConfig] 持久化配置解析失败: {ex.Message}"); }
            }
            return file;
        }

        private static AiConfigFile Merge(AiConfigFile baseline, AiConfigFile over)
        {
            if (baseline == null) return over;
            if (over.deepseek != null) baseline.deepseek = over.deepseek;
            if (over.tripo != null) baseline.tripo = over.tripo;
            return baseline;
        }

        [Serializable]
        private sealed class DeepSeekSection
        {
            public string endpoint = "";
            public string model = "";
            public string apiKey = "";
            public int timeoutSeconds = 60;
            public int maxRetries = 2;
        }

        [Serializable]
        private sealed class TripoSection
        {
            public string endpoint = "";
            public string balanceEndpoint = "";
            public string apiKey = "";
            public string textToModelVersion = "";
            public string rigModelVersion = "";
            public int faceLimit = 5000;
            public int pollIntervalSeconds = 2;
            public int pollTimeoutSeconds = 300;
        }

        [Serializable]
        private sealed class AiConfigFile
        {
            public DeepSeekSection deepseek;
            public TripoSection tripo;
        }
    }
}
