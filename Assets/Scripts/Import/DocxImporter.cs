using System;
using System.IO;
using System.Text;
using System.Xml;
using System.IO.Compression;

namespace ShotField.Import
{
    /// <summary>
    /// .docx 导入器：docx 本质是 zip，解 <c>word/document.xml</c>，
    /// 按 <c>w:p</c> 段落 / <c>w:t</c> 文本节点提取。仅用 .NET 内置 System.IO.Compression，不引第三方包。
    /// 老二进制 .doc 不在支持范围，由路由层明确提示另存为 .docx。
    /// </summary>
    public sealed class DocxImporter : IScriptDocumentImporter
    {
        private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private const string DocumentEntry = "word/document.xml";

        public bool CanImport(string path)
        {
            return PlainTextImporter.HasExtension(path, ".docx");
        }

        public ScriptImportResult Import(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, "文件不存在或路径为空。", path);

            string plain;
            try
            {
                plain = ExtractDocumentXml(path);
            }
            catch (Exception ex)
            {
                return ScriptImportResult.Fail(ScriptImportStatus.Failed,
                    $"解析 .docx 失败：{ex.Message}（若为老 .doc 二进制格式，请另存为 .docx）", path);
            }

            return ScriptTextAnalyzer.Analyze(plain, path, "DOCX(zip/XML)");
        }

        private static string ExtractDocumentXml(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(DocumentEntry);
            if (entry == null)
                throw new InvalidDataException("压缩包内缺少 word/document.xml，不是有效的 .docx 文件。");

            using var stream = entry.Open();
            var doc = new XmlDocument();
            doc.Load(stream);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("w", WordNs);

            var paragraphs = doc.SelectNodes("//w:p", ns);
            var sb = new StringBuilder();
            if (paragraphs == null) return string.Empty;

            foreach (XmlNode p in paragraphs)
            {
                var texts = p.SelectNodes(".//w:t", ns);
                if (texts == null) continue;

                var line = new StringBuilder();
                foreach (XmlNode t in texts) line.Append(t.InnerText);
                if (line.Length > 0) sb.AppendLine(line.ToString());
            }

            return sb.ToString().Trim();
        }
    }
}
