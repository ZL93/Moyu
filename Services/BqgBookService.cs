using Moyu.Models;
using Moyu.Models.Bqg;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Moyu.Services
{
    /// <summary>
    /// 在线小说服务 - 单章加载模式
    /// </summary>
    public class BqgBookService : ChapterBasedBookService, IBookSearch, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSettings;
        private string _novelId;

        private const string SearchApiUrl = "https://apige.cc/api/search?q=";
        private const string BookInfoApiUrl = "https://apige.cc/api/book?id=";
        private const string BookListApiUrl = "https://apige.cc/api/booklist?id=";
        private const string ChapterApiUrl = "https://apige.cc/api/chapter?id={0}&chapterid={1}";

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
            {
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(filePath));
            }

            return Task.Run(async () => await GetBookInfoAsync(filePath)).GetAwaiter().GetResult();
        }

        public override async Task<BookInfo> GetBookInfoAsync(string filePath)
        {
            if (!filePath.StartsWith("online://"))
            {
                throw new ArgumentException("在线书籍路径必须以 online:// 开头", nameof(filePath));
            }

            string novelId = filePath.Replace("online://", "");

            string url = $"{BookInfoApiUrl}{novelId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var detail = JsonConvert.DeserializeObject<BqgNovel>(json, _jsonSettings);

            if (detail == null)
            {
                throw new InvalidOperationException("无法解析书籍信息");
            }

            return new BookInfo
            {
                Title = detail.Title ?? "在线小说",
                Author = detail.Author ?? "未知作者",
                FilePath = filePath,
                Format = BookFormatEnum.Online,
                Introduction = detail.Introduction ?? "暂无简介",
                CurrentChapterIndex = 0,
                CurrentReadOriginalLine = 0,
                LastReadTime = DateTime.Now,
                MarkProgress = 0
            };
        }

        public override void LoadBook(BookInfo book)
        {
            _novelId = book.FilePath.Replace("online://", "");
            base.LoadBook(book);
        }

        protected override async Task<List<ChapterInfo>> LoadChapterListAsync()
        {
            var chapters = new List<ChapterInfo>();

            if (string.IsNullOrEmpty(_novelId))
            {
                return chapters;
            }

            try
            {
                string url = $"{BookListApiUrl}{_novelId}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<BqgChapterListResponse>(json, _jsonSettings);

                if (result?.List != null)
                {
                    for (int i = 0; i < result.List.Count; i++)
                    {
                        var title = result.List[i] ?? $"第{i + 1}章";
                        chapters.Add(new ChapterInfo
                        {
                            Title = title,
                            LineIndex = 0,  // 章节内行索引从0开始
                            ChapterIndex = i
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载章节列表失败: {ex.Message}");
            }

            return chapters;
        }

        protected override async Task<List<string>> LoadChapterContentAsync(int chapterIndex)
        {
            if (chapterIndex < 0 || string.IsNullOrEmpty(_novelId))
            {
                return new List<string> { "参数错误" };
            }

            try
            {
                string url = string.Format(ChapterApiUrl, _novelId, chapterIndex + 1);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var chapter = JsonConvert.DeserializeObject<BqgChapter>(json, _jsonSettings);

                string content = chapter?.Content ?? "内容加载失败";
                string cleanedContent = CleanOnlineContent(content);

                var lines = cleanedContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                return lines.Count == 0
                    ? new List<string> { "本章节暂无内容" }
                    : lines;
            }
            catch (Exception ex)
            {
                return new List<string> { $"章节加载失败: {ex.Message}" };
            }
        }

        protected override void UpdateProgress()
        {
            if (CurrentBook == null || Chapters.Count == 0)
            {
                return;
            }

            CurrentBook.MarkProgress = (float)CurrentChapterIndex / Chapters.Count;
        }

        private string CleanOnlineContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "内容为空";
            }

            string[] onlineAds = {
                "www.bqg6370.xyz",
                "apige.cc",
                "biquge.com",
                "请记住本书首发域名",
                "最新最快无防盗免费阅读"
            };

            string cleaned = content;
            foreach (var ad in onlineAds)
            {
                cleaned = cleaned.Replace(ad, "");
            }

            cleaned = Regex.Replace(cleaned, @"请收藏[：:].*", "");
            cleaned = Regex.Replace(cleaned, @"手机访问地址[：:].*", "");
            cleaned = Regex.Replace(cleaned, @"\(本章完\)", "");
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
                response.EnsureSuccessStatusCode();

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
                    result.Add(new BookInfo
                    {
                        Title = item.Title ?? "未知标题",
                        Author = item.Author ?? "未知作者",
                        FilePath = $"online://{item.Id}",
                        Format = BookFormatEnum.Online,
                        Introduction = item.Introduction ?? "暂无简介",
                        LastReadTime = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜索失败: {ex.Message}");
            }

            return result;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

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