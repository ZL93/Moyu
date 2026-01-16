using Newtonsoft.Json;

namespace Moyu.Models.Bqg
{
    public class BqgChapter
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("chapterid")]
        public int ChapterId { get; set; }

        [JsonProperty("chaptername")]
        public string ChapterName { get; set; }

        [JsonProperty("txt")]
        public string Content { get; set; }

        [JsonProperty("prev")]
        public string PreviousChapterId { get; set; }

        [JsonProperty("next")]
        public string NextChapterId { get; set; }

        [JsonProperty("cs")]
        public int? ChapterSize { get; set; }

        [JsonProperty("md5")]
        public string Md5 { get; set; }

        [JsonProperty("time")]
        public long? Time { get; set; }

        // 清理后的内容
        [JsonIgnore]
        public string CleanedContent { get; set; }
        [JsonProperty("error")]
        public string Error { get; internal set; }
        [JsonIgnore]
        public bool HasError => !string.IsNullOrEmpty(Error);

        public string GetDisplayName()
        {
            return $"第{ChapterId}章 {ChapterName}";
        }
    }
}