using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>视频生成提供方：火山方舟 Seedance 与即梦同系，端点可互换。</summary>
    public enum VideoProviderKind
    {
        SeedanceArk = 0,
        Jimeng = 1
    }

    /// <summary>
    /// Seedance / 即梦视频生成适配器。组合复用既有 <see cref="SeedanceBridge"/> 的显式配置口径：
    /// 离线沙盒只登记 Queued 且无产物；在线分支未配置 Key 或端点时由桥接直接判 Failed，
    /// 适配器不伪造 Completed / videoUrl。端点可换：改 <see cref="ApiEndpoint"/> 即可切同系服务。
    /// </summary>
    public class SeedanceVideoAdapter : IVideoGenerationAdapter
    {
        public VideoProviderKind Kind { get; }
        public string ApiKey { get; set; } = "";
        public string ApiEndpoint { get; set; } = ServiceEndpoints.VolcengineArkBase;
        public bool IsOfflineMock { get; set; } = true;

        private SeedanceBridge _bridge;
        private readonly HttpClient _httpClient;

        public string ProviderId =>
            Kind == VideoProviderKind.Jimeng ? "jimeng" : "seedance-ark";

        /// <summary>在线分支配齐 Key 与端点才算已配置；离线 mock 不计入。</summary>
        public bool IsConfigured =>
            !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiEndpoint);

        public SeedanceVideoAdapter(VideoProviderKind kind = VideoProviderKind.SeedanceArk, HttpClient client = null)
        {
            Kind = kind;
            _httpClient = client;
        }

        public Task<SeedanceTaskResult> SubmitAsync(SeedanceTaskRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            EnsureBridge();

            _bridge.ApiKey = ApiKey;
            _bridge.ApiEndpoint = ApiEndpoint;
            _bridge.IsOfflineMock = IsOfflineMock;
            return _bridge.SubmitGenerationAsync(request);
        }

        private void EnsureBridge()
        {
            if (_bridge == null) _bridge = new SeedanceBridge(_httpClient);
        }
    }
}
