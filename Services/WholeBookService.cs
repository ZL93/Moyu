using Moyu.Models;
using Moyu.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moyu.Services
{
    /// <summary>
    /// 整本加载书籍服务基类
    /// 适用于TXT等一次性加载全部内容的格式
    /// </summary>
    public abstract class WholeBookService : IBookService
    {
        protected BookInfo CurrentBook { get; private set; }
        protected List<ChapterInfo> Chapters { get; private set; } = new List<ChapterInfo>();

        // 整本内容
        protected List<string> OriginalLines { get; set; } = new List<string>();

        // 当前阅读位置
        private int _currentCharOffset = 0;
        private int _currentLineUsedWrappedCount = 0;

        // 添加 null 保护的属性
        protected int CurrentReadOriginalLine
        {
            get => CurrentBook?.CurrentReadOriginalLine ?? 0;
            set
            {
                if (CurrentBook != null)
                    CurrentBook.CurrentReadOriginalLine = value;
            }
        }

        protected int CurrentChapterIndex
        {
            get => CurrentBook?.CurrentChapterIndex ?? 0;
            set
            {
                if (CurrentBook != null)
                    CurrentBook.CurrentChapterIndex = value;
            }
        }

        protected virtual int DefaultPageSize => 10;
        public virtual bool AutoDetectChapters => true;

        #region 抽象方法
        public abstract BookInfo GetBookInfo(string filePath);
        public abstract Task<BookInfo> GetBookInfoAsync(string filePath);
        protected abstract void UpdateProgress();
        #endregion

        #region IBookService 实现
        public virtual void LoadBook(BookInfo book)
        {
            CurrentBook = book ?? throw new ArgumentNullException(nameof(book));

            // 恢复阅读位置
            _currentCharOffset = 0;
            _currentLineUsedWrappedCount = 0;

            CurrentBook.LastReadTime = DateTime.Now;
        }

        public virtual async Task LoadBookAsync(BookInfo book)
            => await Task.Run(() => LoadBook(book));

        public virtual string[] GetCurrentPage()
        {
            if (OriginalLines.Count == 0)
                return new[] { "内容加载中..." };

            int pageSize = CalculatePageSize();
            int windowWidth = GetConsoleWidth();

            var displayLines = GenerateDisplayLines(
                CurrentReadOriginalLine,
                _currentCharOffset,
                pageSize,
                windowWidth,
                out _,
                out _ 
            );

            return displayLines.ToArray();
        }

        public virtual async Task<string[]> GetCurrentPageAsync()
            => await Task.Run(() => GetCurrentPage());

        public virtual void NextPage()
        {
            if (OriginalLines.Count == 0) return;

            int pageSize = CalculatePageSize();
            int windowWidth = GetConsoleWidth();

            // 获取当前阅读位置的全局显示行索引
            int currentDisplayLine = GetGlobalDisplayLineIndex(
                CurrentReadOriginalLine,
                _currentCharOffset,
                windowWidth
            );

            // 目标显示行索引（下一页的开始）
            int targetDisplayLine = currentDisplayLine + pageSize;

            // ✅ 使用统一的查找方法
            var (targetOriginalLine, targetCharOffset) =
                FindOriginalPositionByDisplayLine(targetDisplayLine, windowWidth);

            // 检查是否到达文件末尾
            if (targetOriginalLine >= OriginalLines.Count)
            {
                HandleNextChapter();
                return;
            }

            // 更新阅读位置
            CurrentReadOriginalLine = targetOriginalLine;
            _currentCharOffset = targetCharOffset;
            _currentLineUsedWrappedCount = 0;

            CurrentBook.LastReadTime = DateTime.Now;
            UpdateProgress();
        }

        public virtual async Task NextPageAsync() => await Task.Run(() => NextPage());

        public virtual void PrevPage()
        {
            if (OriginalLines.Count == 0) return;

            int pageSize = CalculatePageSize();
            int windowWidth = GetConsoleWidth();

            // 获取当前阅读位置的全局显示行索引
            int currentDisplayLine = GetGlobalDisplayLineIndex(
                CurrentReadOriginalLine,
                _currentCharOffset,
                windowWidth
            );

            // 计算目标显示行索引（上一页的开始位置）
            int targetDisplayLine = Math.Max(0, currentDisplayLine - pageSize);

            // 查找目标显示行对应的原始位置
            var (originalLine, charOffset) = FindOriginalPositionByDisplayLine(
                targetDisplayLine,
                windowWidth
            );

            // 检查是否到达文件开头
            if (originalLine == 0 && charOffset == 0 && CurrentChapterIndex > 0)
            {
                HandlePrevChapter();
                return;
            }

            // 更新阅读位置
            CurrentReadOriginalLine = originalLine;
            _currentCharOffset = charOffset;
            _currentLineUsedWrappedCount = 0;

            CurrentBook.LastReadTime = DateTime.Now;
            UpdateProgress();
        }

        public virtual async Task PrevPageAsync() => await Task.Run(() => PrevPage());

        public virtual void NextLine()
        {
            if (CurrentReadOriginalLine < OriginalLines.Count - 1)
            {
                CurrentReadOriginalLine++;
                _currentCharOffset = 0;
                _currentLineUsedWrappedCount = 0;
                CurrentBook.LastReadTime = DateTime.Now;
                UpdateProgress();
            }
            else
            {
                HandleNextChapter();
            }
        }

        public virtual async Task NextLineAsync() => await Task.Run(() => NextLine());

        public virtual string GetCurrentSentence()
        {
            if (CurrentReadOriginalLine >= 0 && CurrentReadOriginalLine < OriginalLines.Count)
            {
                string line = OriginalLines[CurrentReadOriginalLine];

                if (_currentCharOffset > 0 && _currentCharOffset < line.Length)
                {
                    int startPos = FindSentenceStart(line, _currentCharOffset);
                    int endPos = FindSentenceEnd(line, _currentCharOffset);

                    if (startPos < endPos)
                    {
                        return line.Substring(startPos, endPos - startPos);
                    }
                }

                return line;
            }
            return null;
        }

        public virtual (int startLine, int endLine) GetCurrentHighlightRange()
        {
            var currentPage = GetCurrentPage();
            if (currentPage.Length == 0)
                return (0, 0);

            int windowWidth = GetConsoleWidth();

            int pageStartGlobalDisplayLine = GetGlobalDisplayLineIndex(
                CurrentReadOriginalLine,
                _currentCharOffset,
                windowWidth
            );

            int currentLineGlobalDisplayLine = GetGlobalDisplayLineIndex(
                CurrentReadOriginalLine,
                0,
                windowWidth
            );

            int startLineInPage = currentLineGlobalDisplayLine - pageStartGlobalDisplayLine;
            int lineHeight = GetWrappedLineCount(CurrentReadOriginalLine, windowWidth);
            int endLineInPage = Math.Min(startLineInPage + lineHeight - 1, currentPage.Length - 1);

            return (Math.Max(0, startLineInPage), Math.Max(startLineInPage, endLineInPage));
        }

        public virtual List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            if (Chapters == null) return new List<ChapterInfo>();

            if (start < 0) start = 0;
            if (end > Chapters.Count) end = Chapters.Count;
            if (start >= end) return new List<ChapterInfo>();

            return Chapters.Skip(start).Take(end - start).ToList();
        }

        public virtual int GetChaptersCount() => Chapters?.Count ?? 0;

        public virtual void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (Chapters == null || chapterIndex < 0 || chapterIndex >= Chapters.Count)
                throw new ArgumentOutOfRangeException(nameof(chapterIndex));

            var chapter = Chapters[chapterIndex];
            CurrentChapterIndex = chapterIndex;
            CurrentReadOriginalLine = Math.Min(chapter.LineIndex + lineOffset, OriginalLines.Count - 1);
            _currentCharOffset = 0;
            _currentLineUsedWrappedCount = 0;
            UpdateProgress();
        }
        #endregion

        #region 核心按需生成逻辑
        /// <summary>
        /// 生成显示行
        /// </summary>
        private List<string> GenerateDisplayLines(
            int startOriginalLine,
            int startCharOffset,
            int pageSize,
            int windowWidth,
            out int endOriginalLine,
            out int endCharOffset)
        {
            var result = new List<string>();
            int remainingLines = pageSize;
            int currentOriginalIdx = startOriginalLine;
            int currentCharOffset = startCharOffset;

            while (remainingLines > 0 && currentOriginalIdx < OriginalLines.Count)
            {
                var wrapped = WrapLine(OriginalLines[currentOriginalIdx], windowWidth);

                int startWrappedIndex = 0;
                if (currentOriginalIdx == startOriginalLine && startCharOffset > 0)
                {
                    startWrappedIndex = FindWrappedLineIndex(wrapped, startCharOffset);
                }

                int takeCount = Math.Min(remainingLines, wrapped.Count - startWrappedIndex);
                for (int i = 0; i < takeCount; i++)
                {
                    result.Add(wrapped[startWrappedIndex + i]);
                }

                remainingLines -= takeCount;

                if (takeCount == wrapped.Count - startWrappedIndex)
                {
                    currentOriginalIdx++;
                    currentCharOffset = 0;
                }
                else
                {
                    int lastUsedWrappedIndex = startWrappedIndex + takeCount - 1;
                    currentCharOffset = GetLineCharOffset(wrapped[lastUsedWrappedIndex]);
                    break;
                }
            }

            endOriginalLine = currentOriginalIdx;
            endCharOffset = currentCharOffset;

            return result;
        }

        /// <summary>
        /// 将一行文本包装为多行
        /// </summary>
        protected List<string> WrapLine(string line, int width)
        {
            if (string.IsNullOrEmpty(line))
                return new List<string> { "" };

            var result = new List<string>();
            var sb = new StringBuilder();
            int currentWidth = 0;

            foreach (char c in line)
            {
                int charWidth = GetCharDisplayWidth(c);
                if (currentWidth + charWidth > width && sb.Length > 0)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    currentWidth = 0;
                }
                sb.Append(c);
                currentWidth += charWidth;
            }

            if (sb.Length > 0)
                result.Add(sb.ToString());

            return result;
        }

        /// <summary>
        /// 查找字符偏移对应的包装行索引
        /// </summary>
        private int FindWrappedLineIndex(List<string> wrappedLines, int charOffset)
        {
            if (charOffset <= 0) return 0;

            int currentPos = 0;
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                int lineLength = GetLineCharOffset(wrappedLines[i]);

                if (charOffset < currentPos + lineLength)
                    return i;

                currentPos += lineLength;
            }

            return wrappedLines.Count - 1;
        }

        /// <summary>
        /// 获取包装行占用的字符宽度
        /// </summary>
        private int GetLineCharOffset(string wrappedLine)
        {
            int offset = 0;
            foreach (char c in wrappedLine)
            {
                offset += GetCharDisplayWidth(c);
            }
            return offset;
        }

        /// <summary>
        /// 计算指定包装行对应的字符偏移
        /// </summary>
        private int CalculateCharOffset(List<string> wrappedLines, int wrappedLineIndex)
        {
            if (wrappedLineIndex <= 0) return 0;
            if (wrappedLineIndex >= wrappedLines.Count)
                wrappedLineIndex = wrappedLines.Count - 1;

            int offset = 0;
            for (int i = 0; i < wrappedLineIndex; i++)
            {
                offset += GetLineCharOffset(wrappedLines[i]);
            }
            return offset;
        }

        /// <summary>
        /// 获取全局显示行索引
        /// </summary>
        private int GetGlobalDisplayLineIndex(int originalLine, int charOffset, int windowWidth)
        {
            if (originalLine < 0) return 0;

            int displayIndex = 0;

            // 累加前面所有行的包装行数
            for (int i = 0; i < originalLine; i++)
            {
                if (i < OriginalLines.Count)
                {
                    displayIndex += WrapLine(OriginalLines[i], windowWidth).Count;
                }
            }

            // 加上当前行内的偏移
            if (originalLine < OriginalLines.Count)
            {
                var wrapped = WrapLine(OriginalLines[originalLine], windowWidth);
                displayIndex += FindWrappedLineIndex(wrapped, charOffset);
            }

            return displayIndex;
        }

        /// <summary>
        /// 通过全局显示行索引查找原始位置
        /// </summary>
        private (int originalLine, int charOffset) FindOriginalPositionByDisplayLine(
            int targetDisplayLine,
            int windowWidth)
        {
            if (targetDisplayLine <= 0) return (0, 0);

            int remainingLines = targetDisplayLine;

            for (int i = 0; i < OriginalLines.Count; i++)
            {
                var wrapped = WrapLine(OriginalLines[i], windowWidth);
                int wrappedCount = wrapped.Count;

                if (remainingLines < wrappedCount)
                {
                    // 目标位置在当前行内
                    int charOffset = CalculateCharOffset(wrapped, remainingLines);
                    return (i, charOffset);
                }

                remainingLines -= wrappedCount;
            }

            // 超出文件末尾，返回最后位置
            int lastLine = OriginalLines.Count - 1;
            var lastWrapped = WrapLine(OriginalLines[lastLine], windowWidth);
            return (lastLine, CalculateCharOffset(lastWrapped, lastWrapped.Count));
        }

        /// <summary>
        /// 获取原始行占用的显示行数
        /// </summary>
        protected virtual int GetWrappedLineCount(int originalLineIndex, int windowWidth)
        {
            if (originalLineIndex < 0 || originalLineIndex >= OriginalLines.Count)
                return 0;
            return WrapLine(OriginalLines[originalLineIndex], windowWidth).Count;
        }

        protected int GetWrappedLineCount(int originalLineIndex)
            => GetWrappedLineCount(originalLineIndex, GetConsoleWidth());
        #endregion

        #region 章节导航
        protected virtual void HandleNextChapter()
        {
            if (Chapters != null && CurrentChapterIndex < Chapters.Count - 1)
            {
                CurrentChapterIndex++;
                var chapter = Chapters[CurrentChapterIndex];
                CurrentReadOriginalLine = chapter.LineIndex;
                _currentCharOffset = 0;
                _currentLineUsedWrappedCount = 0;
            }
            UpdateProgress();
        }

        protected virtual void HandlePrevChapter()
        {
            if (Chapters != null && CurrentChapterIndex > 0)
            {
                CurrentChapterIndex--;
                var chapter = Chapters[CurrentChapterIndex];
                CurrentReadOriginalLine = chapter.LineIndex;
                _currentCharOffset = 0;
                _currentLineUsedWrappedCount = 0;
            }
            UpdateProgress();
        }
        #endregion

        #region 辅助方法
        private int FindSentenceStart(string line, int position)
        {
            for (int i = position; i >= 0; i--)
            {
                if (i == 0 || IsSentenceDelimiter(line[i - 1]))
                    return i;
            }
            return 0;
        }

        private int FindSentenceEnd(string line, int position)
        {
            for (int i = position; i < line.Length; i++)
            {
                if (IsSentenceDelimiter(line[i]))
                    return i + 1;
            }
            return line.Length;
        }

        private bool IsSentenceDelimiter(char c)
        {
            return c == '。' || c == '？' || c == '！' || c == '…' || c == '.' || c == ';' || c == '；';
        }

        protected virtual int CalculatePageSize()
        {
            try
            {
                // ✅ 添加 null 检查
                if (Config.Instance != null)
                {
                    return Config.Instance.ShowHelpInfo
                        ? Console.WindowHeight - 5
                        : Console.WindowHeight - 1;
                }
                return DefaultPageSize;
            }
            catch
            {
                return DefaultPageSize;
            }
        }

        protected virtual int GetConsoleWidth()
        {
            try
            {
                return Console.WindowWidth > 0 ? Console.WindowWidth : 80;
            }
            catch
            {
                return 80;
            }
        }

        protected virtual int GetCharDisplayWidth(char c)
            => TextFileReader.GetCharDisplayWidth(c);
        #endregion
    }
}