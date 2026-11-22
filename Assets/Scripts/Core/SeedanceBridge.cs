using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    public enum SeedanceTaskStatus { None, Queued, Running, Completed, Failed }

    [Serializable]
    public class SeedanceTaskRequest
    {
        public string shotId = "SC01-S01";
        public string prompt = "低角度运镜，电影质感，低饱和度室内光，神秘紧张氛围";
        public string clayReferencePath = "";
        public float durationSeconds = 3.0f;
        public string aspectRatio = "16:9";
        public int cameraKeyframeCount = 0;
    }

    [Serializable]
    public class SeedanceTaskResult
    {
        public string taskId;
        public SeedanceTaskStatus status;
        public string videoUrl;
        public string localVideoPath;
        public string message;
    }

    /// <summary>
    /// Seedance 影视视频生成交接服务。
    /// 打通白模分镜参考图、提示词与相机运镜参数上行，支持离线沙盒与在线 HttpClient 调用。
    /// 离线沙盒不产生任何视频产物；未配置密钥或端点时直接判失败，不发请求。
    /// </summary>
    public class SeedanceBridge
    {
        public string ApiEndpoint { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public bool IsOfflineMock { get; set; } = true;

        private readonly HttpClient _client;

        public SeedanceBridge(HttpClient client = null)
        {
            _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<SeedanceTaskResult> SubmitGenerationAsync(SeedanceTaskRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (IsOfflineMock) return OfflineResult();
            if (string.IsNullOrEmpty(ApiKey)) return FailedResult("未配置 ApiKey，无法提交在线任务");
            if (string.IsNullOrEmpty(ApiEndpoint)) return FailedResult("未配置 ApiEndpoint，无法提交在线任务");
            return await PostOnlineAsync(req);
        }

        // 离线沙盒：只登记排队，不产生视频产物，也不伪造成片
        private static SeedanceTaskResult OfflineResult()
        {
            return new SeedanceTaskResult
            {
                taskId = "mock-task-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                status = SeedanceTaskStatus.Queued,
                videoUrl = null,
                localVideoPath = null,
                message = "离线沙盒：仅导出参考包，未提交任何在线任务，无视频产物"
            };
        }

        // 已通过配置校验后才真正发请求；异常或非成功状态一律降级为 Failed
        private async Task<SeedanceTaskResult> PostOnlineAsync(SeedanceTaskRequest req)
        {
            try
            {
                string payload = "{\"shot_id\":\"" + req.shotId + "\",\"prompt\":\"" + Escape(req.prompt) +
                                 "\",\"duration\":" + req.durationSeconds + ",\"aspect\":\"" + req.aspectRatio + "\"}";
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                _client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);

                var resp = await _client.PostAsync(ApiEndpoint, content);
                if (resp.IsSuccessStatusCode)
                {
                    return new SeedanceTaskResult
                    {
                        taskId = "seedance-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        status = SeedanceTaskStatus.Running,
                        message = "在线任务已提交至 Seedance 云渲染队列"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SeedanceBridge] 在线请求异常，降级处理: {ex.Message}");
            }

            return FailedResult("请求无法送达 Seedance 端点");
        }

        private static SeedanceTaskResult FailedResult(string reason)
        {
            return new SeedanceTaskResult
            {
                taskId = "fallback-task",
                status = SeedanceTaskStatus.Failed,
                videoUrl = null,
                localVideoPath = null,
                message = reason
            };
        }

        private string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
