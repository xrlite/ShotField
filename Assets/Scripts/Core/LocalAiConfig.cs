using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 本地 AI 配置唯一读写口径。
    /// 装载优先级（高 -> 低）：环境变量 Key > persistentDataPath 覆盖 > 编辑器基线
    /// <c>&lt;projectRoot&gt;/Tools/ai_config.local.json</c>（仅 UNITY_EDITOR）> Resources 非敏感模板 > 空默认。
    ///
    /// 安全口径：Resources/** 会被 Unity 无条件打进包体，因此 Resources 资产只提供
    /// endpoint/model/版本号等非敏感默认值，其 apiKey 字段在装载时强制清空，绝不能作为 key 来源。
    /// 非编辑器分支不读任何 Assets 内的 key 来源，只走 persistentDataPath 与环境变量。
    /// Key 只落持久化目录，不回写 Resources；界面与日志一律走 <see cref="Mask"/> 掩码，不带明文。
    /// </summary>
    public static class LocalAiConfig
    {
        private const string ResourceName = "ai_config.local";

        private static string PersistentPath =>
            Path.Combine(Application.persistentDataPath, "ai_config.local.json");

#if UNITY_EDITOR
        // projectRoot 由 Application.dataPath（.../ShotField/Assets）上一级求得，不硬编码盘符。
        private static string EditorBaselinePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Tools", "ai_config.local.json");
#endif

        /// <summary>装载 DeepSeek 配置；环境变量 Key 优先级最高。</summary>
        public static DeepSeekConfig LoadDeepSeek()
        {
            var file = LoadMerged();
            var d = file?.deepseek ?? new DeepSeekSection();
            string endpoint = string.IsNullOrEmpty(d.endpoint)
                ? "https://api.deepseek.com/chat/completions" : d.endpoint;
            string model = string.IsNullOrEmpty(d.model) ? "deepseek-chat" : d.model;
            string apiKey = ResolveKey(d.apiKey, "SHOTFIELD_DEEPSEEK_KEY");
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
            AiConfigFile file = LoadResourcesTemplate();

#if UNITY_EDITOR
            file = Merge(file, ReadJsonFile(EditorBaselinePath));
#endif
            file = Merge(file, ReadJsonFile(PersistentPath));
            return file;
        }

        // Resources 只作非敏感默认模板；apiKey 强制清空，绝不作为 key 来源。
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
            try
            {
                return JsonUtility.FromJson<AiConfigFile>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalAiConfig] 配置解析失败 {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        // 环境变量优先级高于文件内 Key。
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
