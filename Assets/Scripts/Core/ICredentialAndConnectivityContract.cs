using System;

namespace ShotField.Core
{
    /// <summary>
    /// Authoritative production endpoints and service IDs for external cloud services.
    /// Replaces hardcoded placeholder URLs.
    /// </summary>
    public static class ServiceEndpoints
    {
        /// <summary>Tripo 3D OpenAPI v3 base endpoint.</summary>
        public const string TripoOpenApiBase = "https://openapi.tripo3d.com/v3";

        /// <summary>Tripo 3D model draft generation task endpoint.</summary>
        public const string TripoDraftTaskUrl = "https://openapi.tripo3d.com/v3/task/draft";

        /// <summary>Tripo 3D model refinement task endpoint.</summary>
        public const string TripoRefineTaskUrl = "https://openapi.tripo3d.com/v3/task/refine";

        /// <summary>Volcengine Ark runtime API v3 base endpoint.</summary>
        public const string VolcengineArkBase = "https://ark.cn-beijing.volces.com/api/v3";

        /// <summary>Volcengine Ark chat completions endpoint for script parsing.</summary>
        public const string VolcengineChatCompletionsUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

        /// <summary>DeepSeek API v1 chat completions endpoint.</summary>
        public const string DeepSeekChatCompletionsUrl = "https://api.deepseek.com/v1/chat/completions";

        /// <summary>Standard HTTP request timeout in seconds for script and LLM operations.</summary>
        public const int DefaultLlmTimeoutSeconds = 30;

        /// <summary>Standard HTTP request timeout in seconds for 3D model generation operations.</summary>
        public const int DefaultModelGenTimeoutSeconds = 120;

        public const string ServiceIdTripo = "tripo";
        public const string ServiceIdVolcengine = "volcengine";
        public const string ServiceIdDeepSeek = "deepseek";
        public const string ServiceIdFeishu = "feishu";
    }

    /// <summary>
    /// Network and provider availability status.
    /// </summary>
    public enum NetworkStatus
    {
        Offline = 0,
        MockMode = 1,
        Connecting = 2,
        Connected = 3,
        AuthenticationFailed = 4,
        ServiceUnavailable = 5
    }

    /// <summary>
    /// Contract for secure credential storage, masking, and retrieval.
    /// Prevents committing raw API keys to version control.
    /// </summary>
    public interface ICredentialProvider
    {
        /// <summary>Retrieves the API key for the given service.</summary>
        string GetApiKey(string serviceId);

        /// <summary>Stores or updates the API key for the given service in memory.</summary>
        void SetApiKey(string serviceId, string apiKey);

        /// <summary>Checks whether an API key has been registered for the given service.</summary>
        bool HasApiKey(string serviceId);

        /// <summary>Removes any stored API key for the given service.</summary>
        void ClearApiKey(string serviceId);

        /// <summary>Returns a masked string for safe display in UI or logs.</summary>
        string GetMaskedApiKey(string serviceId);

        /// <summary>Validates format requirements for a specific service's API key.</summary>
        bool ValidateKeyFormat(string serviceId, string apiKey);
    }

    /// <summary>
    /// Contract for monitoring connectivity and toggling mock/live execution.
    /// </summary>
    public interface IConnectivityService
    {
        /// <summary>
        /// When true, network calls are intercepted and served from local mocks.
        /// </summary>
        bool IsOfflineMock { get; set; }

        /// <summary>
        /// Gets the current network status for a specific service.
        /// </summary>
        NetworkStatus GetCurrentStatus(string serviceId);

        /// <summary>
        /// Checks whether a specific external service is reachable and authenticated.
        /// </summary>
        bool IsServiceAvailable(string serviceId);

        /// <summary>
        /// Triggered whenever connectivity or authentication state changes for a service.
        /// </summary>
        event Action<string, NetworkStatus> OnConnectivityChanged;
    }
}
