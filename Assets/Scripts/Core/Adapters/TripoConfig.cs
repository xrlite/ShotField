namespace ShotField.Core
{
    /// <summary>
    /// Tripo 3D 接入配置快照。路径、合并、迁移与环境变量优先级统一委托
    /// <see cref="LocalAiConfig"/>，本类不再维护第二套文件读取逻辑。
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
        public static TripoConfig Load() => LocalAiConfig.LoadTripo();
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint);
    }
}
