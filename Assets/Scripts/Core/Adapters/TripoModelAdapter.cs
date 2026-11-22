using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Tripo 3D 模型生成适配器。风格对齐 <see cref="IVideoGenerationAdapter"/>：
    /// <see cref="ProviderId"/> 用于日志/连接中心展示，<see cref="IsConfigured"/> 表示在线分支配齐 Key 与端点。
    /// 流程：提交文生模型 -> 轮询至 success -> 立即下载 GLB 落盘 -> 返回本地路径。
    /// 任何失败 / 超时 / 终态 / 下载失败都抛 <see cref="TripoException"/>，不静默返回假资产。
    /// </summary>
    public class TripoModelAdapter
    {
        private readonly TripoConfig _config;
        private readonly HttpClient _httpClient;

        public string ProviderId => "tripo";

        public bool IsConfigured => _config != null && _config.IsConfigured;

        public TripoModelAdapter(TripoConfig config = null, HttpClient httpClient = null)
        {
            _config = config;
            _httpClient = httpClient;
        }

        /// <summary>
        /// 生成一个模型并落盘。
        /// </summary>
        /// <param name="prompt">模型文本描述。</param>
        /// <param name="fileName">落盘文件名（含 .glb 后缀）。</param>
        /// <param name="localDirectory">落盘目录；空则用 <see cref="Application.persistentDataPath"/>/TripoModels。</param>
        public async Task<TripoModelResult> GenerateModelAsync(
            string prompt, string fileName, string localDirectory = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new TripoException(TripoErrorCode.ParseError, "模型描述为空，无法提交生成");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new TripoException(TripoErrorCode.ParseError, "落盘文件名为空");

            var config = _config ?? TripoConfig.Load();
            if (!config.IsConfigured)
                throw new TripoException(TripoErrorCode.NotConfigured, "未配置 Tripo ApiKey / Endpoint，AI 资产未接通");

            using var client = new TripoClient(config, _httpClient);
            var result = new TripoModelResult { message = "" };

            result.taskId = await client.SubmitTextToModelAsync(prompt, ct).ConfigureAwait(false);
            var task = await client.PollTaskAsync(result.taskId, p => result.progress = p, ct).ConfigureAwait(false);

            string dir = string.IsNullOrEmpty(localDirectory)
                ? Path.Combine(Application.persistentDataPath, "TripoModels")
                : localDirectory;
            string localPath = Path.Combine(dir, fileName);

            await client.DownloadModelAsync(task.output?.model_url, localPath, ct).ConfigureAwait(false);

            result.status = task.status;
            result.modelUrl = task.output?.model_url ?? "";
            result.localModelPath = localPath;
            result.progress = task.progress;
            result.message = "Tripo 模型已生成并落盘";
            return result;
        }
    }
}
