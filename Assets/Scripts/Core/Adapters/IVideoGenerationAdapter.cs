using System.Threading.Tasks;

namespace ShotField.Core
{
    /// <summary>
    /// 视频生成适配器抽象：即梦 / Seedance 同系可换端点，后续接入其他视频模型再实现本接口。
    /// 请求与结果复用 <see cref="SeedanceTaskRequest"/> / <see cref="SeedanceTaskResult"/>，
    /// 不另造平行产物结构。
    /// </summary>
    public interface IVideoGenerationAdapter
    {
        /// <summary>提供商标识（如 seedance-ark / jimeng），用于日志与连接中心展示。</summary>
        string ProviderId { get; }

        /// <summary>在线分支是否已配齐 Key 与端点；未配置时提交必须明确报「未配置」，不造假结果。</summary>
        bool IsConfigured { get; }

        /// <summary>提交一段镜头生成任务；离线沙盒只登记排队，不产生视频产物。</summary>
        Task<SeedanceTaskResult> SubmitAsync(SeedanceTaskRequest request);
    }
}
