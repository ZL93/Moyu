using Moyu.Models;
using System.Collections.Generic;

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
        /// <param name="filePath">书籍文件路径</param>
        /// <returns>书籍信息对象</returns>
        BookInfo GetBookInfo(string filePath);

        /// <summary>
        /// 加载指定书籍内容，初始化章节、分页等数据。
        /// </summary>
        /// <param name="book">要加载的书籍信息</param>
        void LoadBook(BookInfo book);

        /// <summary>
        /// 获取指定范围内的章节信息（用于章节分页显示）。
        /// </summary>
        /// <param name="start">起始章节索引（包含）</param>
        /// <param name="end">结束章节索引（不包含）</param>
        /// <returns>章节信息列表</returns>
        List<ChapterInfo> GetChaptersPage(int start, int end);

        /// <summary>
        /// 获取章节总数。
        /// </summary>
        /// <returns>章节数量</returns>
        int GetChaptersCount();

        /// <summary>
        /// 跳转到指定章节内的某一行（偏移）。
        /// </summary>
        /// <param name="chapterIndex">章节索引</param>
        /// <param name="lineOffset">章节内行偏移</param>
        void JumpToLineInChapter(int chapterIndex, int lineOffset);

        /// <summary>
        /// 获取当前页的内容（适配不同格式的分页）。
        /// </summary>
        /// <returns>当前页内容</returns>
        string[] GetCurrentPage();
        /// <summary>
        /// 翻到下一页，并更新书签等状态。
        /// </summary>
        void NextPage();

        /// <summary>
        /// 翻到上一页，并更新书签等状态。
        /// </summary>
        void PrevPage();

        void NextLine();
    }
}
