using System;

namespace Moyu.Models
{
    public class BookInfo
    {
        /// <summary>
        /// 书籍文件的路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        /// <summary>
        /// 书籍名称
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 书籍作者
        /// </summary>
        public string Author { get; set; } = string.Empty;
        /// <summary>
        /// 书籍类型
        /// </summary>
        public BookFormatEnum Format { get; set; } = BookFormatEnum.Txt;
        /// <summary>
        /// 书签位置
        /// </summary>
        public int MarkLoc { get; set; } = 0;
        /// <summary>
        /// 书签进度
        /// </summary>
        public float MarkProgress { get; set; } = 0f;
        /// <summary>
        /// 书籍最后阅读时间
        /// </summary>
        public DateTime LastReadTime { get; set; } = DateTime.MinValue;
        /// <summary>
        /// 当前章节序号
        /// </summary>
        public int CurrentChapterIndex { get; set; } = 0;
        /// <summary>
        /// 当前阅读到章节行数
        /// </summary>
        public int CurrentReadChapterLine { get; set; }

    }
}