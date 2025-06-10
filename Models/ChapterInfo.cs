namespace Moyu.Models
{
    /// <summary>
    /// 书籍章节信息
    /// </summary>
    public class ChapterInfo
    {
        /// <summary>
        /// 章节标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 章节所在行数
        /// </summary>
        public int LineIndex { get; set; } = 0;
        
    }
}
