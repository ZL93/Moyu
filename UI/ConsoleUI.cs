using Moyu.Models;
using Moyu.Services;
using Moyu.Utils;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Moyu.UI
{
    public class ConsoleUI
    {
        private BookService bookService = new BookService();
        private bool bossKeyDown = false;
        private const char BOSSKEY1 = '·';
        private const char BOSSKEY2 = '`';

        public void Run()
        {
            Console.CursorVisible = false;
            Console.ForegroundColor = ConsoleColor.DarkGray;

            Config.Instance.LoadConfig();
            Config.Instance.bookInfos = Config.Instance.bookInfos.OrderByDescending(b => b.LastReadTime).ToList();

            int globalIndex = 0;
            int pageSize = 10;
            while (true)
            {
                // 计算当前页的行数
                if (Config.Instance.ShowHelpInfo)
                {
                    pageSize = Console.WindowHeight - 8;
                }
                else
                {
                    pageSize = Console.WindowHeight - 3;
                }
                var booksList = Config.Instance.bookInfos;
                int booksCount = booksList.Count; //总书籍数量
                int currentPage = globalIndex / pageSize; // 计算当前页的索引
                int selectedIndexInPage = globalIndex % pageSize; //

                ConsoleHelper.ClearAll();
                if (bossKeyDown)
                {
                    Console.WriteLine("正在更新中，请稍候...");
                }
                else
                {
                    ShowBookShelf(currentPage, selectedIndexInPage, pageSize);
                }

                var key = Console.ReadKey(true);

                if (key.KeyChar == BOSSKEY1 || key.KeyChar == BOSSKEY2)
                {
                    bossKeyDown = !bossKeyDown;
                    continue;
                }
                if (bossKeyDown)
                {
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        ConsoleHelper.ClearAll();
                        return;
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        globalIndex = Math.Min(booksCount - 1, globalIndex + pageSize);
                        break;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        globalIndex = Math.Max(0, globalIndex - pageSize);
                        break;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        globalIndex = Math.Max(0, globalIndex - 1);
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        globalIndex = Math.Min(booksCount - 1, globalIndex + 1);
                        break;
                    case ConsoleKey.Enter:
                        if (globalIndex >= 0 && globalIndex < booksCount)
                        {
                            ReadBook(booksList[globalIndex]);
                        }
                        break;
                    case ConsoleKey.O:
                        AddBook();
                        break;
                    case ConsoleKey.Delete:
                        Console.Clear();
                        Console.Write("\n确认删除请按Y键");
                        if (Console.ReadKey().Key == ConsoleKey.Y)
                        
                        {
                            if (globalIndex >= 0 && globalIndex < booksCount)
                            {
                                RemoveBook(booksList[globalIndex].Title);
                                if (globalIndex == booksCount - 1)
                                {
                                    globalIndex--;
                                }
                                Config.Instance.SaveConfig();
                            }
                        }
                        break;
                    case ConsoleKey.P:
                        try
                        {
                            SysEnvironment.SetPathAfter(AppDomain.CurrentDomain.BaseDirectory);
                            Console.Clear();
                            Console.WriteLine("设置成功!(部分电脑可能需要重启~)");
                        }
                        catch (Exception ex)
                        {
                            Console.Clear();
                            Console.WriteLine("设置失败!可能原因:权限不足,异常消息:" + ex.Message);
                        }
                        Thread.Sleep(1000);
                        break;
                    case ConsoleKey.H:
                        Config.Instance.ShowHelpInfo = !Config.Instance.ShowHelpInfo;
                        Config.Instance.SaveConfig();
                        break;
                    default:
                        if (char.IsDigit(key.KeyChar))
                        {
                            int selectedIndex = key.KeyChar - '0' - 1;
                            int index = currentPage * pageSize + selectedIndex;
                            if (selectedIndex >= 0 && index < booksCount)
                            {
                                ReadBook(booksList[index]);
                            }
                        }
                        break;
                }
            }
        }

        private void ShowBookShelf(int currentPage, int selectedIndexInPage, int pageSize)
        {
            var books = Config.Instance.bookInfos;
            int totalBooks = books.Count;
            int totalPages = (totalBooks + pageSize - 1) / pageSize;// 计算总页数
            int start = currentPage * pageSize;// 计算当前页的起始索引
            int end = Math.Min(start + pageSize, totalBooks); // 计算当前页的结束索引

            Console.WriteLine($"书架目录（第 {currentPage + 1}/{totalPages} 页）\n");

            for (int i = start; i < end; i++)
            {
                int indexInPage = i - start;
                var book = books[i];

                string progressStr = book.MarkProgress.ToString("P0").PadRight(6);
                string dateStr = book.LastReadTime.ToString("yy/MM/dd");

                string indexStr = $"[{indexInPage + 1}]";
                string prefix = "  ";
                if (indexInPage == selectedIndexInPage)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    prefix = ">>";
                }

                var bookName = book.Title;
                if (!string.IsNullOrEmpty(book.Author))
                {
                    bookName += $" - {book.Author}";
                }

                string titleStr = $"{prefix} {indexStr} {TextFileReader.Truncate(bookName, 30)}";
                string padStr = TextFileReader.PadRightDisplay(titleStr, 40);
                if (book.LastReadTime == DateTime.MinValue)
                {
                    Console.WriteLine($"{padStr} 未读");
                }
                else
                {
                    Console.WriteLine($"{padStr} 进度:{progressStr} {dateStr}");
                }
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            if (Config.Instance.ShowHelpInfo)
            {
                Console.WriteLine("\n操作说明：");
                Console.WriteLine(" ↑/W ↓/S 选择书籍    Enter 打开    ESC 返回");
                Console.WriteLine(" ←/A →/D 翻页        O 添加书籍    Delete 删除");
                Console.WriteLine(" P 设置环境变量      H 开启/关闭操作说明");
            }
        }

        private void AddBook()
        {
            Console.Clear();
            Console.Write("\n请输入小说文件或文件夹完整路径：");
            string inputPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                Console.WriteLine("路径不能为空！");
                Thread.Sleep(500);
                return;
            }

            if (File.Exists(inputPath))
            {
                AddBook(inputPath);
                Config.Instance.SaveConfig();
                Console.WriteLine("添加文件成功！");
            }
            else if (Directory.Exists(inputPath))
            {
                var extensions = new[] { ".txt", ".epub" };
                var files = Directory.GetFiles(inputPath)
                    .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                    .ToList();
                if (files.Count == 0)
                {
                    Console.WriteLine("文件夹内无小说文件！");
                }
                else
                {
                    foreach (var file in files)
                    {
                        AddBook(file);
                    }
                    Config.Instance.SaveConfig();
                    Console.WriteLine($"文件夹内共添加 {files.Count} 本小说！");
                }
            }
            else
            {
                Console.WriteLine("路径无效！");
            }
            Thread.Sleep(500);
        }

        private void AddBook(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (Config.Instance.bookInfos.Find(b => b.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)) != null)
            {
                return;
            }

            Config.Instance.bookInfos.Add(bookService.GetBookInfo(filePath));
        }

        private void RemoveBook(string bookName)
        {
            Config.Instance.bookInfos.RemoveAll(b => b.Title.Equals(bookName, StringComparison.OrdinalIgnoreCase));
        }

        private void ReadBook(BookInfo book)
        {
            ConsoleHelper.ClearAll();
            Console.WriteLine("加载中，请稍候...");
            bookService.LoadBook(book);
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                if (bossKeyDown)
                {
                    Console.WriteLine("正在更新中，请稍候...");
                }
                else
                {
                    string pageContent = bookService.GetCurrentPage();
                    Console.WriteLine(pageContent);

                    if (Config.Instance.ShowHelpInfo)
                    {
                        Console.WriteLine("\n操作说明：");
                        Console.WriteLine(" ←/A 上一页    →/D 下一页    T 选择章节");
                    }
                }
                var key = Console.ReadKey(true);

                if (key.KeyChar == BOSSKEY1 || key.KeyChar == BOSSKEY2)
                {
                    bossKeyDown = !bossKeyDown;
                    continue;
                }
                if (bossKeyDown)
                {
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        bookService.NextPage();
                        Config.Instance.SaveConfig();
                        break;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        bookService.PrevPage();
                        Config.Instance.SaveConfig();
                        break;
                    case ConsoleKey.T:
                        ShowChapters(book);
                        break;
                    case ConsoleKey.Escape:
                        exit = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private void ShowChapters(BookInfo book)
        {
            int chapterCount = bookService.GetChaptersCount();
            if (chapterCount == 0)
            {
                Console.WriteLine("未检测到章节。");
                Thread.Sleep(1000);
                return;
            }

            int pageSize = Config.Instance.ShowHelpInfo ? Console.WindowHeight - 8 : Console.WindowHeight - 1;
            int totalChapterPages = (chapterCount + pageSize - 1) / pageSize;

            int currentChapterIndex = book.CurrentChapterIndex;

            while (true)
            {
                int chapterPage = currentChapterIndex / pageSize;
                int selectedIndexInPage = currentChapterIndex % pageSize;

                Console.Clear();
                if (Config.Instance.ShowHelpInfo)
                {
                    Console.WriteLine($"章节目录 （第 {chapterPage + 1}/{totalChapterPages} 页）\n");
                }

                int start = chapterPage * pageSize;
                int end = Math.Min(start + pageSize, chapterCount);
                var chapters = bookService.GetChaptersPage(start, end);

                for (int i = 0; i < chapters.Count; i++)
                {
                    int globalIndex = start + i;
                    if (globalIndex == currentChapterIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($">>[{i + 1}] {chapters[i].Title}");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        Console.WriteLine($"  [{i + 1}] {chapters[i].Title}");
                    }
                }

                if (Config.Instance.ShowHelpInfo)
                {
                    Console.WriteLine("\n操作说明：");
                    Console.WriteLine(" ↑/↓ 移动选择   ←/A 上一页   →/D 下一页");
                    Console.WriteLine(" Enter 跳转章节    ESC 返回");
                }

                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (currentChapterIndex > 0)
                            currentChapterIndex--;
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (currentChapterIndex < chapterCount - 1)
                            currentChapterIndex++;
                        break;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        if (chapterPage > 0)
                            currentChapterIndex = Math.Max(0, currentChapterIndex - pageSize);
                        break;
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        if ((chapterPage + 1) * pageSize < chapterCount)
                            currentChapterIndex = Math.Min(chapterCount - 1, currentChapterIndex + pageSize);
                        break;
                    case ConsoleKey.Enter:
                        bookService.JumpToLineInChapter(currentChapterIndex, 0);
                        return;
                    case ConsoleKey.Escape:
                        return;
                    default:
                        if (char.IsDigit(key.KeyChar))
                        {
                            int selIndex = key.KeyChar - '0' - 1;
                            int gIndex = chapterPage * pageSize + selIndex;
                            if (selIndex >= 0 && gIndex < chapterCount)
                            {
                                currentChapterIndex = gIndex;
                                bookService.JumpToLineInChapter(currentChapterIndex, 0);
                                return;
                            }
                        }
                        break;
                }
            }
        }

    }
}
