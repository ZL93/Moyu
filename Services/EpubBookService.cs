using Moyu.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using VersOne.Epub;

namespace Moyu.Services
{
    public class EpubBookService : BaseBookService
    {
        private EpubBook _epubBook;

        public override BookInfo GetBookInfo(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new System.IO.FileNotFoundException($"文件不存在: {filePath}");
            }

            try
            {
                var book = EpubReader.ReadBook(filePath);
                return new BookInfo
                {
                    Title = book.Title ?? "未知标题",
                    Author = book.AuthorList != null && book.AuthorList.Any()
                        ? string.Join(", ", book.AuthorList)
                        : "未知作者",
                    FilePath = filePath,
                    Format = BookFormatEnum.Epub,
                    Introduction = book.Description
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"读取EPUB文件失败: {ex.Message}", ex);
            }
        }

        public override void LoadBook(BookInfo book)
        {
            base.LoadBook(book);

            try
            {
                _epubBook = EpubReader.ReadBook(book.FilePath);
                InitializeEpubChapters();

                // 加载当前章节内容
                if (CurrentBook.CurrentChapterIndex >= 0 && CurrentBook.CurrentChapterIndex < Chapters.Count)
                {
                    LoadChapterContent(CurrentBook.CurrentChapterIndex);
                }
                else if (Chapters.Count > 0)
                {
                    LoadChapterContent(0);
                    CurrentBook.CurrentChapterIndex = 0;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载EPUB书籍失败: {ex.Message}", ex);
            }
        }

        private void InitializeEpubChapters()
        {
            Chapters.Clear();

            if (_epubBook?.Navigation == null)
            {
                return;
            }

            int index = 0;
            foreach (var navItem in _epubBook.Navigation)
            {
                Chapters.Add(new ChapterInfo
                {
                    Title = navItem.Title ?? $"章节 {index + 1}",
                    ChapterIndex = index,
                    // EPUB章节没有LineIndex，使用ChapterIndex
                    LineIndex = index
                });
                index++;
            }

            // 如果没有导航，使用阅读顺序
            if (Chapters.Count == 0 && _epubBook?.ReadingOrder != null)
            {
                for (int i = 0; i < _epubBook.ReadingOrder.Count; i++)
                {
                    Chapters.Add(new ChapterInfo
                    {
                        Title = $"章节 {i + 1}",
                        ChapterIndex = i,
                        LineIndex = i
                    });
                }
            }
        }

        protected override void LoadChapterContentInternal(int chapterIndex)
        {
            if (_epubBook == null || chapterIndex < 0)
            {
                return;
            }

            try
            {
                // 获取章节内容
                var readingOrder = _epubBook.ReadingOrder;
                if (chapterIndex >= readingOrder.Count)
                {
                    WrappedLines = new List<string> { "章节不存在" };
                    return;
                }

                var chapterFile = readingOrder[chapterIndex];
                string htmlContent = chapterFile.Content;

                // 转换HTML为纯文本
                string plainText = HtmlToPlainText(htmlContent);

                // 包装文本
                int width = Console.WindowWidth;
                WrappedLines = WrapText(plainText, width);

                // 确保有内容
                if (WrappedLines.Count == 0)
                {
                    WrappedLines.Add("本章节暂无内容");
                }
            }
            catch (Exception ex)
            {
                WrappedLines = new List<string> { $"加载章节失败: {ex.Message}" };
            }
        }

        protected override void InitializeChapters()
        {
            // 章节已经在LoadBook中初始化
        }

        private string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            // 移除脚本和样式
            html = Regex.Replace(html, @"<script[^>]*?>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*?>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);

            // 替换换行标签
            html = Regex.Replace(html, @"<(br|p|div|h\d)[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(p|div|h\d)[^>]*>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<head[^>]*>.*?</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            // 移除所有HTML标签
            html = Regex.Replace(html, @"<[^>]+>", string.Empty);

            // 解码HTML实体
            html = WebUtility.HtmlDecode(html);

            // 清理多余的空格和换行
            html = Regex.Replace(html, @"^(\s*\n)+", "");
            html = Regex.Replace(html, @"(\n\s*)+$", "");

            return html.Trim();
        }

        protected override int GetCharDisplayWidth(char c)
        {
            // EPUB可能需要特殊处理某些字符
            return base.GetCharDisplayWidth(c);
        }
    }
}