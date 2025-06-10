using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Moyu.Utils
{
    public static class TextFileReader
    {
        private static readonly Encoding[] EncodingsToTry;

        static TextFileReader()
        {
            EncodingsToTry = new Encoding[]
            {
                Encoding.UTF8,
                Encoding.GetEncoding("GB2312"),
                Encoding.Unicode,
                Encoding.BigEndianUnicode,
                Encoding.Default
            };
        }

        public static List<string> ReadTextFileLines(string filePath)
        {
            foreach (var encoding in EncodingsToTry)
            {
                try
                {
                    using (var reader = new StreamReader(filePath, encoding, detectEncodingFromByteOrderMarks: true))
                    {
                        var lines = new List<string>();
                        int readCount = 0;
                        bool isValid = true;

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lines.Add(line);
                            readCount++;

                            // 提前检测有效性（仅检测前100行）
                            if (readCount == 100)
                            {
                                if (!IsTextValid(lines))
                                {
                                    isValid = false;
                                    break;
                                }
                            }
                        }

                        // 如果不足100行，仍需判断
                        if (readCount < 100 && !IsTextValid(lines))
                        {
                            isValid = false;
                        }

                        if (isValid && lines.Count > 0)
                        {
                            return lines;
                        }
                    }
                }
                catch
                {
                    // 忽略错误，尝试下一个编码
                }
            }

            throw new Exception($"无法读取文件：{filePath}，尝试了多种编码均失败。");
        }



        // 简单判断文本是否“合理”，主要排除大量�字符和不可打印字符
        private static bool IsTextValid(List<string> lines)
        {
            int totalChars = 0;
            int invalidChars = 0;

            foreach (var line in lines)
            {
                foreach (var ch in line)
                {
                    totalChars++;
                    if (IsInvalidChar(ch))
                        invalidChars++;
                }
            }
            if (totalChars == 0) return false;

            double invalidRatio = (double)invalidChars / totalChars;
            // 如果乱码字符比例超过 5%，判为无效文本
            return invalidRatio < 0.05;
        }

        // 判断单个字符是否为“乱码”或不可打印字符
        private static bool IsInvalidChar(char ch)
        {
            // Unicode替换字符 � (U+FFFD)
            if (ch == '\uFFFD')
                return true;

            // 控制字符，除换行(\n)、回车(\r)、制表(\t)外视为无效
            if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
                return true;

            // 你可以根据需求添加更多规则，比如排除非常罕见的字符等

            return false;
        }

        public static int GetCharDisplayWidth(char c)
        {
            if (char.IsControl(c))
            {
                return 0;
            }

            if (c == '\t')
            {
                return 4;
            }

            var code = (int)c;
            if ((code >= 0x4E00 && code <= 0x9FFF) || (code >= 0xFF00 && code <= 0xFFEF) || (code >= 0x3000 && code <= 0x303F))
            {
                return 2;
            }

            return 1;
        }

        /// <summary>
        /// 截断中文书名（按显示宽度）
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxDisplayWidth"></param>
        /// <returns></returns>
        public static string Truncate(string text, int maxDisplayWidth)
        {
            int width = 0;
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                int w = GetCharDisplayWidth(ch);
                if (width + w > maxDisplayWidth)
                {
                    sb.Append("…");
                    break;
                }
                sb.Append(ch);
                width += w;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 补足显示宽度到指定宽度
        /// </summary>
        /// <param name="text"></param>
        /// <param name="totalDisplayWidth"></param>
        /// <returns></returns>
        public static string PadRightDisplay(string text, int totalDisplayWidth)
        {
            int currentWidth = GetDisplayWidth(text);
            return text + new string(' ', Math.Max(0, totalDisplayWidth - currentWidth));
        }

        /// <summary>
        /// 获取字符串显示宽度
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (var ch in text)
            {
                width += GetCharDisplayWidth(ch);
            }
            return width;
        }
    }


}
