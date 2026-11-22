using System;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// DeepSeek 接入配置快照。
    /// 来源优先级：<c>Resources/ai_config.local.json</c>（已加入 .gitignore，绝不进 git）
    /// -> 环境变量 <c>SHOTFIELD_DEEPSEEK_KEY</c> 兜底 key。
    /// 代码不硬编码 key，key 不写日志、不进任何报错文案。
    /// 实测可用地址：<c>https://api.deepseek.com/chat/completions</c>（不带 /v1）；
    /// 旧默认 <c>https://api.deepseek.com/v1/chat/completions</c> 在实测环境 404，已弃用。
    /// </summary>
    public sealed class DeepSeekConfig
    {
        public string Endpoint { get; }
        public string Model { get; }
        public string ApiKey { get; }
        public int TimeoutSeconds { get; }
        public int MaxRetries { get; }

        public DeepSeekConfig(string endpoint, string model, string apiKey, int timeoutSeconds, int maxRetries)
        {
            Endpoint = endpoint;
            Model = model;
            ApiKey = apiKey;
            TimeoutSeconds = timeoutSeconds;
            MaxRetries = maxRetries;
        }

        [Serializable]
        private sealed class ConfigFile
        {
            public DeepSeekSection deepseek;
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

        /// <summary>从 Resources 配置 + 环境变量装载配置；配置缺失或解析失败时退回安全默认值。</summary>
        public static DeepSeekConfig Load()
        {
            string endpoint = "https://api.deepseek.com/chat/completions";
            string model = "deepseek-chat";
            string apiKey = Environment.GetEnvironmentVariable("SHOTFIELD_DEEPSEEK_KEY") ?? "";
            int timeoutSeconds = 60;
            int maxRetries = 2;

            // Resources 资产名去最后扩展名：ai_config.local.json -> ai_config.local
            var asset = Resources.Load<TextAsset>("ai_config.local");
            if (asset != null)
            {
                try
                {
                    var file = JsonUtility.FromJson<ConfigFile>(asset.text);
                    var section = file?.deepseek;
                    if (section != null)
                    {
                        if (!string.IsNullOrEmpty(section.endpoint)) endpoint = section.endpoint;
                        if (!string.IsNullOrEmpty(section.model)) model = section.model;
                        if (!string.IsNullOrEmpty(section.apiKey)) apiKey = section.apiKey;
                        if (section.timeoutSeconds > 0) timeoutSeconds = section.timeoutSeconds;
                        if (section.maxRetries >= 0) maxRetries = section.maxRetries;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeepSeekConfig] 本地配置解析失败，走环境变量/默认值: {ex.Message}");
                }
            }

            return new DeepSeekConfig(endpoint, model, apiKey, timeoutSeconds, maxRetries);
        }

        /// <summary>在线分支配齐 Key 与端点才算已配置。</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint);

        /// <summary>
        /// 按当前配置构造解析器：用户显式选「离线演示」或配置未接通时走离线结构化；
        /// 配置存在且未选离线演示时默认走在线 DeepSeek。
        /// </summary>
        public static AiScriptParser CreateParser(bool offlineDemoRequested)
        {
            var config = Load();
            return new AiScriptParser { IsOfflineMock = offlineDemoRequested || !config.IsConfigured };
        }
    }
}
