using System;
using System.IO;
using System.Text;

namespace ShotField.Import
{
    /// <summary>
    /// 纯文本 .txt 导入器。编码探测顺序：UTF-8 BOM → UTF-16 BOM → 严格 UTF-8 → GB18030。
    /// Android / IL2CPP 下代码页 936 可能未注册，探测失败时回落 UTF-8 并在 EncodingDetected 标注，不猜乱码。
    /// </summary>
    public sealed class PlainTextImporter : IScriptDocumentImporter
    {
        public bool CanImport(string path)
        {
            return HasExtension(path, ".txt");
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

            if (bytes.Length == 0)
                return ScriptImportResult.Fail(ScriptImportStatus.EmptyText, "文件大小为 0 字节。", path);

            string text;
            string encoding;
            try
            {
                (text, encoding) = Decode(bytes);
            }
            catch (Exception ex)
            {
                return ScriptImportResult.Fail(ScriptImportStatus.Failed, $"编码探测失败：{ex.Message}", path);
            }

            return ScriptTextAnalyzer.Analyze(text, path, encoding);
        }

        internal static (string text, string encoding) Decode(byte[] bytes)
        {
            // UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8 BOM");

            // UTF-16 LE / BE BOM（防御性支持，正文仍是纯文本）
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16 LE");
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16 BE");

            // 严格 UTF-8：有非法字节就抛，交给 GB18030 回落
            try
            {
                var strict = new UTF8Encoding(false, true);
                return (strict.GetString(bytes), "UTF-8");
            }
            catch (DecoderFallbackException)
            {
                // 继续走 GB18030
            }

            try
            {
                var gb = Encoding.GetEncoding(936);
                return (gb.GetString(bytes), "GB18030");
            }
            catch (ArgumentException)
            {
                // Android/IL2CPP 未注册 936：回落 UTF-8，标注未知，避免静默乱码
                return (Encoding.UTF8.GetString(bytes), "未知(回落UTF-8)");
            }
        }

        internal static bool HasExtension(string path, string ext)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }
    }
}
