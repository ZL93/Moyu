using Moyu.Models;
using Moyu.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using VersOne.Epub;

namespace Moyu.Services
{
    public class EpubBookService : IBookService
    {
        private EpubBook epubBook;
        private BookInfo currentBook;
        private List<ChapterInfo> chapterList = new List<ChapterInfo>();
        private int _pageSize = 10;
        private int currentChapterIndex = -1;

        private List<string> _originalLines = new List<string>();
        private List<string> _wrappedLines = new List<string>();
       
        public BookInfo GetBookInfo(string filePath)
        {
            var book = EpubReader.ReadBook(filePath);
            return new BookInfo
            {
                Title = book.Title,
                Author = string.Join(", ", book.AuthorList),
                FilePath = filePath,
                Format =  BookFormatEnum.Epub
            };
        }
        public void LoadBook(BookInfo book) 
        {
            currentBook = book;
            epubBook = EpubReader.ReadBook(currentBook.FilePath);
            chapterList.Clear();
            int index = 0;
            foreach (var chapter in epubBook.Navigation)
            {
                chapterList.Add(new ChapterInfo
                {
                    Title = chapter.Title ?? $"章节 {index + 1}",
                    ChapterIndex = index
                });
                index++;
            }
            book.LastReadTime = DateTime.Now;
        }

        public List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            return chapterList.Skip(start).Take(end - start).ToList();
        }

        public void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            currentBook.CurrentChapterIndex = chapterIndex;
            currentBook.CurrentReadChapterLine = lineOffset;

            currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
        }

        public string[] GetCurrentPage()
        {
            currentBook.LastReadTime = DateTime.Now;
            GetCurrentChapter();
            int startLine = currentBook.CurrentReadChapterLine;
            if (startLine >= _wrappedLines.Count)
            {
                return new string[] { };
            }


            // 计算当前页的行数
            if (Config.Instance.ShowHelpInfo)
            {
                _pageSize = Console.WindowHeight - 5;
            }
            else
            {
                _pageSize = Console.WindowHeight - 1;
            }
            int count = Math.Min(_pageSize, _wrappedLines.Count - startLine);
            return _wrappedLines.GetRange(startLine, count).ToArray();

        }

        private void GetCurrentChapter()
        {
            if (currentChapterIndex == currentBook.CurrentChapterIndex)
            {
                return;
            }
            currentChapterIndex = currentBook.CurrentChapterIndex;
            _originalLines.Clear();
            _wrappedLines.Clear();
            var lines = HtmlToPlainText(epubBook.ReadingOrder[currentBook.CurrentChapterIndex].Content);
            foreach (var line in lines)
            {
                _originalLines.Add(line.Trim());
            }
            
            int width = Console.WindowWidth;
            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    _wrappedLines.Add("");
                    continue;
                }
                var sb = new StringBuilder();
                int currentWidth = 0;
                foreach (char c in line)
                {
                    int charWidth = TextFileReader.GetCharDisplayWidth(c);
                    if (currentWidth + charWidth > width)
                    {
                        _wrappedLines.Add(sb.ToString());
                        sb.Clear();
                        currentWidth = 0;
                    }
                    sb.Append(c);
                    currentWidth += charWidth;
                }
                if (sb.Length > 0)
                {
                    _wrappedLines.Add(sb.ToString());
                }
            }
        }


        public void NextPage()
        {
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentReadChapterLine += _pageSize;
            if (currentBook.CurrentReadChapterLine >= _wrappedLines.Count)
            {
                if (currentBook.CurrentChapterIndex < chapterList.Count - 1)
                {
                    currentBook.CurrentChapterIndex++;
                    currentBook.CurrentReadChapterLine = 0;
                    currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    GetCurrentChapter();
                }
                else
                {
                    currentBook.CurrentReadChapterLine = Math.Max(0, _wrappedLines.Count - _pageSize);
                }
            }
        }
        public void PrevPage() 
        {
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentReadChapterLine -= _pageSize;
            if (currentBook.CurrentReadChapterLine < 0)
            {
                if (currentBook.CurrentChapterIndex > 0)
                {
                    currentBook.CurrentChapterIndex--;
                    currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    GetCurrentChapter();
                    currentBook.CurrentReadChapterLine = Math.Max(_wrappedLines.Count - _pageSize, 0);
                }
                else
                {
                    currentBook.CurrentReadChapterLine = 0;
                }
            }
        }

        public void NextLine()
        {
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentReadChapterLine++;
            if (currentBook.CurrentReadChapterLine >= _wrappedLines.Count)
            {
                if (currentBook.CurrentChapterIndex < chapterList.Count - 1)
                {
                    currentBook.CurrentChapterIndex++;
                    currentBook.CurrentReadChapterLine = 0;
                    currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    GetCurrentChapter();
                }
                else
                {
                    currentBook.CurrentReadChapterLine = Math.Max(0, _wrappedLines.Count - _pageSize);
                }
            }
        }

        public int GetChaptersCount() => chapterList.Count;
       
        private string[] HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return new string[0];
            }

            // 1. 移除 script/style 标签及其内容
            html = Regex.Replace(html, @"<script[^>]*?>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*?>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<head[^>]*?>[\s\S]*?</head>", string.Empty, RegexOptions.IgnoreCase);

            // 2. 替换常用段落/换行标签为换行符（注意顺序）
            html = Regex.Replace(html, @"(<br\s*/?>|</p>|</div>|</h\d>)", "\n", RegexOptions.IgnoreCase);

            // 3. 移除所有其他 HTML 标签
            html = Regex.Replace(html, @"<[^>]+>", string.Empty, RegexOptions.IgnoreCase);

            // 4. 解码 HTML 实体
            html = System.Net.WebUtility.HtmlDecode(html);

            html = html.Trim();
            return html.Split('\n');
        }
    }
}
