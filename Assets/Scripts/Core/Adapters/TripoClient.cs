using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Tripo 3D 传输层：提交文生模型、轮询任务、下载 GLB、查余额。
    /// 鉴权头挂在每请求 <see cref="HttpRequestMessage"/> 上，不写 <c>DefaultRequestHeaders</c>；
    /// 模型下载走预签名 CDN URL，不带 Bearer，避免 key 外泄到第三方域名。
    /// </summary>
    public sealed class TripoClient : IDisposable
    {
        private static readonly string[] TerminalStatuses = { "failed", "cancelled", "banned", "expired" };

        private readonly TripoConfig _config;
        private readonly HttpClient _http;
        private readonly bool _ownsClient;

        public TripoClient(TripoConfig config, HttpClient httpClient = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (httpClient != null)
            {
                _http = httpClient;
                _ownsClient = false;
            }
            else
            {
                _http = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.PollTimeoutSeconds) };
                _ownsClient = true;
            }
        }

        /// <summary>提交文生模型任务，返回 task_id。</summary>
        public async Task<string> SubmitTextToModelAsync(string prompt, CancellationToken ct = default)
        {
            var payload = new TripoTextToModelPayload
            {
                prompt = prompt,
                model = _config.TextToModelVersion,
                face_limit = _config.FaceLimit
            };
            string body = JsonUtility.ToJson(payload);
            string url = _config.Endpoint.TrimEnd('/') + "/generation/text-to-model";

            string raw = await SendJsonAsync(url, body, ct).ConfigureAwait(false);
            var response = JsonUtility.FromJson<TripoSubmitResponse>(raw);
            if (response == null || response.code != 0 || string.IsNullOrEmpty(response.data?.task_id))
            {
                throw new TripoException(TripoErrorCode.HttpError,
                    $"Tripo 提交被拒: {response?.message ?? "响应不可解析"}");
            }
            return response.data.task_id;
        }

        /// <summary>轮询任务直到 success / 终态 / 超时；成功返回任务数据。</summary>
        public async Task<TripoTaskData> PollTaskAsync(string taskId, Action<int> onProgress, CancellationToken ct = default)
        {
            string url = _config.Endpoint.TrimEnd('/') + "/tasks/" + Uri.EscapeDataString(taskId);
            var deadline = DateTime.UtcNow.AddSeconds(_config.PollTimeoutSeconds);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                string raw = await SendJsonAsync(url, null, ct).ConfigureAwait(false);
                var response = JsonUtility.FromJson<TripoTaskResponse>(raw);
                if (response == null || response.code != 0)
                {
                    throw new TripoException(TripoErrorCode.HttpError,
                        $"Tripo 任务查询失败: {response?.message ?? "响应不可解析"}");
                }

                var task = response.data;
                if (task != null)
                {
                    onProgress?.Invoke(task.progress);
                    if (task.status == "success") return task;
                    if (Array.IndexOf(TerminalStatuses, task.status) >= 0)
                    {
                        throw new TripoException(TripoErrorCode.TaskFailed,
                            $"Tripo 任务终态: {task.status} ({response.message})");
                    }
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new TripoException(TripoErrorCode.Timeout, $"Tripo 轮询超时（{_config.PollTimeoutSeconds}s）");
                }
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), ct).ConfigureAwait(false);
            }
        }

        /// <summary>从预签名 URL 下载 GLB 落盘，返回字节数。URL 5 分钟内有效，成功后须立即下载。</summary>
        public async Task<long> DownloadModelAsync(string modelUrl, string localPath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(modelUrl))
                throw new TripoException(TripoErrorCode.DownloadFailed, "model_url 为空，无法下载");

            using var request = new HttpRequestMessage(HttpMethod.Get, modelUrl);
            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TripoException(TripoErrorCode.Timeout, "GLB 下载超时");
            }
            catch (HttpRequestException ex)
            {
                throw new TripoException(TripoErrorCode.Network, $"GLB 下载网络异常: {ex.Message}", ex);
            }

            if (!resp.IsSuccessStatusCode)
            {
                resp.Dispose();
                throw new TripoException(TripoErrorCode.DownloadFailed,
                    $"GLB 下载 HTTP {(int)resp.StatusCode}", (int)resp.StatusCode);
            }

            string dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await using var fs = File.Create(localPath);
            await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
            resp.Dispose();

            return new FileInfo(localPath).Length;
        }

        /// <summary>查询余额（可用额度 / 冻结额度）。</summary>
        public async Task<(double balance, double frozen)> GetBalanceAsync(CancellationToken ct = default)
        {
            string raw = await SendJsonAsync(_config.BalanceEndpoint, null, ct).ConfigureAwait(false);
            var response = JsonUtility.FromJson<TripoBalanceResponse>(raw);
            if (response == null || response.code != 0 || response.data == null)
            {
                throw new TripoException(TripoErrorCode.HttpError,
                    $"Tripo 余额查询失败: {response?.message ?? "响应不可解析"}");
            }
            return (response.data.balance, response.data.frozen);
        }

        private async Task<string> SendJsonAsync(string url, string body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TripoException(TripoErrorCode.Timeout, "Tripo 请求超时");
            }
            catch (HttpRequestException ex)
            {
                throw new TripoException(TripoErrorCode.Network, $"Tripo 网络异常: {ex.Message}", ex);
            }

            string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                resp.Dispose();
                throw new TripoException(TripoErrorCode.HttpError, $"Tripo HTTP {(int)resp.StatusCode}", (int)resp.StatusCode);
            }
            return raw;
        }

        public void Dispose()
        {
            if (_ownsClient) _http.Dispose();
        }
    }
}
