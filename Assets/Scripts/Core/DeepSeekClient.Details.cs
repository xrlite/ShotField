using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    public sealed partial class DeepSeekClient
    {
        private HttpRequestMessage BuildRequest(string body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
            return request;
        }

        private DeepSeekRequestPayload BuildPayload(string systemPrompt, string userPrompt)
        {
            return new DeepSeekRequestPayload
            {
                model = _config.Model,
                temperature = 0.2f,
                messages = new[]
                {
                    new DeepSeekRequestMessage { role = "system", content = systemPrompt },
                    new DeepSeekRequestMessage { role = "user", content = userPrompt }
                },
                response_format = new DeepSeekResponseFormat { type = "json_object" }
            };
        }

        private DeepSeekRequestPayloadNoJson BuildPayloadNoJson(string systemPrompt, string userPrompt)
        {
            return new DeepSeekRequestPayloadNoJson
            {
                model = _config.Model,
                temperature = 0.2f,
                messages = new[]
                {
                    new DeepSeekRequestMessage { role = "system", content = systemPrompt },
                    new DeepSeekRequestMessage { role = "user", content = userPrompt }
                }
            };
        }

        // 指数退避：第 1 次重试等 1s，第 2 次等 2s。
        private static int BackoffDelayMs(int attempt) => (int)Math.Pow(2, attempt - 1) * 1000;

        public void Dispose()
        {
            if (_ownsClient) _http.Dispose();
        }
    }
}
