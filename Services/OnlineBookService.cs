using Moyu.Models;
using Moyu.Models.Online;
using Moyu.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Moyu.Services
{
    /// <summary>
    /// 在线小说阅读服务 - 适配IBookService接口
    /// 注意：某些方法对在线阅读是无效的，但为了兼容接口必须实现
    /// </summary>
    public class OnlineBookService : IBookService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSettings;

        // 当前阅读状态
        private BookInfo currentBook;
        private string novelId;
        private List<ChapterInfo> chapterList = new List<ChapterInfo>();
        private int _pageSize = 10;
        private int currentChapterIndex = -1;
        private List<string> _wrappedLines = new List<string>();

        // 缓存管理
        private readonly Dictionary<int, string[]> _chapterContentCache = new Dictionary<int, string[]>();
        private readonly Dictionary<int, List<string>> _chapterLinesCache = new Dictionary<int, List<string>>();

        // API配置
        private const string SearchApiUrl = "https://www.bqg6370.xyz/api/search?q=";
        private const string BookInfoApiUrl = "https://apige.cc/api/book?id=";
        private const string BookListApiUrl = "https://apige.cc/api/booklist?id=";
        private const string ChapterApiUrl = "https://apige.cc/api/chapter?id={0}&chapterid={1}";

        public OnlineBookService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
        }

        /// <summary>
        /// 在线小说的特殊实现 - 创建在线书籍信息
        /// </summary>
        public BookInfo GetBookInfo(string filePath)
        {
            // 文件路径格式: "online://小说ID"
            if (filePath.StartsWith("online://"))
            {
                string novelId = filePath.Replace("online://", "");

                string url = $"{BookInfoApiUrl}{novelId}";
                Console.WriteLine($"获取书籍详情: {url}");

                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var detail = JsonConvert.DeserializeObject<OnlineNovel>(json, _jsonSettings);

                    if (detail == null)
                    {
                        return null;
                    }

                    return GetBookInfo(detail);
                }
                else
                {
                    Console.WriteLine($"HTTP错误: {response.StatusCode}");
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// 创建在线书籍信息（扩展方法）
        /// </summary>
        public BookInfo GetBookInfo(OnlineNovel novel)
        {
            return new BookInfo
            {
                Title = novel.Title ?? "在线小说",
                Author = novel.Author ?? "未知作者",
                FilePath = $"online://{novel.Id}",
                Format = BookFormatEnum.Online,
                Introduction = novel.Introduction ?? "暂无简介",
                CurrentChapterIndex = 0,
                CurrentReadChapterLine = 0,
                LastReadTime = DateTime.Now,
                MarkProgress = 0
            };
        }

        /// <summary>
        /// IBookService接口实现 - 这里需要BookInfo对象
        /// </summary>
        public void LoadBook(BookInfo book)
        {
            currentBook = book;

            // 在线小说需要额外的初始化
            if (book.FilePath.StartsWith("online://"))
            {
                novelId = book.FilePath.Replace("online://", "");
                // 初始化占位章节
                chapterList.Clear();
                for (int i = 0; i < 100; i++)
                {
                    chapterList.Add(new ChapterInfo
                    {
                        Title = $"第{i + 1}章",
                        ChapterIndex = i
                    });
                }
                // 异步加载实际章节列表（但不阻塞）
                Task.Run(() => LoadActualChapterList());

                // 加载当前章节内容
                LoadChapterContent(book.CurrentChapterIndex);
            }
        }

        /// <summary>
        /// 异步加载实际章节列表
        /// </summary>
        private void LoadActualChapterList()
        {
            try
            {
                if (string.IsNullOrEmpty(novelId))
                    return;

                string url = $"{BookListApiUrl}{novelId}";
                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var result = JsonConvert.DeserializeObject<ChapterListResponse>(json, _jsonSettings);

                    if (result != null && result.List != null && result.List.Count > 0)
                    {
                        // 更新章节列表
                        chapterList.Clear();
                        for (int i = 0; i < result.List.Count; i++)
                        {
                            chapterList.Add(new ChapterInfo
                            {
                                Title = result.List[i],
                                ChapterIndex = i
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载章节列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载指定章节内容
        /// </summary>
        private void LoadChapterContent(int chapterIndex)
        {
            if (chapterIndex < 0 || string.IsNullOrEmpty(novelId))
                return;

            // 检查缓存
            if (_chapterLinesCache.ContainsKey(chapterIndex))
            {
                _wrappedLines = _chapterLinesCache[chapterIndex];
                currentChapterIndex = chapterIndex;
                return;
            }

            try
            {
                string url = string.Format(ChapterApiUrl, novelId, chapterIndex + 1);
                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var chapter = JsonConvert.DeserializeObject<OnlineChapter>(json, _jsonSettings);

                    string content = chapter?.Content ?? "内容加载失败";
                    string cleanedContent = CleanContent(content);

                    // 包装内容
                    _wrappedLines = WrapContent(cleanedContent, Console.WindowWidth);

                    // 缓存
                    _chapterLinesCache[chapterIndex] = _wrappedLines;
                    currentChapterIndex = chapterIndex;
                }
                else
                {
                    _wrappedLines = new List<string> { $"章节加载失败: HTTP {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                _wrappedLines = new List<string> { $"章节加载失败: {ex.Message}" };
            }

            // 确保有内容显示
            if (_wrappedLines.Count == 0)
            {
                _wrappedLines.Add("本章节暂无内容");
            }
        }

        /// <summary>
        /// 包装文本以适应控制台宽度
        /// </summary>
        private List<string> WrapContent(string content, int width)
        {
            var lines = new List<string>();
            var paragraphs = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    lines.Add("");
                    continue;
                }

                var sb = new StringBuilder();
                int currentWidth = 0;

                foreach (char c in trimmed)
                {
                    int charWidth = TextFileReader.GetCharDisplayWidth(c);
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
        /// 清理内容中的广告和HTML标签
        /// </summary>
        private string CleanContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "内容为空";

            string[] ads = {
                "更*多`精;彩小*说尽|在．０１bz．第一;*小说*站",
                "downloadchmdepilernow:",
                "请收藏本站",
                "无弹窗全文免费阅读",
                "小兵小说",
                "笔趣阁",
                "www.bqg6370.xyz",
                "apige.cc"
            };

            string cleaned = content;
            foreach (var ad in ads)
            {
                cleaned = cleaned.Replace(ad, "");
            }

            // 清理HTML标签
            cleaned = Regex.Replace(cleaned, "<[^>]+>", string.Empty);

            // 清理格式
            cleaned = cleaned.Replace("\r\n", "\n")
                            .Replace("\n\n\n", "\n\n")
                            .Replace("\t", "    ")
                            .Trim();

            return cleaned;
        }

        // ==================== IBookService接口实现 ====================

        public List<ChapterInfo> GetChaptersPage(int start, int end)
        {
            return chapterList.Skip(start).Take(end - start).ToList();
        }

        public int GetChaptersCount()
        {
            return chapterList.Count;
        }

        public void JumpToLineInChapter(int chapterIndex, int lineOffset)
        {
            if (chapterIndex >= 0 && chapterIndex < chapterList.Count)
            {
                // 确保章节已加载
                if (currentChapterIndex != chapterIndex)
                {
                    LoadChapterContent(chapterIndex);
                }

                currentBook.CurrentChapterIndex = chapterIndex;
                currentBook.CurrentReadChapterLine = lineOffset;

                if (chapterList.Count > 0)
                {
                    currentBook.MarkProgress = (float)chapterIndex / chapterList.Count;
                }
            }
        }

        public string[] GetCurrentPage()
        {
            if (currentBook == null || _wrappedLines.Count == 0)
                return new string[] { "内容加载中..." };

            // 动态计算页面大小
            if (Config.Instance.ShowHelpInfo)
            {
                _pageSize = Console.WindowHeight - 5;
            }
            else
            {
                _pageSize = Console.WindowHeight - 1;
            }

            int startLine = currentBook.CurrentReadChapterLine;
            if (startLine >= _wrappedLines.Count)
            {
                startLine = Math.Max(0, _wrappedLines.Count - _pageSize);
                currentBook.CurrentReadChapterLine = startLine;
            }

            int count = Math.Min(_pageSize, _wrappedLines.Count - startLine);
            return _wrappedLines.Skip(startLine).Take(count).ToArray();
        }

        public void NextPage()
        {
            if (currentBook == null) return;

            currentBook.CurrentReadChapterLine += _pageSize;
            currentBook.LastReadTime = DateTime.Now;

            // 如果超出当前章节，尝试加载下一章
            if (currentBook.CurrentReadChapterLine >= _wrappedLines.Count)
            {
                if (currentBook.CurrentChapterIndex < chapterList.Count - 1)
                {
                    // 切换到下一章
                    currentBook.CurrentChapterIndex++;
                    LoadChapterContent(currentBook.CurrentChapterIndex);
                    currentBook.CurrentReadChapterLine = 0;

                    if (chapterList.Count > 0)
                    {
                        currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    }
                }
                else
                {
                    // 已经是最后一章，回滚到最后一行
                    currentBook.CurrentReadChapterLine = Math.Max(0, _wrappedLines.Count - _pageSize);
                }
            }
        }

        public void PrevPage()
        {
            if (currentBook == null) return;

            currentBook.CurrentReadChapterLine -= _pageSize;
            currentBook.LastReadTime = DateTime.Now;

            if (currentBook.CurrentReadChapterLine < 0)
            {
                if (currentBook.CurrentChapterIndex > 0)
                {
                    // 切换到上一章
                    currentBook.CurrentChapterIndex--;
                    LoadChapterContent(currentBook.CurrentChapterIndex);

                    // 定位到上一章的末尾
                    currentBook.CurrentReadChapterLine = Math.Max(0, _wrappedLines.Count - _pageSize);

                    if (chapterList.Count > 0)
                    {
                        currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    }
                }
                else
                {
                    // 已经是第一章，回到开头
                    currentBook.CurrentReadChapterLine = 0;
                }
            }
        }

        public void NextLine()
        {
            if (currentBook == null) return;

            currentBook.CurrentReadChapterLine++;
            currentBook.LastReadTime = DateTime.Now;

            // 检查是否需要切换到下一章
            if (currentBook.CurrentReadChapterLine >= _wrappedLines.Count)
            {
                if (currentBook.CurrentChapterIndex < chapterList.Count - 1)
                {
                    currentBook.CurrentChapterIndex++;
                    LoadChapterContent(currentBook.CurrentChapterIndex);
                    currentBook.CurrentReadChapterLine = 0;

                    if (chapterList.Count > 0)
                    {
                        currentBook.MarkProgress = (float)currentBook.CurrentChapterIndex / chapterList.Count;
                    }
                }
                else
                {
                    currentBook.CurrentReadChapterLine = Math.Max(0, _wrappedLines.Count - 1);
                }
            }
        }

        // ==================== 在线阅读专用方法 ====================

        /// <summary>
        /// 搜索小说（同步）
        /// </summary>
        public List<BookInfo> SearchNovels(string keyword)
        {
            List<BookInfo> result = new List<BookInfo>();
            try
            {
                string encodedKeyword = HttpUtility.UrlEncode(keyword, Encoding.UTF8);
                string url = $"{SearchApiUrl}{encodedKeyword}";

                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    List<OnlineNovel> novels;
                    try
                    {
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(json, _jsonSettings);
                        novels = apiResponse?.Data ?? new List<OnlineNovel>();
                    }
                    catch
                    {
                        novels = JsonConvert.DeserializeObject<List<OnlineNovel>>(json, _jsonSettings)
                                 ?? new List<OnlineNovel>();
                    }
                    foreach (var item in novels)
                    {
                        result.Add(GetBookInfo(item));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜索失败: {ex.Message}");
            }

            return result;
        }

        // 内部API响应类
        private class ApiResponse
        {
            [JsonProperty("data")]
            public List<OnlineNovel> Data { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }
        }
    }
}