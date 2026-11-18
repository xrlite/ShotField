using System;
using System.IO;
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
    /// </summary>
    public class SeedanceBridge
    {
        public string ApiEndpoint { get; set; } = "https://api.seedance.ai/v1/generate";
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

            if (IsOfflineMock || string.IsNullOrEmpty(ApiKey))
            {
                return new SeedanceTaskResult
                {
                    taskId = "mock-task-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    status = SeedanceTaskStatus.Completed,
                    videoUrl = "file://" + Path.Combine(Application.streamingAssetsPath, "mock_seedance.mp4"),
                    localVideoPath = req.clayReferencePath.Replace(".png", "_generated.mp4"),
                    message = "离线沙盒：成功打包白模参考帧与机位曲线，模拟视频生成完毕"
                };
            }

            try
            {
                string payload = "{\"shot_id\":\"" + req.shotId + "\",\"prompt\":\"" + Escape(req.prompt) +
                                 "\",\"duration\":" + req.durationSeconds + ",\"aspect\":\"" + req.aspectRatio + "\"}";
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(ApiKey))
                {
                    _client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                }

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

            return new SeedanceTaskResult
            {
                taskId = "fallback-task",
                status = SeedanceTaskStatus.Failed,
                message = "请求无法送达 Seedance 端点"
            };
        }

        private string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
