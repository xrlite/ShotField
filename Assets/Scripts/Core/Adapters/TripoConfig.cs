using System;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Tripo 3D 接入配置快照。装载方式与 <see cref="DeepSeekConfig"/> 同一套：
    /// <c>Resources/ai_config.local.json</c>（已 .gitignore）-> 环境变量 <c>SHOTFIELD_TRIPO_KEY</c> 兜底。
    /// key 不硬编码、不写日志、不进报错文案。
    /// 实测端点（官方 quick-start）：生成 <c>https://openapi.tripo3d.ai/v3</c>，
    /// 余额 <c>https://api.tripo3d.ai/v2/openapi/user/balance</c>。
    /// 注意：既有常量 <see cref="ServiceEndpoints.TripoOpenApiBase"/> 用的是 .com 域名，
    /// 本轮未实探 .com，也未改该常量以免影响其他调用方；新链路一律读本配置的 .ai 地址。
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

        /// <summary>从 Resources 配置 + 环境变量装载；缺失或解析失败时退回安全默认值。</summary>
        public static TripoConfig Load()
        {
            string endpoint = "https://openapi.tripo3d.ai/v3";
            string balanceEndpoint = "https://api.tripo3d.ai/v2/openapi/user/balance";
            string apiKey = Environment.GetEnvironmentVariable("SHOTFIELD_TRIPO_KEY") ?? "";
            string textToModelVersion = "v3.1-20260211";
            string rigModelVersion = "v3.0-20260909";
            int faceLimit = 5000;
            int pollIntervalSeconds = 2;
            int pollTimeoutSeconds = 300;

            // Resources 资产名去最后扩展名：ai_config.local.json -> ai_config.local
            var asset = Resources.Load<TextAsset>("ai_config.local");
            if (asset != null)
            {
                try
                {
                    var file = JsonUtility.FromJson<ConfigFile>(asset.text);
                    var section = file?.tripo;
                    if (section != null)
                    {
                        if (!string.IsNullOrEmpty(section.endpoint)) endpoint = section.endpoint;
                        if (!string.IsNullOrEmpty(section.balanceEndpoint)) balanceEndpoint = section.balanceEndpoint;
                        if (!string.IsNullOrEmpty(section.apiKey)) apiKey = section.apiKey;
                        if (!string.IsNullOrEmpty(section.textToModelVersion)) textToModelVersion = section.textToModelVersion;
                        if (!string.IsNullOrEmpty(section.rigModelVersion)) rigModelVersion = section.rigModelVersion;
                        if (section.faceLimit > 0) faceLimit = section.faceLimit;
                        if (section.pollIntervalSeconds > 0) pollIntervalSeconds = section.pollIntervalSeconds;
                        if (section.pollTimeoutSeconds > 0) pollTimeoutSeconds = section.pollTimeoutSeconds;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TripoConfig] 本地配置解析失败，走环境变量/默认值: {ex.Message}");
                }
            }

            return new TripoConfig(endpoint, balanceEndpoint, apiKey, textToModelVersion,
                rigModelVersion, faceLimit, pollIntervalSeconds, pollTimeoutSeconds);
        }

        /// <summary>在线分支配齐 Key 与端点才算已配置。</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint);
    }
}
