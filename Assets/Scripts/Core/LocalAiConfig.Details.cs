using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    public static partial class LocalAiConfig
    {
        [Serializable] private sealed class DeepSeekSection
        {
            public string providerId = "deepseek";
            public string providerName = "DeepSeek";
            public string endpoint = "";
            public string model = "";
            public string apiKey = "";
            public int timeoutSeconds = 60;
            public int maxRetries = 2;
            public bool useJsonMode = true;
        }

        [Serializable] private sealed class TripoSection
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

        [Serializable] private sealed class AiConfigFile
        {
            public DeepSeekSection deepseek;
            public TripoSection tripo;
        }
    }
}
