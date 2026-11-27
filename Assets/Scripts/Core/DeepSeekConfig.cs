using System;
namespace ShotField.Core
{
    /// <summary>
    /// OpenAI 兼容大模型接入配置快照。类名保留 DeepSeek 只为兼容旧引用，
    /// 配置本身不绑定单一服务商：ProviderId/ProviderName、Endpoint、Model、ApiKey 均可替换。
    /// 来源优先级：环境变量 SHOTFIELD_LLM_KEY / SHOTFIELD_DEEPSEEK_KEY
    /// -> persistentDataPath/ai_credentials.json -> 旧持久化文件/编辑器基线 -> Resources 非敏感模板。
    /// 代码不硬编码 key，key 不写日志、不进任何报错文案。
    /// </summary>
    public sealed class DeepSeekConfig
    {
        public string ProviderId { get; }
        public string ProviderName { get; }
        public string Endpoint { get; }
        public string Model { get; }
        public string ApiKey { get; }
        public int TimeoutSeconds { get; }
        public int MaxRetries { get; }
        public bool UseJsonMode { get; }

        public DeepSeekConfig(string endpoint, string model, string apiKey, int timeoutSeconds, int maxRetries,
            string providerId = "deepseek", string providerName = "DeepSeek", bool useJsonMode = true)
        {
            ProviderId = string.IsNullOrWhiteSpace(providerId) ? "custom" : providerId.Trim();
            ProviderName = string.IsNullOrWhiteSpace(providerName) ? "大模型服务" : providerName.Trim();
            Endpoint = endpoint?.Trim().TrimEnd('/');
            if (Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) &&
                (uri.AbsolutePath == "/" || uri.AbsolutePath.TrimEnd('/').EndsWith("/v1")) &&
                string.IsNullOrEmpty(uri.Query))
                Endpoint += uri.AbsolutePath == "/" ? "/v1/chat/completions" : "/chat/completions";
            Model = model;
            ApiKey = apiKey;
            TimeoutSeconds = timeoutSeconds;
            MaxRetries = maxRetries;
            UseJsonMode = useJsonMode;
        }

        public string DisplayName => string.IsNullOrWhiteSpace(ProviderName) ? "大模型服务" : ProviderName;

        /// <summary>从环境变量、持久化凭据、编辑器基线与非敏感模板装载；委托 <see cref="LocalAiConfig"/> 单一口径。</summary>
        public static DeepSeekConfig Load() => LocalAiConfig.LoadDeepSeek();

        /// <summary>在线分支配齐 Key、端点与模型才算已配置。</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(Model);

        /// <summary>
        /// 按当前配置构造解析器：用户显式选「离线演示」或配置未接通时走离线结构化；
        /// 配置存在且未选离线演示时默认走在线大模型。
        /// </summary>
        public static AiScriptParser CreateParser(bool offlineDemoRequested)
        {
            var config = Load();
            return new AiScriptParser { IsOfflineMock = offlineDemoRequested || !config.IsConfigured };
        }
    }
}
