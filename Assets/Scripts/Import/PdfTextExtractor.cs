using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ShotField.Import
{
    /// <summary>
    /// PDF 文本层最小提取器（纯 .NET 内置库，无第三方包）。
    /// 支持：未压缩 / FlateDecode 流；Type0 字体的 ToUnicode bfchar 映射；Tj / TJ 文本操作符。
    /// 不支持：bfrange 区间、加密 PDF、ToUnicode 缺失时的 CID 反查。
    /// 提取不到文本时返回空串，由 <see cref="PdfImporter"/> 判定为「需要 OCR」，绝不静默当成功。
    /// </summary>
    internal static class PdfTextExtractor
    {
        private static readonly Regex ObjectRegex = new Regex(@"(\d+)\s+(\d+)\s+obj(.*?)endobj", RegexOptions.Singleline);
        private static readonly Regex TounicodeRef = new Regex(@"/ToUnicode\s+(\d+)\s+0\s+R");
        private static readonly Regex ContentsRef = new Regex(@"/Contents\s+(\d+)\s+0\s+R");
        private static readonly Regex ContentsArray = new Regex(@"/Contents\s*\[(.*?)\]", RegexOptions.Singleline);
        private static readonly Regex RefNum = new Regex(@"(\d+)\s+0\s+R");
        private static readonly Regex BfcharBlock = new Regex(@"beginbfchar(.*?)endbfchar", RegexOptions.Singleline);
        private static readonly Regex BfcharPair = new Regex("<([0-9A-Fa-f]+)>\\s*<([0-9A-Fa-f]+)>");
        private static readonly Regex BtBlock = new Regex(@"BT(.*?)ET", RegexOptions.Singleline);

        public static string Extract(byte[] pdf)
        {
            if (pdf == null || pdf.Length == 0) return string.Empty;

            var bodies = new Dictionary<int, string>();
            var rawStreams = new Dictionary<int, byte[]>();
            ParseObjects(pdf, bodies, rawStreams);

            var cmap = BuildCmap(bodies, rawStreams);
            var contentStreams = CollectContentStreams(bodies, rawStreams);

            var lines = new List<string>();
            foreach (var content in contentStreams)
            {
                foreach (Match bt in BtBlock.Matches(BytesToLatin(content)))
                {
                    string line = ExtractBlockText(LatinToBytes(bt.Groups[1].Value), cmap);
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
                }
            }

            return string.Join("\n", lines);
        }

        private static void ParseObjects(byte[] pdf, Dictionary<int, string> bodies, Dictionary<int, byte[]> streams)
        {
            string all = BytesToLatin(pdf);
            foreach (Match m in ObjectRegex.Matches(all))
            {
                int num = int.Parse(m.Groups[1].Value);
                string body = m.Groups[3].Value;
                bodies[num] = body;

                var sm = Regex.Match(body, "stream\r?\n(.*?)\r?\nendstream", RegexOptions.Singleline);
                if (sm.Success)
                {
                    byte[] raw = LatinToBytes(sm.Groups[1].Value);
                    streams[num] = IsFlate(body) ? Inflate(raw) : raw;
                }
            }
        }

        private static Dictionary<int, string> BuildCmap(Dictionary<int, string> bodies, Dictionary<int, byte[]> streams)
        {
            var map = new Dictionary<int, string>();
            foreach (var body in bodies.Values)
            {
                Match tm = TounicodeRef.Match(body);
                if (!tm.Success || !int.TryParse(tm.Groups[1].Value, out int refNum)) continue;
                if (!streams.TryGetValue(refNum, out byte[] raw)) continue;

                string cmapText = BytesToLatin(raw);
                foreach (Match block in BfcharBlock.Matches(cmapText))
                {
                    foreach (Match pair in BfcharPair.Matches(block.Groups[1].Value))
                    {
                        int code = Convert.ToInt32(pair.Groups[1].Value, 16);
                        string dst = pair.Groups[2].Value;
                        var chars = new StringBuilder();
                        for (int i = 0; i + 4 <= dst.Length; i += 4)
                            chars.Append((char)Convert.ToInt32(dst.Substring(i, 4), 16));
                        map[code] = chars.ToString();
                    }
                }
            }
            return map;
        }

        private static List<byte[]> CollectContentStreams(Dictionary<int, string> bodies, Dictionary<int, byte[]> streams)
        {
            var result = new List<byte[]>();
            foreach (var body in bodies.Values)
            {
                if (!body.Contains("/Type /Page") && !body.Contains("/Type/Page")) continue;

                Match single = ContentsRef.Match(body);
                if (single.Success && int.TryParse(single.Groups[1].Value, out int n) && streams.TryGetValue(n, out byte[] s))
                    result.Add(s);

                Match arr = ContentsArray.Match(body);
                if (arr.Success)
                {
                    foreach (Match r in RefNum.Matches(arr.Groups[1].Value))
                    {
                        if (int.TryParse(r.Groups[1].Value, out int id) && streams.TryGetValue(id, out byte[] cs))
                            result.Add(cs);
                    }
                }
            }
            return result;
        }

        private static string ExtractBlockText(byte[] block, Dictionary<int, string> cmap)
        {
            var strings = PdfStringReader.ScanTextStrings(block);
            var sb = new StringBuilder();
            foreach (var raw in strings) sb.Append(Decode(raw, cmap));
            return sb.ToString();
        }

        private static string Decode(byte[] raw, Dictionary<int, string> cmap)
        {
            if (cmap.Count > 0 && raw.Length % 2 == 0)
            {
                var sb = new StringBuilder();
                for (int k = 0; k < raw.Length; k += 2)
                {
                    int code = (raw[k] << 8) | raw[k + 1];
                    sb.Append(cmap.TryGetValue(code, out string s) ? s : "�");
                }
                return sb.ToString();
            }
            return BytesToLatin(raw);
        }

        private static bool IsFlate(string body) => body.Contains("/FlateDecode");

        private static byte[] Inflate(byte[] raw)
        {
            // .NET DeflateStream 要裸 deflate；zlib 包装头两字节 0x78 需剥离。
            int offset = raw.Length >= 2 && raw[0] == 0x78 ? 2 : 0;
            using var input = new MemoryStream(raw, offset, raw.Length - offset);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        // ISO-8859-1 逐字节映射，避免依赖运行时是否注册 Latin1 代码页。
        private static string BytesToLatin(byte[] bytes)
        {
            var chars = new char[bytes.Length];
            for (int k = 0; k < bytes.Length; k++) chars[k] = (char)bytes[k];
            return new string(chars);
        }

        private static byte[] LatinToBytes(string text)
        {
            var bytes = new byte[text.Length];
            for (int k = 0; k < text.Length; k++) bytes[k] = (byte)text[k];
            return bytes;
        }
    }
}
