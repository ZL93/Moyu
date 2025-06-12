using Moyu.Models;
using Moyu.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Moyu.Services
{
    public class TxtBookService : IBookService
    {
        private List<string> _originalLines = new List<string>();
        private List<string> _wrappedLines = new List<string>();
        private List<int> _originalLineToWrappedLineMap = new List<int>();
        private List<int> _wrappedLineToOriginalLineMap = new List<int>();
        private List<ChapterInfo> _chapters = new List<ChapterInfo>();
        private int currentLineCount = 0;
        private int _pageSize = 10;
        private BookInfo currentBook;

        public BookInfo GetBookInfo(string filePath)
        {
            return new BookInfo
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath),
                Format = BookFormatEnum.Txt
            };
        }

        public void LoadBook(BookInfo book)
        {
            currentBook = book;
            _originalLines = TextFileReader.ReadTextFileLines(book.FilePath);
            _wrappedLines.Clear();
            _originalLineToWrappedLineMap.Clear();
            _wrappedLineToOriginalLineMap.Clear();

            int width = Console.WindowWidth;
            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i];
                _originalLineToWrappedLineMap.Add(_wrappedLines.Count);
                if (string.IsNullOrWhiteSpace(line))
                {
                    _wrappedLines.Add("");
                    _wrappedLineToOriginalLineMap.Add(i);
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
                        _wrappedLineToOriginalLineMap.Add(i);
                        sb.Clear();
                        currentWidth = 0;
                    }
                    sb.Append(c);
                    currentWidth += charWidth;
                }
                if (sb.Length > 0)
                {
                    _wrappedLines.Add(sb.ToString());
                    _wrappedLineToOriginalLineMap.Add(i);
                }
            }

            currentLineCount = book.MarkLoc >= 0 && book.MarkLoc < _originalLineToWrappedLineMap.Count
                ? _originalLineToWrappedLineMap[book.MarkLoc] : 0;

            DetectChapters();
        }

        public void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (chapterIndex < 0 || chapterIndex >= _chapters.Count)
            {
                return;
            }

            int target = _chapters[chapterIndex].LineIndex + lineOffset;
            JumpToLine(target);
        }

        public void NextPage()
        {
            currentLineCount += _pageSize;
            if (currentLineCount >= _wrappedLines.Count)
            {
                currentLineCount = _wrappedLines.Count - 1;
            }

            currentBook.MarkLoc = _wrappedLineToOriginalLineMap[currentLineCount];
            currentBook.MarkProgress = (float)currentBook.MarkLoc / _originalLines.Count;
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentChapterIndex = FindCurrentChapterIndex(currentBook.MarkLoc);
        }

        public void PrevPage()
        {
            currentLineCount = Math.Max(0, currentLineCount - _pageSize);

            currentBook.MarkLoc = _wrappedLineToOriginalLineMap[currentLineCount];
            currentBook.MarkProgress = (float)currentBook.MarkLoc / _originalLines.Count;
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentChapterIndex = FindCurrentChapterIndex(currentBook.MarkLoc);
        }
        public void NextLine()
        {
            currentLineCount++;
            if (currentLineCount >= _wrappedLines.Count)
            {
                currentLineCount = _wrappedLines.Count - 1;
            }

            currentBook.MarkLoc = _wrappedLineToOriginalLineMap[currentLineCount];
            currentBook.MarkProgress = (float)currentBook.MarkLoc / _originalLines.Count;
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentChapterIndex = FindCurrentChapterIndex(currentBook.MarkLoc);
        }
        public int GetChaptersCount() => _chapters.Count;

        public List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            return _chapters.Skip(start).Take(end - start).ToList();
        }
        public string GetCurrentPage()
        {
            currentBook.LastReadTime = DateTime.Now;
            int startLine = currentLineCount;
            if (startLine >= _wrappedLines.Count)
            {
                return string.Empty;
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
            var pageLines = _wrappedLines.GetRange(startLine, count);

            return string.Join(Environment.NewLine, pageLines);
        }

        private void DetectChapters()
        {
            _chapters.Clear();
            var regex = new Regex(@"^(第[零一二三四五六七八九十百千万\d]+[章节回卷篇])|(^Chapter\s+\d+)", RegexOptions.Compiled);
            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i].Trim();
                if (regex.IsMatch(line))
                {
                    _chapters.Add(new ChapterInfo
                    {
                        Title = line.Length > 30 ? line.Substring(0, 30) + "..." : line,
                        LineIndex = i
                    });
                }
            }
        }
        private int FindCurrentChapterIndex(int lineIndex)
        {
            int idx = _chapters.FindLastIndex(c => c.LineIndex <= lineIndex);
            return idx < 0 ? 0 : idx;
        }
        private void JumpToLine(int lineIndex)
        {
            if (lineIndex >= _originalLineToWrappedLineMap.Count)
            {
                return;
            }

            currentLineCount = _originalLineToWrappedLineMap[lineIndex];
            currentBook.MarkLoc = lineIndex;
            currentBook.MarkProgress = (float)currentBook.MarkLoc / _originalLines.Count;
            currentBook.LastReadTime = DateTime.Now;
            currentBook.CurrentChapterIndex = FindCurrentChapterIndex(currentBook.MarkLoc);
        }
    }
}
