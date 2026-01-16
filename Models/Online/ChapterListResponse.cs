using System.Collections.Generic;
using Newtonsoft.Json;

namespace Moyu.Models.Online
{
    public class ChapterListResponse
    {
        [JsonProperty("list")]
        public List<string> List { get; set; }

        [JsonProperty("bookId")]
        public string BookId { get; set; }

        [JsonProperty("bookName")]
        public string BookName { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonIgnore]
        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}