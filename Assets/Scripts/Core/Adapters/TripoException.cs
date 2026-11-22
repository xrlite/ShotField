using System;

namespace ShotField.Core
{
    /// <summary>Tripo 资产生成失败的结构化错误码，风格对齐 <see cref="ScriptParseErrorCode"/>。</summary>
    public enum TripoErrorCode
    {
        NotConfigured = 0,
        Network = 1,
        Timeout = 2,
        HttpError = 3,
        TaskFailed = 4,
        ParseError = 5,
        DownloadFailed = 6,
        NotFound = 7
    }

    /// <summary>
    /// Tripo 调用结构化异常。Message 只放安全文案，绝不携带 ApiKey；
    /// 失败必须显式上报，不静默返回假资产。
    /// </summary>
    public sealed class TripoException : Exception
    {
        public TripoErrorCode ErrorCode { get; }
        public int HttpStatus { get; }

        public TripoException(TripoErrorCode errorCode, string message, Exception inner = null)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            HttpStatus = 0;
        }

        public TripoException(TripoErrorCode errorCode, string message, int httpStatus, Exception inner = null)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            HttpStatus = httpStatus;
        }
    }
}
