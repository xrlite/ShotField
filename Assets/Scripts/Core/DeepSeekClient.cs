using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    // ---- DeepSeek 响应包装（usage 供接口级探针统计 token 用量）----

    [Serializable]
    public class DeepSeekChatResponse
    {
        public List<DeepSeekChoice> choices;
        public DeepSeekUsage usage;
    }

    [Serializable]
    public class DeepSeekChoice
    {
        public DeepSeekMessage message;
    }

    [Serializable]
    public class DeepSeekMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class DeepSeekUsage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    // ---- 请求载荷（JsonUtility 序列化，避免手写拼接破坏中文 / 引号 / 换行）----

    [Serializable]
    internal class DeepSeekRequestMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    internal class DeepSeekResponseFormat
    {
        public string type;
    }

    [Serializable]
    internal class DeepSeekRequestPayload
    {
        public string model;
        public float temperature;
        public DeepSeekRequestMessage[] messages;
        public DeepSeekResponseFormat response_format;
    }

    /// <summary>
    /// DeepSeek 聊天补全传输层：承载 HTTP、每请求独立鉴权头、超时、指数退避重试与错误码。
    /// 不做剧本语义解析；鉴权头只挂在 <see cref="HttpRequestMessage"/> 上，
    /// 不写 <c>HttpClient.DefaultRequestHeaders</c>，避免并发请求串味。
    /// </summary>
    public sealed class DeepSeekClient : IDisposable
    {
        private readonly DeepSeekConfig _config;
        private readonly HttpClient _http;
        private readonly bool _ownsClient;

        public DeepSeekClient(DeepSeekConfig config, HttpClient httpClient = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (httpClient != null)
            {
                _http = httpClient;
                _ownsClient = false;
            }
            else
            {
                _http = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds) };
                _ownsClient = true;
            }
        }

        /// <summary>发起一次补全请求；成功返回解析后的响应包装，失败抛 <see cref="ScriptParseException"/>。</summary>
        public async Task<DeepSeekChatResponse> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            string body = JsonUtility.ToJson(BuildPayload(systemPrompt, userPrompt));
            int attempts = Math.Max(0, _config.MaxRetries) + 1;
            ScriptParseException lastError = null;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var request = BuildRequest(body);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

                    using var resp = await _http
                        .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                        .ConfigureAwait(false);
                    string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (resp.IsSuccessStatusCode)
                    {
                        var parsed = JsonUtility.FromJson<DeepSeekChatResponse>(raw);
                        if (parsed == null)
                        {
                            throw new ScriptParseException(ScriptParseErrorCode.ParseError, "DeepSeek 响应体不可解析");
                        }
                        return parsed;
                    }

                    lastError = new ScriptParseException(
                        ScriptParseErrorCode.HttpError,
                        $"DeepSeek 返回 HTTP {(int)resp.StatusCode}",
                        (int)resp.StatusCode);

                    // 4xx（除 429）属请求错误，重试无意义，直接抛出；5xx / 限流走退避重试。
                    if ((int)resp.StatusCode < 500 && (int)resp.StatusCode != 429) throw lastError;
                }
                catch (ScriptParseException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    lastError = new ScriptParseException(ScriptParseErrorCode.Timeout, "DeepSeek 请求超时");
                }
                catch (HttpRequestException ex)
                {
                    lastError = new ScriptParseException(ScriptParseErrorCode.Network, $"DeepSeek 网络异常: {ex.Message}", ex);
                }

                if (attempt < attempts) await Task.Delay(BackoffDelayMs(attempt), ct).ConfigureAwait(false);
            }

            throw lastError ?? new ScriptParseException(ScriptParseErrorCode.Network, "DeepSeek 请求失败");
        }

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

        // 指数退避：第 1 次重试等 1s，第 2 次等 2s。
        private static int BackoffDelayMs(int attempt) => (int)Math.Pow(2, attempt - 1) * 1000;

        public void Dispose()
        {
            if (_ownsClient) _http.Dispose();
        }
    }
}
