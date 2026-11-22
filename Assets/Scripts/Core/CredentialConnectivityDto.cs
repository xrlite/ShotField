using System;

namespace ShotField.Core
{
    /// <summary>
    /// Transfer object describing credential status for a given service.
    /// Exposes masked keys for safe diagnostic display.
    /// </summary>
    [Serializable]
    public class CredentialStatusDto
    {
        public string serviceId;
        public bool hasKey;
        public string maskedKey;
        public string lastUpdatedTimestamp;
        public bool isEnvironmentInjected;

        public CredentialStatusDto()
        {
        }

        public CredentialStatusDto(string serviceId, bool hasKey, string maskedKey, bool envInjected = false)
        {
            this.serviceId = serviceId;
            this.hasKey = hasKey;
            this.maskedKey = maskedKey;
            this.isEnvironmentInjected = envInjected;
            this.lastUpdatedTimestamp = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>
    /// Transfer object describing real-time connection status and latency.
    /// </summary>
    [Serializable]
    public class ConnectivityStatusDto
    {
        public string serviceId;
        public int status;
        public bool isOfflineMock;
        public float latencyMs;
        public int consecutiveFailureCount;
        public int retryAfterSeconds;
        public string lastProbeTimestamp;
        public string statusMessage;

        public ConnectivityStatusDto()
        {
        }

        public ConnectivityStatusDto(
            string serviceId,
            NetworkStatus status,
            bool isMock,
            float latency = 0f,
            string message = "")
        {
            this.serviceId = serviceId;
            this.status = (int)status;
            this.isOfflineMock = isMock;
            this.latencyMs = latency;
            this.consecutiveFailureCount = 0;
            this.retryAfterSeconds = 0;
            this.statusMessage = message;
            this.lastProbeTimestamp = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>
    /// Transfer object holding service endpoint configuration and runtime policies.
    /// </summary>
    [Serializable]
    public class ServiceEndpointConfigDto
    {
        public string serviceId;
        public string baseUrl;
        public string apiVersion;
        public int timeoutSeconds;
        public int maxRetryCount;
        public bool bypassSslValidation;
        public string customHeaderKey;
        public string customHeaderValue;

        public ServiceEndpointConfigDto()
        {
            apiVersion = "v3";
            timeoutSeconds = 30;
            maxRetryCount = 3;
            bypassSslValidation = false;
        }

        public ServiceEndpointConfigDto(
            string serviceId,
            string baseUrl,
            int timeout = 30,
            int retries = 3)
        {
            this.serviceId = serviceId;
            this.baseUrl = baseUrl;
            this.apiVersion = "v3";
            this.timeoutSeconds = timeout;
            this.maxRetryCount = retries;
            this.bypassSslValidation = false;
            this.customHeaderKey = string.Empty;
            this.customHeaderValue = string.Empty;
        }
    }
}
