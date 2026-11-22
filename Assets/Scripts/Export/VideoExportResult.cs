namespace ShotField.Export
{
    public enum VideoExportState
    {
        Idle = 0,
        Recording = 1,
        Processing = 2,
        Done = 3,
        Failed = 4,
        Unsupported = 5
    }

    /// <summary>
    /// 一次 mp4 导出的回执数据：文件名、绝对路径、时长、体积、帧数与错误信息。
    /// 纯 DTO，不持有场景对象；OK 判定要求有帧且体积大于 0。
    /// </summary>
    public sealed class VideoExportResult
    {
        public VideoExportState State;
        public string FileName = string.Empty;
        public string AbsolutePath = string.Empty;
        public long DurationMs;
        public long ByteSize;
        public int Width;
        public int Height;
        public int FrameCount;
        public string ErrorMessage = string.Empty;

        public bool Ok => State == VideoExportState.Done && ByteSize > 0 && FrameCount > 0;

        /// <summary>UI 回执一行文案：成功「已保存：名.mp4（5.00s，1.23 MB）」，失败带原因。</summary>
        public string Summary
        {
            get
            {
                if (Ok)
                {
                    double seconds = DurationMs / 1000.0;
                    return $"已保存：{FileName}（{seconds:0.00}s，{FormatBytes(ByteSize)}）";
                }
                return string.IsNullOrEmpty(ErrorMessage) ? "导出失败" : "导出失败：" + ErrorMessage;
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024L) return $"{bytes} B";
            if (bytes < 1024L * 1024L) return $"{bytes / 1024.0:0.00} KB";
            return $"{bytes / (1024.0 * 1024.0):0.00} MB";
        }
    }
}
