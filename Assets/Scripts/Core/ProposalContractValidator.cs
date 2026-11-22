using System;
using System.Text.RegularExpressions;

namespace ShotField.Core
{
    /// <summary>
    /// 提案合同响应校验：枚举字段在原始 JSON 上复查，非法值不能被 JsonUtility
    /// 静默落成默认枚举；失败信息附带原始内容片段，供上层显示与重试。
    /// </summary>
    internal static class ProposalContractValidator
    {
        private const int PreviewLength = 200;

        /// <summary>从原始 JSON 提取 recommendedMode；缺失或非法返回 false。</summary>
        public static bool TryParseMode(string rawJson, out ProposalMode mode)
        {
            mode = ProposalMode.Tabletop;
            if (string.IsNullOrWhiteSpace(rawJson)) return false;

            var match = Regex.Match(rawJson, "\"recommendedMode\"\\s*:\\s*\"([A-Za-z]+)\"");
            if (!match.Success) return false;

            return Enum.TryParse(match.Groups[1].Value, true, out mode);
        }

        /// <summary>原始内容截断预览；不携带任何密钥。</summary>
        public static string Preview(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "(空)";
            var s = content.Trim();
            return s.Length <= PreviewLength ? s : s.Substring(0, PreviewLength) + "…";
        }
    }
}
