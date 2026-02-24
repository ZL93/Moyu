using Moyu.Models;
using System;

namespace Moyu.Services
{
    /// <summary>
    /// 书籍服务工厂
    /// </summary>
    public static class BookServiceFactory
    {
        /// <summary>
        /// 根据文件路径创建服务
        /// </summary>
        public static IBookService CreateService(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (filePath.StartsWith("online://", StringComparison.OrdinalIgnoreCase))
                return new BqgBookService();

            var extension = System.IO.Path.GetExtension(filePath)?.ToLower();

            switch (extension)
            {
                case ".txt":
                    return new TxtBookService();
                case ".epub":
                    return new EpubBookService();
                default:
                    throw new NotSupportedException($"不支持的文件格式: {extension}");
            }
        }

        /// <summary>
        /// 根据书籍格式创建服务
        /// </summary>
        public static IBookService CreateService(BookFormatEnum format)
        {
            switch (format)
            {
                case BookFormatEnum.Txt:
                    return new TxtBookService();
                case BookFormatEnum.Epub:
                    return new EpubBookService();
                case BookFormatEnum.Online:
                    return new BqgBookService();
                default:
                    throw new NotSupportedException($"不支持的书籍格式: {format}");
            }
        }

        /// <summary>
        /// 根据书籍信息创建服务
        /// </summary>
        public static IBookService CreateService(BookInfo bookInfo)
        {
            if (bookInfo == null)
                throw new ArgumentNullException(nameof(bookInfo));

            return CreateService(bookInfo.Format);
        }
    }
}