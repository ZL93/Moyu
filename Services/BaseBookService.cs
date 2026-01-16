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
    /// 书籍服务基类，提供通用功能实现
    /// </summary>
    public abstract class BaseBookService : IBookService
    {
        protected BookInfo CurrentBook { get; private set; }
        protected List<ChapterInfo> Chapters { get; private set; } = new List<ChapterInfo>();
        protected List<string> WrappedLines { get; set; } = new List<string>();
        protected int CurrentChapterIndex { get; set; } = -1;

        // 缓存管理
        protected readonly Dictionary<int, List<string>> ChapterLinesCache = new Dictionary<int, List<string>>();
        protected readonly Dictionary<int, string[]> ChapterContentCache = new Dictionary<int, string[]>();

        // 配置
        protected virtual int DefaultPageSize => 10;
        protected virtual bool EnableCaching => true;
        protected virtual bool AutoDetectChapters => true;

        #region 抽象方法 - 子类必须实现
        public abstract BookInfo GetBookInfo(string filePath);

        /// <summary>
        /// 加载章节内容的具体实现
        /// </summary>
        protected abstract void LoadChapterContentInternal(int chapterIndex);

        /// <summary>
        /// 初始化章节列表
        /// </summary>
        protected abstract void InitializeChapters();
        #endregion

        #region 虚方法 - 子类可以重写
        public virtual async Task<BookInfo> GetBookInfoAsync(string filePath)
        {
            return await Task.Run(() => GetBookInfo(filePath));
        }

        public virtual void LoadBook(BookInfo book)
        {
            CurrentBook = book ?? throw new ArgumentNullException(nameof(book));
            CurrentBook.LastReadTime = DateTime.Now;

            // 初始化章节列表
            InitializeChapters();

            // 如果有当前章节索引，加载对应章节
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

        public virtual async Task LoadBookAsync(BookInfo book)
        {
            await Task.Run(() => LoadBook(book));
        }

        public virtual List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            if (start < 0) start = 0;
            if (end > Chapters.Count) end = Chapters.Count;
            if (start >= end) return new List<ChapterInfo>();

            return Chapters.Skip(start).Take(end - start).ToList();
        }

        public virtual int GetChaptersCount() => Chapters.Count;

        public virtual void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (chapterIndex < 0 || chapterIndex >= Chapters.Count)
                throw new ArgumentOutOfRangeException(nameof(chapterIndex));

            // 确保章节已加载
            if (CurrentChapterIndex != chapterIndex)
            {
                LoadChapterContent(chapterIndex);
            }

            CurrentBook.CurrentChapterIndex = chapterIndex;
            CurrentBook.CurrentReadChapterLine = Math.Max(0, Math.Min(lineOffset, WrappedLines.Count - 1));

            UpdateProgress();
        }

        public virtual string[] GetCurrentPage()
        {
            if (CurrentBook == null || WrappedLines.Count == 0)
                return new[] { "内容加载中..." };

            int startLine = GetCurrentStartLine();
            int pageSize = CalculatePageSize();
            int count = Math.Min(pageSize, WrappedLines.Count - startLine);

            return WrappedLines.Skip(startLine).Take(count).ToArray();
        }

        public virtual async Task<string[]> GetCurrentPageAsync()
        {
            return await Task.Run(() => GetCurrentPage());
        }

        public virtual void NextPage()
        {
            if (CurrentBook == null) return;

            CurrentBook.CurrentReadChapterLine += CalculatePageSize();
            CurrentBook.LastReadTime = DateTime.Now;

            HandlePageBoundary();
        }

        public virtual async Task NextPageAsync()
        {
            await Task.Run(() => NextPage());
        }

        public virtual void PrevPage()
        {
            if (CurrentBook == null) return;

            CurrentBook.CurrentReadChapterLine -= CalculatePageSize();
            CurrentBook.LastReadTime = DateTime.Now;

            if (CurrentBook.CurrentReadChapterLine < 0)
            {
                HandlePrevChapterOrReset();
            }
        }

        public virtual async Task PrevPageAsync()
        {
            await Task.Run(() => PrevPage());
        }

        public virtual void NextLine()
        {
            if (CurrentBook == null) return;

            CurrentBook.CurrentReadChapterLine++;
            CurrentBook.LastReadTime = DateTime.Now;

            HandlePageBoundary();
        }

        public virtual async Task NextLineAsync()
        {
            await Task.Run(() => NextLine());
        }
        #endregion

        #region 受保护的方法 - 供子类使用
        /// <summary>
        /// 加载章节内容（带缓存）
        /// </summary>
        protected virtual void LoadChapterContent(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= Chapters.Count)
                return;

            // 检查缓存
            if (EnableCaching && ChapterLinesCache.TryGetValue(chapterIndex, out var cachedLines))
            {
                WrappedLines = cachedLines;
                CurrentChapterIndex = chapterIndex;
                return;
            }

            // 加载内容
            LoadChapterContentInternal(chapterIndex);

            // 缓存结果
            if (EnableCaching)
            {
                ChapterLinesCache[chapterIndex] = WrappedLines;
            }

            CurrentChapterIndex = chapterIndex;
        }

        /// <summary>
        /// 文本包装通用方法
        /// </summary>
        protected virtual List<string> WrapText(string text, int width)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines;

            // 按段落分割
            var paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var paragraph in paragraphs)
            {
                var trimmed = paragraph.TrimEnd();
                if (string.IsNullOrEmpty(trimmed))
                {
                    lines.Add("");
                    continue;
                }

                var sb = new StringBuilder();
                int currentWidth = 0;

                foreach (char c in trimmed)
                {
                    int charWidth = GetCharDisplayWidth(c);
                    if (currentWidth + charWidth > width)
                    {
                        lines.Add(sb.ToString());
                        sb.Clear();
                        currentWidth = 0;
                    }
                    sb.Append(c);
                    currentWidth += charWidth;
                }

                if (sb.Length > 0)
                {
                    lines.Add(sb.ToString());
                }
            }

            return lines;
        }

        /// <summary>
        /// 获取字符显示宽度
        /// </summary>
        protected virtual int GetCharDisplayWidth(char c)
        {
            return TextFileReader.GetCharDisplayWidth(c);
        }

        /// <summary>
        /// 更新阅读进度
        /// </summary>
        protected virtual void UpdateProgress()
        {
            if (CurrentBook == null || Chapters.Count == 0) return;

            CurrentBook.MarkProgress = (float)CurrentBook.CurrentChapterIndex / Chapters.Count;
            CurrentBook.LastReadTime = DateTime.Now;
        }

        /// <summary>
        /// 处理页面边界（翻页时超出当前章节）
        /// </summary>
        protected virtual void HandlePageBoundary()
        {
            if (CurrentBook.CurrentReadChapterLine >= WrappedLines.Count)
            {
                HandleNextChapter();
            }
        }

        /// <summary>
        /// 处理切换到下一章
        /// </summary>
        protected virtual void HandleNextChapter()
        {
            if (CurrentBook.CurrentChapterIndex < Chapters.Count - 1)
            {
                CurrentBook.CurrentChapterIndex++;
                LoadChapterContent(CurrentBook.CurrentChapterIndex);
                CurrentBook.CurrentReadChapterLine = 0;
                UpdateProgress();
            }
            else
            {
                // 已经是最后一章，回滚到最后一行
                CurrentBook.CurrentReadChapterLine = Math.Max(0, WrappedLines.Count - CalculatePageSize());
            }
        }

        /// <summary>
        /// 处理切换到上一章或重置
        /// </summary>
        protected virtual void HandlePrevChapterOrReset()
        {
            if (CurrentBook.CurrentChapterIndex > 0)
            {
                CurrentBook.CurrentChapterIndex--;
                LoadChapterContent(CurrentBook.CurrentChapterIndex);
                CurrentBook.CurrentReadChapterLine = Math.Max(0, WrappedLines.Count - CalculatePageSize());
                UpdateProgress();
            }
            else
            {
                // 已经是第一章，回到开头
                CurrentBook.CurrentReadChapterLine = 0;
            }
        }

        /// <summary>
        /// 获取当前起始行
        /// </summary>
        protected virtual int GetCurrentStartLine()
        {
            if (CurrentBook == null) return 0;

            int startLine = CurrentBook.CurrentReadChapterLine;
            if (startLine >= WrappedLines.Count)
            {
                startLine = Math.Max(0, WrappedLines.Count - CalculatePageSize());
                CurrentBook.CurrentReadChapterLine = startLine;
            }

            return startLine;
        }

        /// <summary>
        /// 计算页面大小
        /// </summary>
        protected virtual int CalculatePageSize()
        {
            try
            {
                return Config.Instance.ShowHelpInfo
                    ? Console.WindowHeight - 5
                    : Console.WindowHeight - 1;
            }
            catch
            {
                return DefaultPageSize;
            }
        }

        /// <summary>
        /// 清理内容中的广告和不需要的文本
        /// </summary>
        protected virtual string CleanContent(string content, params string[] adsToRemove)
        {
            if (string.IsNullOrEmpty(content)) return content;

            var cleaned = content;

            // 默认的广告词
            var defaultAds = new[]
            {
                "更*多`精;彩小*说尽|在．０１bz．第一;*小说*站",
                "downloadchmdepilernow:",
                "请收藏本站",
                "无弹窗全文免费阅读",
                "小兵小说",
                "笔趣阁"
            };

            var allAds = adsToRemove != null && adsToRemove.Length > 0
                ? defaultAds.Concat(adsToRemove).Distinct().ToArray()
                : defaultAds;

            foreach (var ad in allAds)
            {
                cleaned = cleaned.Replace(ad, "");
            }

            return cleaned;
        }
        #endregion
    }
}