using Newtonsoft.Json;
using System.Collections.Generic;

namespace Moyu.Models.Bqg
{
    public class BqgNovel
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("intro")]
        public string Introduction { get; set; }

        [JsonProperty("cover")]
        public string CoverUrl { get; set; }
        [JsonProperty("sortname")]
        public string Category { get; set; } // 新增：小说分类

        [JsonProperty("full")]
        public string Status { get; set; } // 新增：是否完本

        [JsonProperty("lastChapter")]
        public string LastChapter { get; set; }

        [JsonProperty("updateTime")]
        public string UpdateTime { get; set; }
        [JsonProperty("lastchapterid")]
        public string LastChapterId { get; set; } // 新增：最新章节ID

        [JsonProperty("lastupdate")]
        public string LastUpdate { get; set; } // 新增：最后更新时间

        [JsonProperty("dirid")]
        public string DirectoryId { get; set; } // 新增：目录ID

        // 扩展字段
        public int TotalChapters { get; set; }
        public string Source { get; set; }

        [JsonIgnore]
        public bool IsFavorite { get; set; }
    }
}
