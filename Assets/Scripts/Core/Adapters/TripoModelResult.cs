namespace ShotField.Core
{
    /// <summary>
    /// Tripo 模型生成结果。结构对齐 <see cref="SeedanceTaskResult"/>：
    /// 任务号 / 状态 / 远端 URL / 本地落盘路径 / 消息；未完成或失败时 URL 与本地路径为空，不伪造资产。
    /// </summary>
    [System.Serializable]
    public class TripoModelResult
    {
        public string taskId = "";
        public string status = "";
        public string modelUrl = "";
        public string localModelPath = "";
        public int progress;
        /// <summary>面数；响应未给出时为 -1，不编数字。</summary>
        public int faceCount = -1;
        public string message = "";

        public bool HasAsset => !string.IsNullOrEmpty(localModelPath);
    }
}
