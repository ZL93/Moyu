using Moyu.Models;
using Moyu.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Moyu.Services
{
    /// <summary>
    /// 单章加载书籍服务基类
    /// 适用于EPUB、在线小说等按章节独立加载的格式
    /// </summary>
    public abstract class ChapterBasedBookService : IBookService
    {
        protected BookInfo CurrentBook { get; private set; }
        protected List<ChapterInfo> Chapters { get; private set; } = new List<ChapterInfo>();

        // 章节内容缓存
        protected ConcurrentDictionary<int, List<string>> ChapterContentCache { get; } = new ConcurrentDictionary<int, List<string>>();
        protected ConcurrentDictionary<int, string> ChapterTitleCache { get; } = new ConcurrentDictionary<int, string>();

        // 当前阅读位置（章节内）
        private int _currentChapterIndex = 0;
        private int _currentChapterLineIndex = 0;
        private int _currentWrappedLineIndex = 0;

        // 预加载相关字段
        private readonly object _preloadLock = new object();
        private int _preloadingChapter = -1;           // 正在预加载的章节
        private DateTime _lastPreloadTime = DateTime.MinValue;
        private const int PreloadDelayMs = 500;        // 防抖延迟，避免频繁预加载
        private const int PreloadMaxChapters = 2;       // 最大预加载章节数

        protected virtual int DefaultPageSize => 10;

        protected int CurrentChapterIndex
        {
            get => _currentChapterIndex;
            set
            {
                _currentChapterIndex = value;
                if (CurrentBook != null)
                    CurrentBook.CurrentChapterIndex = value;
            }
        }

        protected int CurrentChapterLineIndex
        {
            get => _currentChapterLineIndex;
            set
            {
                _currentChapterLineIndex = value;
                if (CurrentBook != null)
                    CurrentBook.CurrentReadOriginalLine = value;
            }
        }

        #region 抽象方法
        public abstract BookInfo GetBookInfo(string filePath);
        public abstract Task<BookInfo> GetBookInfoAsync(string filePath);
        protected abstract Task<List<string>> LoadChapterContentAsync(int chapterIndex);
        protected abstract Task<List<ChapterInfo>> LoadChapterListAsync();
        protected abstract void UpdateProgress();
        #endregion

        #region IBookService 实现
        public virtual void LoadBook(BookInfo book)
        {
            CurrentBook = book ?? throw new ArgumentNullException(nameof(book));

            CurrentChapterIndex = Math.Max(0, book.CurrentChapterIndex);
            CurrentChapterLineIndex = Math.Max(0, book.CurrentReadOriginalLine);
            _currentWrappedLineIndex = 0;

            CurrentBook.LastReadTime = DateTime.Now;

            Task.Run(async () => await LoadChaptersAsync());
        }

        public virtual async Task LoadBookAsync(BookInfo book)
        {
            CurrentBook = book ?? throw new ArgumentNullException(nameof(book));

            CurrentChapterIndex = Math.Max(0, book.CurrentChapterIndex);
            CurrentChapterLineIndex = Math.Max(0, book.CurrentReadOriginalLine);
            _currentWrappedLineIndex = 0;

            CurrentBook.LastReadTime = DateTime.Now;

            await LoadChaptersAsync();
        }

        public virtual async Task<string[]> GetCurrentPageAsync()
            => await Task.Run(() => GetCurrentPage());

        public virtual void NextPage()
        {
            var chapterLines = GetCurrentChapterLines();
            if (chapterLines.Count == 0) return;

            int pageSize = CalculatePageSize();
            int windowWidth = GetConsoleWidth();

            var (newLineIndex, newWrappedIndex) = CalculateNextPosition(
                chapterLines,
                _currentChapterLineIndex,
                _currentWrappedLineIndex,
                pageSize,
                windowWidth
            );

            if (newLineIndex >= chapterLines.Count)
            {
                // 本章结束，进入下一章
                if (CurrentChapterIndex < Chapters.Count - 1)
                {
                    CurrentChapterIndex++;
                    CurrentChapterLineIndex = 0;
                    _currentWrappedLineIndex = 0;

                    // 触发预加载
                    TriggerPreload();
                }
            }
            else
            {
                CurrentChapterLineIndex = newLineIndex;
                _currentWrappedLineIndex = newWrappedIndex;
            }

            CurrentBook.LastReadTime = DateTime.Now;
            UpdateProgress();
        }

        public virtual async Task NextPageAsync() => await Task.Run(() => NextPage());

        public virtual void PrevPage()
        {
            if (_currentChapterLineIndex == 0 && _currentWrappedLineIndex == 0)
            {
                if (CurrentChapterIndex > 0)
                {
                    CurrentChapterIndex--;

                    var prevChapterLines = EnsureChapterLoadedAsync(CurrentChapterIndex).GetAwaiter().GetResult();

                    int pageSize = CalculatePageSize();
                    int windowWidth = GetConsoleWidth();

                    var (lastLineIndex, lastWrappedIndex) = GetLastPageStartPosition(
                        prevChapterLines,
                        pageSize,
                        windowWidth
                    );

                    CurrentChapterLineIndex = lastLineIndex;
                    _currentWrappedLineIndex = lastWrappedIndex;
                }
            }
            else
            {
                int pageSize = CalculatePageSize();
                int windowWidth = GetConsoleWidth();
                var chapterLines = GetCurrentChapterLines();

                var (newLineIndex, newWrappedIndex) = CalculatePrevPosition(
                    chapterLines,
                    _currentChapterLineIndex,
                    _currentWrappedLineIndex,
                    pageSize,
                    windowWidth
                );

                CurrentChapterLineIndex = newLineIndex;
                _currentWrappedLineIndex = newWrappedIndex;
            }

            CurrentBook.LastReadTime = DateTime.Now;
            UpdateProgress();
        }

        public virtual async Task PrevPageAsync() => await Task.Run(() => PrevPage());

        public virtual void NextLine()
        {
            var chapterLines = GetCurrentChapterLines();

            if (_currentChapterLineIndex < chapterLines.Count - 1)
            {
                _currentChapterLineIndex++;
                CurrentBook.CurrentReadOriginalLine = _currentChapterLineIndex;
                CurrentBook.LastReadTime = DateTime.Now;
                UpdateProgress();
            }
            else if (_currentChapterIndex < Chapters.Count - 1)
            {
                // 下一章
                _currentChapterIndex++;
                _currentChapterLineIndex = 0;
                CurrentBook.CurrentChapterIndex = _currentChapterIndex;
                CurrentBook.CurrentReadOriginalLine = 0;
                CurrentBook.LastReadTime = DateTime.Now;
                UpdateProgress();

                // 触发预加载
                TriggerPreload();
            }
        }

        public virtual async Task NextLineAsync() => await Task.Run(() => NextLine());

        public virtual string[] GetCurrentPage()
        {
            var chapterLines = GetCurrentChapterLines();
            if (chapterLines.Count == 0)
                return new[] { "章节加载中..." };

            int pageSize = CalculatePageSize();
            int windowWidth = GetConsoleWidth();

            return GeneratePageContent(
                chapterLines,
                _currentChapterLineIndex,
                _currentWrappedLineIndex,
                pageSize,
                windowWidth
            );
        }

        public virtual string GetCurrentSentence()
        {
            var chapterLines = GetCurrentChapterLines();
            return _currentChapterLineIndex >= 0 && _currentChapterLineIndex < chapterLines.Count
                ? chapterLines[_currentChapterLineIndex]
                : null;
        }

        public virtual (int startLine, int endLine) GetCurrentHighlightRange()
        {
            var currentPage = GetCurrentPage();
            if (currentPage.Length == 0)
                return (0, 0);

            int windowWidth = GetConsoleWidth();
            var chapterLines = GetCurrentChapterLines();

            if (_currentChapterLineIndex >= chapterLines.Count)
                return (0, 0);

            // 1. 获取当前行的所有分割行
            var wrapped = WrapLine(chapterLines[_currentChapterLineIndex], windowWidth);
            int lineHeight = wrapped.Count;

            // 2. 在 currentPage 中查找当前行的第一个分割 行
            int endLineInPage = 0;

            // 遍历当前页的每一行，找到第一个匹配当前行内容的行
            for (int i = 0; i < lineHeight; i++)
            {
                if (wrapped[i] == currentPage[0])  // 匹配当前行的第一个分割行
                {
                    endLineInPage = lineHeight - i - 1;
                    break;
                }
            }
            int pageSize = CalculatePageSize();
            if (endLineInPage >= pageSize)
                endLineInPage = pageSize - 1;
            return (0, endLineInPage);
        }

        public virtual List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            lock (Chapters)
            {
                if (Chapters == null || Chapters.Count == 0)
                {
                    return GetPlaceholderChapters(start, end);
                }

                if (start < 0) start = 0;
                if (end > Chapters.Count) end = Chapters.Count;
                if (start >= end) return new List<ChapterInfo>();

                return Chapters.Skip(start).Take(end - start).ToList();
            }
        }

        public virtual int GetChaptersCount()
        {
            lock (Chapters)
            {
                return Chapters?.Count ?? 0;
            }
        }

        public virtual void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (chapterIndex < 0)
            {
                return;
            }

            lock (Chapters)
            {
                if (Chapters == null || Chapters.Count == 0)
                {
                    CurrentChapterIndex = chapterIndex;
                    CurrentChapterLineIndex = Math.Max(0, lineOffset);
                    _currentWrappedLineIndex = 0;

                    Task.Run(async () =>
                    {
                        await LoadChaptersAsync();
                        lock (Chapters)
                        {
                            if (CurrentChapterIndex >= Chapters.Count)
                            {
                                int newIndex = Math.Max(0, Chapters.Count - 1);
                                CurrentChapterIndex = newIndex;
                            }
                        }
                        await EnsureChapterLoadedAsync(CurrentChapterIndex);

                        // 触发预加载
                        TriggerPreload();
                    });
                    return;
                }

                if (chapterIndex >= Chapters.Count)
                {
                    chapterIndex = Math.Max(0, Chapters.Count - 1);
                }

                CurrentChapterIndex = chapterIndex;
                CurrentChapterLineIndex = Math.Max(0, lineOffset);
                _currentWrappedLineIndex = 0;

                Task.Run(async () =>
                {
                    await EnsureChapterLoadedAsync(chapterIndex);

                    // 触发预加载
                    TriggerPreload();
                });

                UpdateProgress();
            }
        }
        #endregion

        #region 预加载功能
        /// <summary>
        /// 触发预加载（带防抖）
        /// </summary>
        protected void TriggerPreload()
        {
            // 防抖：避免频繁触发
            if ((DateTime.Now - _lastPreloadTime).TotalMilliseconds < PreloadDelayMs)
                return;

            _lastPreloadTime = DateTime.Now;

            // 异步执行预加载
            Task.Run(async () => await PreloadAdjacentChaptersAsync());
        }

        /// <summary>
        /// 预加载相邻章节
        /// </summary>
        private async Task PreloadAdjacentChaptersAsync()
        {
            try
            {
                // 获取当前章节索引
                int currentIndex = CurrentChapterIndex;

                // 预加载后续章节
                for (int i = 1; i <= PreloadMaxChapters; i++)
                {
                    int nextIndex = currentIndex + i;
                    if (nextIndex < Chapters.Count)
                    {
                        await PreloadChapterAsync(nextIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"预加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 预加载指定章节
        /// </summary>
        private async Task PreloadChapterAsync(int chapterIndex)
        {
            // 参数验证
            if (chapterIndex < 0 || chapterIndex >= Chapters.Count)
                return;

            // 检查是否已在缓存中
            if (ChapterContentCache.ContainsKey(chapterIndex))
                return;

            // 检查是否正在预加载
            lock (_preloadLock)
            {
                if (_preloadingChapter == chapterIndex)
                    return;
                _preloadingChapter = chapterIndex;
            }

            try
            {
                // 执行加载
                var content = await LoadChapterContentAsync(chapterIndex);

                if (content != null && content.Count > 0)
                {
                    // 存入缓存
                    ChapterContentCache.AddOrUpdate(chapterIndex, content, (key, oldValue) => content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"预加载第 {chapterIndex + 1} 章失败: {ex.Message}");
            }
            finally
            {
                lock (_preloadLock)
                {
                    if (_preloadingChapter == chapterIndex)
                        _preloadingChapter = -1;
                }
            }
        }

        /// <summary>
        /// 手动预加载指定章节（对外接口）
        /// </summary>
        public async Task PreloadChapter(int chapterIndex)
        {
            await PreloadChapterAsync(chapterIndex);
        }

        /// <summary>
        /// 清除预加载状态
        /// </summary>
        public void ClearPreloadState()
        {
            lock (_preloadLock)
            {
                _preloadingChapter = -1;
            }
        }
        #endregion

        #region 核心分页计算逻辑
        private string[] GeneratePageContent(
            List<string> chapterLines,
            int startLine,
            int startWrappedIndex,
            int pageSize,
            int windowWidth)
        {
            var result = new List<string>();
            int remainingLines = pageSize;
            int currentLine = startLine;
            int currentWrappedIndex = startWrappedIndex;

            while (remainingLines > 0 && currentLine < chapterLines.Count)
            {
                var wrapped = WrapLine(chapterLines[currentLine], windowWidth);

                int takeCount = Math.Min(remainingLines, wrapped.Count - currentWrappedIndex);

                for (int i = 0; i < takeCount; i++)
                {
                    result.Add(wrapped[currentWrappedIndex + i]);
                }

                remainingLines -= takeCount;

                if (takeCount == wrapped.Count - currentWrappedIndex)
                {
                    currentLine++;
                    currentWrappedIndex = 0;
                }
                else
                {
                    currentWrappedIndex += takeCount;
                    break;
                }
            }

            return result.ToArray();
        }

        private (int lineIndex, int wrappedIndex) CalculateNextPosition(
            List<string> chapterLines,
            int currentLine,
            int currentWrappedIndex,
            int pageSize,
            int windowWidth)
        {
            int remainingLines = pageSize;
            int lineIndex = currentLine;
            int wrappedIndex = currentWrappedIndex;

            while (remainingLines > 0 && lineIndex < chapterLines.Count)
            {
                var wrapped = WrapLine(chapterLines[lineIndex], windowWidth);
                int availableLines = wrapped.Count - wrappedIndex;

                if (remainingLines < availableLines)
                {
                    wrappedIndex += remainingLines;
                    remainingLines = 0;
                }
                else
                {
                    remainingLines -= availableLines;
                    lineIndex++;
                    wrappedIndex = 0;
                }
            }

            return (lineIndex, wrappedIndex);
        }

        private (int lineIndex, int wrappedIndex) CalculatePrevPosition(
            List<string> chapterLines,
            int currentLine,
            int currentWrappedIndex,
            int pageSize,
            int windowWidth)
        {
            if (currentLine == 0 && currentWrappedIndex == 0)
                return (0, 0);

            int totalPrevDisplayLines = 0;
            for (int i = 0; i < currentLine; i++)
            {
                totalPrevDisplayLines += WrapLine(chapterLines[i], windowWidth).Count;
            }
            totalPrevDisplayLines += currentWrappedIndex;

            int targetDisplayLine = Math.Max(0, totalPrevDisplayLines - pageSize);

            return FindPositionByDisplayLine(chapterLines, targetDisplayLine, windowWidth);
        }

        private (int lineIndex, int wrappedIndex) FindPositionByDisplayLine(
            List<string> chapterLines,
            int targetDisplayLine,
            int windowWidth)
        {
            if (targetDisplayLine <= 0) return (0, 0);

            int remainingLines = targetDisplayLine;

            for (int i = 0; i < chapterLines.Count; i++)
            {
                var wrapped = WrapLine(chapterLines[i], windowWidth);

                if (remainingLines < wrapped.Count)
                {
                    return (i, remainingLines);
                }

                remainingLines -= wrapped.Count;
            }

            int lastLine = chapterLines.Count - 1;
            var lastWrapped = WrapLine(chapterLines[lastLine], windowWidth);
            return (lastLine, lastWrapped.Count - 1);
        }

        private (int lineIndex, int wrappedIndex) GetLastPageStartPosition(
            List<string> chapterLines,
            int pageSize,
            int windowWidth)
        {
            if (chapterLines.Count == 0) return (0, 0);

            int totalDisplayLines = 0;
            foreach (var line in chapterLines)
            {
                totalDisplayLines += WrapLine(line, windowWidth).Count;
            }

            int targetDisplayLine = Math.Max(0, totalDisplayLines - pageSize);

            return FindPositionByDisplayLine(chapterLines, targetDisplayLine, windowWidth);
        }

        private (int lineIndex, int wrappedIndex) FindPageStartPosition(
            List<string> chapterLines,
            int currentLine,
            int currentWrappedIndex,
            int pageSize,
            int windowWidth)
        {
            int currentDisplayLine = 0;
            for (int i = 0; i < currentLine; i++)
            {
                currentDisplayLine += WrapLine(chapterLines[i], windowWidth).Count;
            }
            currentDisplayLine += currentWrappedIndex;

            int pageStartDisplayLine = Math.Max(0, currentDisplayLine - (pageSize - 1));

            return FindPositionByDisplayLine(chapterLines, pageStartDisplayLine, windowWidth);
        }
        #endregion

        #region 核心章节加载逻辑
        private async Task LoadChaptersAsync()
        {
            try
            {
                var chapters = await LoadChapterListAsync();
                lock (Chapters)
                {
                    Chapters.Clear();
                    if (chapters != null)
                    {
                        Chapters.AddRange(chapters);

                        for (int i = 0; i < chapters.Count; i++)
                        {
                            ChapterTitleCache[i] = chapters[i].Title;
                        }
                    }
                }

                if (CurrentChapterIndex >= Chapters.Count)
                {
                    int oldIndex = CurrentChapterIndex;
                    CurrentChapterIndex = Math.Max(0, Chapters.Count - 1);
                }

                await EnsureChapterLoadedAsync(CurrentChapterIndex);

                // 加载完成后触发预加载
                TriggerPreload();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载章节列表失败: {ex.Message}");
            }
        }

        protected async Task<List<string>> EnsureChapterLoadedAsync(int chapterIndex)
        {
            if (chapterIndex < 0)
                return new List<string> { "无效的章节索引" };

            if (ChapterContentCache.TryGetValue(chapterIndex, out var cached))
                return cached ?? new List<string>();

            try
            {
                var content = await LoadChapterContentAsync(chapterIndex) ?? new List<string>();
                ChapterContentCache.AddOrUpdate(chapterIndex, content, (key, oldValue) => content);
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载章节 {chapterIndex} 失败: {ex.Message}");
                return new List<string> { $"章节加载失败: {ex.Message}" };
            }
        }

        protected List<string> GetCurrentChapterLines()
        {
            if (ChapterContentCache.TryGetValue(CurrentChapterIndex, out var lines))
                return lines;

            try
            {
                var task = Task.Run(async () => await EnsureChapterLoadedAsync(CurrentChapterIndex));
                return task.GetAwaiter().GetResult();
            }
            catch
            {
                return new List<string> { "章节加载失败" };
            }
        }
        #endregion

        #region 辅助方法
        protected List<string> WrapLine(string line, int width)
        {
            if (string.IsNullOrEmpty(line))
                return new List<string> { "" };

            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
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

        protected virtual int CalculatePageSize()
        {
            try
            {
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

        protected List<ChapterInfo> GetPlaceholderChapters(int start, int end)
        {
            var placeholders = new List<ChapterInfo>();
            for (int i = start; i < end && i < 20; i++)
            {
                placeholders.Add(new ChapterInfo
                {
                    Title = $"加载中... 第{i + 1}章",
                    LineIndex = i,
                    ChapterIndex = i
                });
            }
            return placeholders;
        }
        #endregion
    }
}