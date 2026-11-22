using System;

namespace ShotField.Core
{
    /// <summary>
    /// 剧本解析失败的结构化错误码，供上游区分「未配置 / 网络 / 超时 / 上游 4xx-5xx / 解析失败 / 空内容」。
    /// </summary>
    public enum ScriptParseErrorCode
    {
        NotConfigured = 0,
        Network = 1,
        Timeout = 2,
        HttpError = 3,
        ParseError = 4,
        EmptyContent = 5
    }

    /// <summary>
    /// 剧本解析结构化异常。Message 只放安全文案，绝不携带 ApiKey；
    /// 在线失败不再静默降级成 mock 数据，由调用方决定提示或显式兜底。
    /// </summary>
    public sealed class ScriptParseException : Exception
    {
        /// <summary>错误分类码。</summary>
        public ScriptParseErrorCode ErrorCode { get; }

        /// <summary>上游 HTTP 状态码；非 HTTP 错误时为 0。</summary>
        public int HttpStatus { get; }

        public ScriptParseException(ScriptParseErrorCode errorCode, string message, Exception inner = null)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            HttpStatus = 0;
        }

        public ScriptParseException(ScriptParseErrorCode errorCode, string message, int httpStatus, Exception inner = null)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            HttpStatus = httpStatus;
        }
    }
}
