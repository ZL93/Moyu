using Moyu.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VersOne.Epub;

namespace Moyu.Services
{
    /// <summary>
    /// EPUB书籍服务 - 单章加载模式
    /// </summary>
    public class EpubBookService : ChapterBasedBookService, IDisposable
    {
        private EpubBook _epubBook;
        private string _filePath;

        public override BookInfo GetBookInfo(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException($"文件不存在: {filePath}");

            var book = EpubReader.ReadBook(filePath);
            return new BookInfo
            {
                Title = book.Title ?? "未知标题",
                Author = book.AuthorList != null && book.AuthorList.Any()
                    ? string.Join(", ", book.AuthorList)
                    : "未知作者",
                FilePath = filePath,
                Format = BookFormatEnum.Epub,
                Introduction = book.Description,
                CurrentReadOriginalLine = 0,
                CurrentChapterIndex = 0
            };
        }

        public override async Task<BookInfo> GetBookInfoAsync(string filePath)
            => await Task.Run(() => GetBookInfo(filePath));

        public override void LoadBook(BookInfo book)
        {
            _filePath = book.FilePath;
            _epubBook = EpubReader.ReadBook(_filePath);

            base.LoadBook(book);
        }

        protected override Task<List<ChapterInfo>> LoadChapterListAsync()
        {
            var chapters = new List<ChapterInfo>();

            if (_epubBook?.ReadingOrder == null || _epubBook.ReadingOrder.Count == 0)
                return Task.FromResult(chapters);

            for (int i = 0; i < _epubBook.ReadingOrder.Count; i++)
            {
                string title = GetChapterTitle(i) ?? $"第 {i + 1} 章";

                chapters.Add(new ChapterInfo
                {
                    Title = title,
                    ChapterIndex = i,
                    LineIndex = 0  // EPUB使用章节内行索引
                });

                ChapterTitleCache[i] = title;
            }

            return Task.FromResult(chapters);
        }

        protected override Task<List<string>> LoadChapterContentAsync(int chapterIndex)
        {
            if (_epubBook == null || chapterIndex < 0 || chapterIndex >= _epubBook.ReadingOrder.Count)
                return Task.FromResult(new List<string> { "章节不存在" });

            try
            {
                var chapterFile = _epubBook.ReadingOrder[chapterIndex];
                string htmlContent = chapterFile.Content;
                string plainText = HtmlToPlainText(htmlContent);

                var lines = plainText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                return Task.FromResult(lines.Count == 0
                    ? new List<string> { "本章节暂无内容" }
                    : lines);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new List<string> { $"加载章节失败: {ex.Message}" });
            }
        }

        protected override void UpdateProgress()
        {
            if (CurrentBook == null || Chapters.Count == 0) return;
            CurrentBook.MarkProgress = (float)CurrentChapterIndex / Chapters.Count;
        }

        private string GetChapterTitle(int chapterIndex)
        {
            try
            {
                if (_epubBook?.Navigation != null && chapterIndex < _epubBook.Navigation.Count)
                {
                    var navItem = _epubBook.Navigation[chapterIndex];
                    if (!string.IsNullOrEmpty(navItem?.Title))
                    {
                        return navItem.Title;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            return null;
        }

        private string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            html = Regex.Replace(html, @"<script[^>]*?>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*?>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(br|p|div|h\d|tr|li)[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]+>", "");
            html = System.Net.WebUtility.HtmlDecode(html);
            html = Regex.Replace(html, @"\n\s*\n", "\n");
            html = Regex.Replace(html, @"[ \t]+", " ");

            return html.Trim();
        }

        public void Dispose()
        {
            _epubBook = null;
        }
    }
}