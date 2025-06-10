using Moyu.Models;
using System;
using System.Collections.Generic;

namespace Moyu.Services
{
    public class BookService : IBookService
    {
        private IBookService _innerService;

        public void LoadBook(BookInfo book)
        {
            switch (book.Format)
            {
                case BookFormatEnum.Txt:
                    _innerService = new TxtBookService();
                    break;
                case BookFormatEnum.Epub:
                    _innerService = new EpubBookService();
                    break;
                default:
                    throw new NotSupportedException("不支持的格式");
            }
            _innerService.LoadBook(book);
        }

        public void JumpToChapter(int chapterIndex) => _innerService.JumpToChapter(chapterIndex);
        public void JumpToLineInChapter(int chapterIndex, int lineOffset) => _innerService.JumpToLineInChapter(chapterIndex, lineOffset);
        public BookInfo GetBookInfo(string filePath)
        {
            return filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? new TxtBookService().GetBookInfo(filePath)
                : filePath.EndsWith(".epub", StringComparison.OrdinalIgnoreCase)
                    ? new EpubBookService().GetBookInfo(filePath)
                    : throw new NotSupportedException("不支持的书籍格式");
        }
        public void NextPage() => _innerService.NextPage();
        public void PrevPage() => _innerService.PrevPage();
        public int GetChaptersCount() => _innerService.GetChaptersCount();
        public List<ChapterInfo> GetChaptersPage(int start, int end) => _innerService.GetChaptersPage(start, end);

        public string GetCurrentPage() => _innerService.GetCurrentPage();
    }

}