using System;
using System.Collections.Generic;
using System.Text;

namespace ShotField.Import
{
    /// <summary>
    /// PDF 内容流里的文本串读取：字面串 <c>(...)</c>（含转义与嵌套括号）与十六进制串 <c>&lt;...&gt;</c>。
    /// 从 <see cref="PdfTextExtractor"/> 拆出，控制单文件行数。
    /// </summary>
    internal static class PdfStringReader
    {
        public static List<byte[]> ScanTextStrings(byte[] content)
        {
            var list = new List<byte[]>();
            int i = 0;
            while (i < content.Length)
            {
                byte c = content[i];
                if (c == '(')
                {
                    var literal = ReadLiteral(content, ref i);
                    if (literal != null) list.Add(literal);
                }
                else if (c == '<')
                {
                    var hex = ReadHex(content, ref i);
                    if (hex != null) list.Add(hex);
                }
                else
                {
                    i++;
                }
            }
            return list;
        }

        private static byte[] ReadLiteral(byte[] content, ref int i)
        {
            i++; // 跳过 '('
            var buf = new List<byte>();
            int depth = 1;
            while (i < content.Length && depth > 0)
            {
                byte c = content[i];
                if (c == '\\')
                {
                    i++;
                    buf.Add(DecodeEscape(content, ref i));
                }
                else if (c == '(') { depth++; buf.Add(c); i++; }
                else if (c == ')') { depth--; if (depth > 0) buf.Add(c); i++; }
                else { buf.Add(c); i++; }
            }
            return buf.ToArray();
        }

        private static byte DecodeEscape(byte[] content, ref int i)
        {
            if (i >= content.Length) return 0;
            char e = (char)content[i];
            switch (e)
            {
                case 'n': i++; return 10;
                case 'r': i++; return 13;
                case 't': i++; return 9;
                case 'b': i++; return 8;
                case 'f': i++; return 12;
                case '(': i++; return 40;
                case ')': i++; return 41;
                case '\\': i++; return 92;
                case '0': case '1': case '2': case '3': case '4':
                case '5': case '6': case '7':
                    int oct = 0;
                    for (int k = 0; k < 3 && i < content.Length && content[i] >= '0' && content[i] <= '7'; k++, i++)
                        oct = oct * 8 + (content[i] - '0');
                    return (byte)oct;
                default: i++; return (byte)e;
            }
        }

        private static byte[] ReadHex(byte[] content, ref int i)
        {
            i++; // 跳过 '<'
            var hex = new StringBuilder();
            while (i < content.Length && content[i] != '>')
            {
                char c = (char)content[i];
                if (!char.IsWhiteSpace(c)) hex.Append(c);
                i++;
            }
            i++; // 跳过 '>'
            string h = hex.ToString();
            if (h.Length % 2 == 1) h += "0";
            var bytes = new byte[h.Length / 2];
            for (int k = 0; k < bytes.Length; k++)
                bytes[k] = Convert.ToByte(h.Substring(k * 2, 2), 16);
            return bytes;
        }
    }
}
