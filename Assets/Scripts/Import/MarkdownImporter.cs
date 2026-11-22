using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShotField.Import
{
    /// <summary>
    /// Markdown .md 导入器：先按纯文本编码探测读取，再剥离 md 符号（标题井号、加粗/斜体、
    /// 引用、列表符、代码围栏、行内代码、链接），保留标题、对白与段落正文。
    /// </summary>
    public sealed class MarkdownImporter : IScriptDocumentImporter
    {
        private static readonly Regex Link = new Regex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex Heading = new Regex(@"^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
        private static readonly Regex Quote = new Regex(@"^\s*>\s?", RegexOptions.Compiled);
        private static readonly Regex ListMarker = new Regex(@"^\s*[-*+]\s+", RegexOptions.Compiled);
        private static readonly Regex Bold = new Regex(@"\*\*|__", RegexOptions.Compiled);
        private static readonly Regex InlineCode = new Regex("`", RegexOptions.Compiled);
        private static readonly Regex Strikethrough = new Regex(@"~~", RegexOptions.Compiled);

        public bool CanImport(string path)
        {
            return PlainTextImporter.HasExtension(path, ".md");
        }

        public ScriptImportResult Import(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, "文件不存在或路径为空。", path);

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"读取文件失败：{ex.Message}", path);
            }

            string raw;
            string encoding;
            try
            {
                (raw, encoding) = PlainTextImporter.Decode(bytes);
            }
            catch (Exception ex)
            {
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"编码探测失败：{ex.Message}", path);
            }

            string plain = StripMarkdown(raw);
            return ScriptTextAnalyzer.Analyze(plain, path, encoding);
        }

        internal static string StripMarkdown(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var srcLines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var outLines = new StringBuilder();
            bool inFence = false;

            foreach (var src in srcLines)
            {
                string line = src;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }
                if (inFence) continue;

                line = Heading.Replace(line, string.Empty);
                line = Quote.Replace(line, string.Empty);
                line = ListMarker.Replace(line, string.Empty);
                line = Link.Replace(line, "$1");
                line = Bold.Replace(line, string.Empty);
                line = Strikethrough.Replace(line, string.Empty);
                line = InlineCode.Replace(line, string.Empty);
                line = line.Replace('*', ' ').Replace('_', ' ');
                line = line.Trim();

                if (line.Length > 0) outLines.AppendLine(line);
            }

            return outLines.ToString().Trim();
        }
    }
}
