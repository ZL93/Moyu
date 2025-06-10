using System;

namespace Moyu.Models
{
    public class BookInfo
    {
        /// <summary>
        /// 书籍文件的路径
        /// </summary>
        public string BookFilePath { get; set; } = string.Empty;
        /// <summary>
        /// 书籍名称
        /// </summary>
        public string BookName { get; set; } = string.Empty;
        /// <summary>
        /// 书籍类型
        /// </summary>
        public BookFormatEnum BookFormat { get; set; } = BookFormatEnum.Txt;
        /// <summary>
        /// 书签位置
        /// </summary>
        public int BookMarkLoc { get; set; } = 0;
        /// <summary>
        /// 书签进度
        /// </summary>
        public float BookMarkProgress { get; set; } = 0f;
        /// <summary>
        /// 书籍最后阅读时间
        /// </summary>
        public DateTime LastReadTime { get; set; } = DateTime.MinValue;
        
    }
}