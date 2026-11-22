using System;
using System.Collections.Generic;
using System.IO;

namespace ShotField.Import
{
    /// <summary>
    /// 路由层：按扩展名选导入器，做大小与格式前置校验。
    /// 本层不接触网络与 AI；返回 <see cref="ScriptImportStatus.PendingConfirmation"/> 即「待用户确认」，
    /// 上游必须经过预览 / 修正 / 确认环节后，才允许调用 DeepSeek。
    /// </summary>
    public sealed class ScriptImporterRouter
    {
        /// <summary>单文件大小上限 25MB，超出直接拒绝，避免大文件拖垮预览。</summary>
        public const long MaxFileBytes = 25L * 1024 * 1024;

        private readonly List<IScriptDocumentImporter> _importers = new List<IScriptDocumentImporter>
        {
            new PlainTextImporter(),
            new MarkdownImporter(),
            new DocxImporter(),
            new PdfImporter()
        };

        public IReadOnlyList<IScriptDocumentImporter> Importers => _importers;

        public ScriptImportResult Import(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, "未提供文件路径。", path);

            if (!File.Exists(path))
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"文件不存在：{path}", path);

            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase))
                return ScriptImportResult.Fail(ScriptImportStatus.UnsupportedFormat,
                    "老二进制 .doc 格式不支持导入，请在 Word/WPS 里另存为 .docx 后再选。", path);

            var size = new FileInfo(path).Length;
            if (size > MaxFileBytes)
                return ScriptImportResult.Fail(ScriptImportStatus.Failed,
                    $"文件 {size / 1024 / 1024}MB 超过上限 25MB。", path);

            foreach (var importer in _importers)
            {
                if (importer.CanImport(path)) return importer.Import(path);
            }

            return ScriptImportResult.Fail(ScriptImportStatus.UnsupportedFormat,
                $"不支持的格式「{ext}」；支持 .txt / .md / .docx / .pdf（文本层）。", path);
        }
    }
}
