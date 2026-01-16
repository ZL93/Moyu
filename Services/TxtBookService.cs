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
    public class TxtBookService : BaseBookService
    {
        private List<string> _originalLines = new List<string>();
        private List<int> _originalLineToWrappedLineMap = new List<int>();
        private List<int> _wrappedLineToOriginalLineMap = new List<int>();

        private bool _isLoaded = false;

        public override BookInfo GetBookInfo(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在: {filePath}");

            return new BookInfo
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath),
                Format = BookFormatEnum.Txt
            };
        }

        public override void LoadBook(BookInfo book)
        {
            base.LoadBook(book);

            // 读取文件内容
            _originalLines = TextFileReader.ReadTextFileLines(book.FilePath);
            _isLoaded = true;

            // 处理文本包装
            ProcessTextWrapping();

            // 自动检测章节
            if (AutoDetectChapters)
            {
                DetectChapters();
            }

            // 恢复阅读位置
            RestoreReadingPosition();
        }

        private void ProcessTextWrapping()
        {
            WrappedLines.Clear();
            _originalLineToWrappedLineMap.Clear();
            _wrappedLineToOriginalLineMap.Clear();

            int width = Console.WindowWidth;

            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i];
                _originalLineToWrappedLineMap.Add(WrappedLines.Count);

                if (string.IsNullOrWhiteSpace(line))
                {
                    WrappedLines.Add("");
                    _wrappedLineToOriginalLineMap.Add(i);
                    continue;
                }

                var sb = new StringBuilder();
                int currentWidth = 0;

                foreach (char c in line)
                {
                    int charWidth = GetCharDisplayWidth(c);
                    if (currentWidth + charWidth > width)
                    {
                        WrappedLines.Add(sb.ToString());
                        _wrappedLineToOriginalLineMap.Add(i);
                        sb.Clear();
                        currentWidth = 0;
                    }
                    sb.Append(c);
                    currentWidth += charWidth;
                }

                if (sb.Length > 0)
                {
                    WrappedLines.Add(sb.ToString());
                    _wrappedLineToOriginalLineMap.Add(i);
                }
            }
        }

        private void DetectChapters()
        {
            Chapters.Clear();

            var regex = new Regex(
                @"^(第[零一二三四五六七八九十百千万\d]+[章节回卷篇])|(^Chapter\s+\d+)|(^\d+\.)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase
            );

            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i].Trim();
                if (regex.IsMatch(line))
                {
                    Chapters.Add(new ChapterInfo
                    {
                        Title = line.Length > 50 ? line.Substring(0, 50) + "..." : line,
                        LineIndex = i,
                        ChapterIndex = Chapters.Count
                    });
                }
            }

            // 如果没有检测到章节，将整个文件作为一个章节
            if (Chapters.Count == 0 && _originalLines.Count > 0)
            {
                Chapters.Add(new ChapterInfo
                {
                    Title = "全文",
                    LineIndex = 0,
                    ChapterIndex = 0
                });
            }
        }

        private void RestoreReadingPosition()
        {
            if (CurrentBook == null) return;

            // 如果有书签位置，跳转到书签
            if (CurrentBook.MarkLoc >= 0 && CurrentBook.MarkLoc < _originalLineToWrappedLineMap.Count)
            {
                int wrappedLineIndex = _originalLineToWrappedLineMap[CurrentBook.MarkLoc];
                CurrentBook.CurrentReadChapterLine = wrappedLineIndex;
            }
            // 否则跳转到当前章节
            else if (CurrentBook.CurrentChapterIndex >= 0 && CurrentBook.CurrentChapterIndex < Chapters.Count)
            {
                var chapter = Chapters[CurrentBook.CurrentChapterIndex];
                if (chapter.LineIndex < _originalLineToWrappedLineMap.Count)
                {
                    int wrappedLineIndex = _originalLineToWrappedLineMap[chapter.LineIndex];
                    CurrentBook.CurrentReadChapterLine = wrappedLineIndex;
                }
            }

            UpdateProgress();
        }

        protected override void LoadChapterContentInternal(int chapterIndex)
        {
            // TXT格式不需要特殊加载，因为所有内容已经加载
            // 这里主要是确保章节索引正确
            if (chapterIndex >= 0 && chapterIndex < Chapters.Count)
            {
                var chapter = Chapters[chapterIndex];
                if (chapter.LineIndex < _originalLineToWrappedLineMap.Count)
                {
                    int wrappedLineIndex = _originalLineToWrappedLineMap[chapter.LineIndex];

                    // 确保当前行在包装后的行范围内
                    if (CurrentBook.CurrentReadChapterLine >= WrappedLines.Count)
                    {
                        CurrentBook.CurrentReadChapterLine = wrappedLineIndex;
                    }
                }
            }
        }

        protected override void InitializeChapters()
        {
            // 章节已经在LoadBook中初始化
        }

        public override void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (!_isLoaded) return;

            base.JumpToLineInChapter(chapterIndex, lineOffset);

            // 更新书签位置
            if (CurrentBook.CurrentReadChapterLine < _wrappedLineToOriginalLineMap.Count)
            {
                CurrentBook.MarkLoc = _wrappedLineToOriginalLineMap[CurrentBook.CurrentReadChapterLine];
            }
        }

        public override void NextPage()
        {
            base.NextPage();
            UpdateBookmark();
        }

        public override void PrevPage()
        {
            base.PrevPage();
            UpdateBookmark();
        }

        public override void NextLine()
        {
            base.NextLine();
            UpdateBookmark();
        }

        private void UpdateBookmark()
        {
            if (CurrentBook == null || CurrentBook.CurrentReadChapterLine >= _wrappedLineToOriginalLineMap.Count)
                return;

            CurrentBook.MarkLoc = _wrappedLineToOriginalLineMap[CurrentBook.CurrentReadChapterLine];
            CurrentBook.MarkProgress = (float)CurrentBook.MarkLoc / _originalLines.Count;
        }

        private int FindCurrentChapterIndex(int lineIndex)
        {
            if (Chapters.Count == 0) return -1;

            for (int i = Chapters.Count - 1; i >= 0; i--)
            {
                if (Chapters[i].LineIndex <= lineIndex)
                    return i;
            }

            return 0;
        }
    }
}