using System.Collections.Generic;

namespace ShotField.Import
{
    /// <summary>
    /// 导入层状态机。本层只负责「提取可预览的结构化文本 + 校验」，
    /// 确认后才允许上游调用 DeepSeek；<see cref="PendingConfirmation"/> 即「尚未确认，禁止直接发 AI」的明确信号。
    /// </summary>
    public enum ScriptImportStatus
    {
        /// <summary>提取成功，等待用户预览、修正角色名与格式；未确认，禁止直接调用 AI。</summary>
        PendingConfirmation = 0,

        /// <summary>读取或解析失败，FailureReason 说明原因。</summary>
        Failed = 1,

        /// <summary>PDF 无文本层（扫描件/图片版），归 Labs，需要 OCR；不得静默返回空文本当成功。</summary>
        NeedsOcr = 2,

        /// <summary>扩展名不支持（如老二进制 .doc）。</summary>
        UnsupportedFormat = 3,

        /// <summary>文件可读但提取出的正文为空。</summary>
        EmptyText = 4
    }

    /// <summary>
    /// 剧本导入结果 DTO。自包含，不依赖 Core 程序集，方便 Import 层单独编译与测试。
    /// </summary>
    public class ScriptImportResult
    {
        /// <summary>整体是否可进入预览环节（PendingConfirmation 才算成功）。</summary>
        public bool Success;

        public ScriptImportStatus Status;

        /// <summary>提取出的纯文本（已剥离格式符号）。</summary>
        public string PlainText = string.Empty;

        /// <summary>剧名 / 首个标题行。</summary>
        public string Title = string.Empty;

        /// <summary>识别出的人物名（对白发言者，去重，保持出现顺序）。</summary>
        public List<string> Characters = new List<string>();

        /// <summary>对白条数（含「角色：台词」结构的行数）。</summary>
        public int DialogueCount;

        /// <summary>场景条数（内景 / 外景 / 场 标题行）。</summary>
        public int SceneCount;

        /// <summary>首段动作 / 梗概摘要（截断 60 字）。</summary>
        public string Summary = string.Empty;

        /// <summary>失败或降级原因；成功时为空。</summary>
        public string FailureReason = string.Empty;

        /// <summary>源文件路径（Android SAF 下为复制进沙盒后的路径）。</summary>
        public string SourcePath = string.Empty;

        /// <summary>探测到的编码（UTF-8 / UTF-8 BOM / GB18030 / 未知）。</summary>
        public string EncodingDetected = string.Empty;

        /// <summary>预览阶段允许用户修正角色名与格式；确认动作由上游界面负责。</summary>
        public bool RequiresUserReview = true;

        public static ScriptImportResult Pending(string sourcePath, string plainText, string encoding)
        {
            return new ScriptImportResult
            {
                Success = true,
                Status = ScriptImportStatus.PendingConfirmation,
                SourcePath = sourcePath ?? string.Empty,
                PlainText = plainText ?? string.Empty,
                EncodingDetected = encoding ?? string.Empty
            };
        }

        public static ScriptImportResult Fail(ScriptImportStatus status, string reason, string sourcePath)
        {
            return new ScriptImportResult
            {
                Success = false,
                Status = status,
                FailureReason = reason ?? string.Empty,
                SourcePath = sourcePath ?? string.Empty
            };
        }
    }
}
