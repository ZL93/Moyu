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
        /// 书籍简介
        /// </summary>
        public string Introduction { get; set; } = string.Empty;
        /// <summary>
        /// 书籍类型
        /// </summary>
        public BookFormatEnum Format { get; set; } = BookFormatEnum.Txt;
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
        /// 当前阅读的原始行位置（对于 Txt 格式，精确到行；对于 Epub 格式，表示章节内行位置）
        /// </summary>
        public int CurrentReadOriginalLine { get; set; }

    }
}