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
    }


}
