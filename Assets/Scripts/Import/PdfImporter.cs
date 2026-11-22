using System;
using System.IO;

namespace ShotField.Import
{
    /// <summary>
    /// .pdf 导入器（仅文本层）。有文本层 → 提取纯文本走结构化统计；
    /// 无文本层（扫描件 / 图片版）→ 明确返回 <see cref="ScriptImportStatus.NeedsOcr"/>，归 Labs，需要 OCR，
    /// 绝不把空文本当成功。加密 PDF 直接报失败，不尝试解密。
    /// </summary>
    public sealed class PdfImporter : IScriptDocumentImporter
    {
        public bool CanImport(string path)
        {
            return PlainTextImporter.HasExtension(path, ".pdf");
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
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"读取 PDF 失败：{ex.Message}", path);
            }

            if (bytes.Length == 0)
                return ScriptImportResult.Fail(ScriptImportStatus.EmptyText, "PDF 文件大小为 0 字节。", path);

            if (LooksEncrypted(bytes))
                return ScriptImportResult.Fail(ScriptImportStatus.Failed,
                    "PDF 已加密，当前导入层不支持解密，请先解除口令。", path);

            string plain;
            try
            {
                plain = PdfTextExtractor.Extract(bytes);
            }
            catch (Exception ex)
            {
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"PDF 文本层解析失败：{ex.Message}", path);
            }

            if (string.IsNullOrWhiteSpace(plain))
            {
                return ScriptImportResult.Fail(ScriptImportStatus.NeedsOcr,
                    "未检测到可用文本层（可能为扫描件或图片版 PDF），需要 OCR 后再导入（Labs 能力）。", path);
            }

            return ScriptTextAnalyzer.Analyze(plain, path, "PDF(文本层)");
        }

        private static bool LooksEncrypted(byte[] bytes)
        {
            // 粗扫 trailer / 字典中的 /Encrypt 标记；不做完整语法解析。
            for (int i = 0; i + 8 <= bytes.Length; i++)
            {
                if (bytes[i] == '/' && bytes[i + 1] == 'E' && bytes[i + 2] == 'n' &&
                    bytes[i + 3] == 'c' && bytes[i + 4] == 'r' && bytes[i + 5] == 'y' &&
                    bytes[i + 6] == 'p' && bytes[i + 7] == 't')
                    return true;
            }
            return false;
        }
    }
}
