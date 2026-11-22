using System.Collections.Generic;
using System.Linq;

namespace ShotField.Import
{
    /// <summary>
    /// 纯文本结构化统计：标题 / 人物 / 对白数 / 场景数 / 摘要。
    /// 四个导入器在各自提取出纯文本后统一走这里，保证四种格式口径一致。
    /// 启发式规则，不做语义理解；识别不准的角色名留给预览环节人工修正。
    /// </summary>
    public static class ScriptTextAnalyzer
    {
        private static readonly string[] ScenePrefixes = { "内景", "外景", "场景", "SCENE", "Scene" };
        private const int SummaryMaxChars = 60;

        public static ScriptImportResult Analyze(string plainText, string sourcePath, string encoding)
        {
            var result = ScriptImportResult.Pending(sourcePath, plainText, encoding);
            if (string.IsNullOrWhiteSpace(plainText))
            {
                result.Success = false;
                result.Status = ScriptImportStatus.EmptyText;
                result.FailureReason = "文件可读，但提取出的正文为空。";
                return result;
            }

            var lines = plainText
                .Split(new[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            result.Title = lines[0];
            var speakers = new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (IsSceneHeading(line))
                {
                    result.SceneCount++;
                    continue;
                }

                string speaker = TryGetSpeaker(line);
                if (speaker != null)
                {
                    result.DialogueCount++;
                    if (!speakers.Contains(speaker)) speakers.Add(speaker);
                    continue;
                }

                if (i > 0 && result.Summary.Length == 0)
                {
                    result.Summary = Truncate(line, SummaryMaxChars);
                }
            }

            result.Characters = speakers;
            return result;
        }

        private static bool IsSceneHeading(string line)
        {
            foreach (var prefix in ScenePrefixes)
            {
                if (line.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return true;
            }

            // 第X场 / 场1 这类编号标题
            if (line.Length >= 2 && line[0] == '场') return true;
            return line.StartsWith("第") && line.Contains("场");
        }

        /// <summary>识别「角色：台词」结构；返回发言者，不是对白行则返回 null。</summary>
        private static string TryGetSpeaker(string line)
        {
            int colon = line.IndexOfAny(new[] { '：', ':' });
            if (colon <= 0 || colon >= line.Length - 1) return null;

            string speaker = line.Substring(0, colon).Trim();
            string dialogue = line.Substring(colon + 1).Trim();
            if (speaker.Length == 0 || speaker.Length > 12 || dialogue.Length == 0) return null;

            foreach (char c in speaker)
            {
                if (char.IsWhiteSpace(c) || c == '，' || c == '。' || c == '（' || c == '）' || c == '、')
                    return null;
            }

            return speaker;
        }

        private static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max);
        }
    }
}
