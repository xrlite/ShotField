using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Tripo 3D 接入配置快照。装载优先级（高 -> 低）：环境变量 <c>SHOTFIELD_TRIPO_KEY</c>
    /// > persistentDataPath 覆盖 > 编辑器基线 <c>&lt;projectRoot&gt;/Tools/ai_config.local.json</c>
    /// （仅 UNITY_EDITOR）> Resources 非敏感模板 > 空默认。
    ///
    /// Resources/** 会被无条件打进包体，故 Resources 模板只提供端点/版本号等非敏感默认值，
    /// 其 apiKey 在装载时强制清空，绝不能作为 key 来源；非编辑器分支不读 Assets 内任何 key 来源。
    /// key 不硬编码、不写日志、不进报错文案。
    /// 实测端点（官方 quick-start）：生成 <c>https://openapi.tripo3d.ai/v3</c>，
    /// 余额 <c>https://api.tripo3d.ai/v2/openapi/user/balance</c>。
    /// 注意：既有常量 <see cref="ServiceEndpoints.TripoOpenApiBase"/> 用的是 .com 域名，
    /// 本轮未改该常量以免影响其他调用方；新链路一律读本配置的 .ai 地址。
    /// </summary>
    public sealed class TripoConfig
    {
        public string Endpoint { get; }
        public string BalanceEndpoint { get; }
        public string ApiKey { get; }
        public string TextToModelVersion { get; }
        public string RigModelVersion { get; }
        public int FaceLimit { get; }
        public int PollIntervalSeconds { get; }
        public int PollTimeoutSeconds { get; }

        public TripoConfig(string endpoint, string balanceEndpoint, string apiKey,
            string textToModelVersion, string rigModelVersion, int faceLimit,
            int pollIntervalSeconds, int pollTimeoutSeconds)
        {
            Endpoint = endpoint;
            BalanceEndpoint = balanceEndpoint;
            ApiKey = apiKey;
            TextToModelVersion = textToModelVersion;
            RigModelVersion = rigModelVersion;
            FaceLimit = faceLimit;
            PollIntervalSeconds = pollIntervalSeconds;
            PollTimeoutSeconds = pollTimeoutSeconds;
        }

        [Serializable]
        private sealed class ConfigFile
        {
            public TripoSection tripo;
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

        private static string PersistentPath =>
            Path.Combine(Application.persistentDataPath, "ai_config.local.json");

#if UNITY_EDITOR
        private static string EditorBaselinePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Tools", "ai_config.local.json");
#endif

        /// <summary>按优先级装载；缺失或解析失败时退回安全默认值，key 不来自 Resources。</summary>
        public static TripoConfig Load()
        {
            string endpoint = "https://openapi.tripo3d.ai/v3";
            string balanceEndpoint = "https://api.tripo3d.ai/v2/openapi/user/balance";
            string apiKey = "";
            string textToModelVersion = "v3.1-20260211";
            string rigModelVersion = "v3.0-20260909";
            int faceLimit = 5000;
            int pollIntervalSeconds = 2;
            int pollTimeoutSeconds = 300;

            var section = ResolveSection();
            if (section != null)
            {
                if (!string.IsNullOrEmpty(section.endpoint)) endpoint = section.endpoint;
                if (!string.IsNullOrEmpty(section.balanceEndpoint)) balanceEndpoint = section.balanceEndpoint;
                if (!string.IsNullOrEmpty(section.textToModelVersion)) textToModelVersion = section.textToModelVersion;
                if (!string.IsNullOrEmpty(section.rigModelVersion)) rigModelVersion = section.rigModelVersion;
                if (section.faceLimit > 0) faceLimit = section.faceLimit;
                if (section.pollIntervalSeconds > 0) pollIntervalSeconds = section.pollIntervalSeconds;
                if (section.pollTimeoutSeconds > 0) pollTimeoutSeconds = section.pollTimeoutSeconds;
                apiKey = section.apiKey ?? "";
            }

            var env = Environment.GetEnvironmentVariable("SHOTFIELD_TRIPO_KEY");
            if (!string.IsNullOrEmpty(env)) apiKey = env;

            return new TripoConfig(endpoint, balanceEndpoint, apiKey, textToModelVersion,
                rigModelVersion, faceLimit, pollIntervalSeconds, pollTimeoutSeconds);
        }

        private static TripoSection ResolveSection()
        {
            var file = LoadResourcesTemplate();

#if UNITY_EDITOR
            file = Merge(file, ReadJsonFile(EditorBaselinePath));
#endif
            file = Merge(file, ReadJsonFile(PersistentPath));
            return file?.tripo;
        }

        // Resources 只作非敏感默认模板；apiKey 强制清空，绝不作为 key 来源。
        private static ConfigFile LoadResourcesTemplate()
        {
            var asset = Resources.Load<TextAsset>("ai_config.local");
            if (asset == null) return null;
            try
            {
                var file = JsonUtility.FromJson<ConfigFile>(asset.text);
                if (file?.tripo != null) file.tripo.apiKey = "";
                return file;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TripoConfig] Resources 模板解析失败，走环境变量/默认值: {ex.Message}");
                return null;
            }
        }

        private static ConfigFile ReadJsonFile(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<ConfigFile>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TripoConfig] 配置解析失败 {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        private static ConfigFile Merge(ConfigFile baseline, ConfigFile over)
        {
            if (baseline == null) return over;
            if (over?.tripo != null) baseline.tripo = over.tripo;
            return baseline;
        }

        /// <summary>在线分支配齐 Key 与端点才算已配置。</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint);
    }
}
