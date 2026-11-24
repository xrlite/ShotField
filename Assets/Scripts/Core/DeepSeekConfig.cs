namespace ShotField.Core
{
    /// <summary>
    /// DeepSeek 接入配置快照。
    /// 来源优先级：环境变量 <c>SHOTFIELD_DEEPSEEK_KEY</c> -> <c>persistentDataPath/ai_credentials.json</c>
    /// -> 旧持久化文件/编辑器基线 -> Resources 非敏感模板；Resources 中的 apiKey 永远不作为来源。
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

        /// <summary>从环境变量、持久化凭据、编辑器基线与非敏感模板装载；委托 <see cref="LocalAiConfig"/> 单一口径。</summary>
        public static DeepSeekConfig Load() => LocalAiConfig.LoadDeepSeek();

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
