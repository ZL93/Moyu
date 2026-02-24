using Moyu.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moyu.Services
{
    /// <summary>
    /// 书籍服务接口，定义了加载书籍、章节导航、分页、内容获取等通用操作。
    /// 支持 TXT、EPUB 等多种格式的实现。
    /// </summary>
    public interface IBookService
    {
        /// <summary>
        /// 根据文件路径获取书籍基本信息（不加载内容）。
        /// </summary>
        BookInfo GetBookInfo(string filePath);

        /// <summary>
        /// 异步获取书籍信息
        /// </summary>
        Task<BookInfo> GetBookInfoAsync(string filePath);

        /// <summary>
        /// 加载指定书籍内容，初始化章节、分页等数据。
        /// </summary>
        void LoadBook(BookInfo book);

        /// <summary>
        /// 异步加载书籍
        /// </summary>
        Task LoadBookAsync(BookInfo book);

        /// <summary>
        /// 获取指定范围内的章节信息（用于章节分页显示）。
        /// </summary>
        List<ChapterInfo> GetChaptersPage(int start, int end);

        /// <summary>
        /// 获取章节总数。
        /// </summary>
        int GetChaptersCount();

        /// <summary>
        /// 跳转到指定章节内的某一行（偏移）。
        /// </summary>
        void JumpToLineInChapter(int chapterIndex, int lineOffset);

        /// <summary>
        /// 获取当前页的内容（适配不同格式的分页）。
        /// </summary>
        string[] GetCurrentPage();

        /// <summary>
        /// 异步获取当前页内容
        /// </summary>
        Task<string[]> GetCurrentPageAsync();

        /// <summary>
        /// 翻到下一页，并更新书签等状态。
        /// </summary>
        void NextPage();

        /// <summary>
        /// 异步翻到下一页
        /// </summary>
        Task NextPageAsync();

        /// <summary>
        /// 翻到上一页，并更新书签等状态。
        /// </summary>
        void PrevPage();

        /// <summary>
        /// 异步翻到上一页
        /// </summary>
        Task PrevPageAsync();

        /// <summary>
        /// 下一行
        /// </summary>
        void NextLine();

        /// <summary>
        /// 异步下一行
        /// </summary>
        Task NextLineAsync();
        string GetCurrentSentence();
        (int startLine, int endLine) GetCurrentHighlightRange();
    }
}