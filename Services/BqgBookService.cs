using Moyu.Models;
using Moyu.Models.Bqg;
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
    public class BqgBookService : BaseBookService, IBookSearch
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSettings;
        private string _novelId;

        // API配置
        private const string SearchApiUrl = "https://www.bqg6370.xyz/api/search?q=";
        private const string BookInfoApiUrl = "https://apige.cc/api/book?id=";
        private const string BookListApiUrl = "https://apige.cc/api/booklist?id=";
        private const string ChapterApiUrl = "https://apige.cc/api/chapter?id={0}&chapterid={1}";

        protected override bool EnableCaching => false;

        public BqgBookService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
        }

        public override BookInfo GetBookInfo(string filePath)
        {
            if (!filePath.StartsWith("online://"))
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(filePath));

            string novelId = filePath.Replace("online://", "");

            try
            {
                string url = $"{BookInfoApiUrl}{novelId}";
                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var detail = JsonConvert.DeserializeObject<BqgNovel>(json, _jsonSettings);

                    if (detail == null)
                        throw new InvalidOperationException("无法解析书籍信息");

                    return new BookInfo
                    {
                        Title = detail.Title ?? "在线小说",
                        Author = detail.Author ?? "未知作者",
                        FilePath = filePath,
                        Format = BookFormatEnum.Online,
                        Introduction = detail.Introduction ?? "暂无简介",
                        CurrentChapterIndex = 0,
                        CurrentReadChapterLine = 0,
                        LastReadTime = DateTime.Now,
                        MarkProgress = 0
                    };
                }
                else
                {
                    throw new HttpRequestException($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取在线书籍信息失败: {ex.Message}", ex);
            }
        }

        public override async Task<BookInfo> GetBookInfoAsync(string filePath)
        {
            if (!filePath.StartsWith("online://"))
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(filePath));

            string novelId = filePath.Replace("online://", "");

            try
            {
                string url = $"{BookInfoApiUrl}{novelId}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var detail = JsonConvert.DeserializeObject<BqgNovel>(json, _jsonSettings);

                    if (detail == null)
                        throw new InvalidOperationException("无法解析书籍信息");

                    return new BookInfo
                    {
                        Title = detail.Title ?? "在线小说",
                        Author = detail.Author ?? "未知作者",
                        FilePath = filePath,
                        Format = BookFormatEnum.Online,
                        Introduction = detail.Introduction ?? "暂无简介",
                        CurrentChapterIndex = 0,
                        CurrentReadChapterLine = 0,
                        LastReadTime = DateTime.Now,
                        MarkProgress = 0
                    };
                }
                else
                {
                    throw new HttpRequestException($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取在线书籍信息失败: {ex.Message}", ex);
            }
        }

        public override void LoadBook(BookInfo book)
        {
            base.LoadBook(book);

            if (!book.FilePath.StartsWith("online://"))
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(book.FilePath));

            _novelId = book.FilePath.Replace("online://", "");

            // 异步加载章节列表
            Task.Run(async () =>
            {
                try
                {
                    await LoadChapterListAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"异步加载章节列表失败: {ex.Message}");
                }
            });

            // 加载当前章节内容
            if (CurrentBook.CurrentChapterIndex >= 0)
            {
                LoadChapterContent(CurrentBook.CurrentChapterIndex);
            }
        }

        public override async Task LoadBookAsync(BookInfo book)
        {
            await base.LoadBookAsync(book);

            if (!book.FilePath.StartsWith("online://"))
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(book.FilePath));

            _novelId = book.FilePath.Replace("online://", "");

            // 异步加载章节列表
            await LoadChapterListAsync();

            // 加载当前章节内容
            if (CurrentBook.CurrentChapterIndex >= 0)
            {
                await Task.Run(() => LoadChapterContent(CurrentBook.CurrentChapterIndex));
            }
        }

        private async Task LoadChapterListAsync()
        {
            if (string.IsNullOrEmpty(_novelId))
                return;

            try
            {
                string url = $"{BookListApiUrl}{_novelId}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<BqgChapterListResponse>(json, _jsonSettings);

                    if (result?.List != null && result.List.Count > 0)
                    {
                        lock (Chapters)
                        {
                            Chapters.Clear();
                            for (int i = 0; i < result.List.Count; i++)
                            {
                                Chapters.Add(new ChapterInfo
                                {
                                    Title = result.List[i] ?? $"第{i + 1}章",
                                    ChapterIndex = i,
                                    LineIndex = i
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载章节列表失败: {ex.Message}");
            }
        }

        protected override void LoadChapterContentInternal(int chapterIndex)
        {
            if (chapterIndex < 0 || string.IsNullOrEmpty(_novelId))
                return;

            try
            {
                string url = string.Format(ChapterApiUrl, _novelId, chapterIndex + 1);
                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var chapter = JsonConvert.DeserializeObject<BqgChapter>(json, _jsonSettings);

                    string content = chapter?.Content ?? "内容加载失败";
                    string cleanedContent = CleanOnlineContent(content);

                    // 包装内容
                    int width = Console.WindowWidth;
                    WrappedLines = WrapText(cleanedContent, width);
                }
                else
                {
                    WrappedLines = new List<string> { $"章节加载失败: HTTP {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                WrappedLines = new List<string> { $"章节加载失败: {ex.Message}" };
            }

            // 确保有内容显示
            if (WrappedLines.Count == 0)
            {
                WrappedLines.Add("本章节暂无内容");
            }
        }

        protected override void InitializeChapters()
        {
            // 初始化占位章节
            Chapters.Clear();
            for (int i = 0; i < 20; i++) // 初始显示20个占位章节
            {
                Chapters.Add(new ChapterInfo
                {
                    Title = $"加载中... 第{i + 1}章",
                    ChapterIndex = i,
                    LineIndex = i
                });
            }
        }

        private string CleanOnlineContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "内容为空";

            // 在线小说特有的清理
            string[] onlineAds = {
                "www.bqg6370.xyz",
                "apige.cc",
                "biquge.com",
                "请记住本书首发域名",
                "最新最快无防盗免费阅读"
            };

            string cleaned = CleanContent(content, onlineAds);

            // 额外的在线内容清理
            cleaned = Regex.Replace(cleaned, @"请收藏[：:].*", "");
            cleaned = Regex.Replace(cleaned, @"手机访问地址[：:].*", "");
            cleaned = Regex.Replace(cleaned, @"\(本章完\)", "");

            // 清理格式
            cleaned = cleaned.Replace("\r\n", "\n")
                            .Replace("\n\n\n", "\n\n")
                            .Replace("\t", "    ")
                            .Trim();

            return cleaned;
        }

        public List<BookInfo> SearchBooks(string keyword)
        {
            var result = new List<BookInfo>();

            try
            {
                string encodedKeyword = HttpUtility.UrlEncode(keyword, Encoding.UTF8);
                string url = $"{SearchApiUrl}{encodedKeyword}";

                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    List<BqgNovel> novels;
                    try
                    {
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(json, _jsonSettings);
                        novels = apiResponse?.Data ?? new List<BqgNovel>();
                    }
                    catch
                    {
                        novels = JsonConvert.DeserializeObject<List<BqgNovel>>(json, _jsonSettings)
                                 ?? new List<BqgNovel>();
                    }

                    foreach (var item in novels)
                    {
                        var bookInfo = new BookInfo
                        {
                            Title = item.Title ?? "未知标题",
                            Author = item.Author ?? "未知作者",
                            FilePath = $"online://{item.Id}",
                            Format = BookFormatEnum.Online,
                            Introduction = item.Introduction ?? "暂无简介",
                            LastReadTime = DateTime.Now
                        };
                        result.Add(bookInfo);
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
            public List<BqgNovel> Data { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }
        }
    }
}