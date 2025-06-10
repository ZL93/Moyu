using Moyu.Models;
using Moyu.Services;
using Moyu.Utils;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Moyu
{
    public class Program
    {
        private static int _pageSize = 10;
        private static BookService bookService = new BookService();
        private static int currentPage = 0;
        private static bool bossKeyDown = false;
        private const char BOSSKEY1 = '·';
        private const char BOSSKEY2 = '`';
        private static void Main()
        {
            Console.CursorVisible = false;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            bookService.LoadBooksFromFile();

            int selectedIndexInPage = 0; // 当前页中选中的项索引

            while (true)
            {
                // 计算当前页的行数
                if (Config.Instance.ShowHelpInfo)
                {
                    _pageSize = Console.WindowHeight - 8;
                }
                else
                {
                    _pageSize = Console.WindowHeight - 3;
                }
                ConsoleHelper.ClearAll(); // 清空控制台缓冲区
                if (bossKeyDown)
                {
                    Console.WriteLine("正在更新中，请稍候...");
                }
                else
                {
                    var books = bookService.GetBooks();
                    int totalBooks = books.Count;
                    int totalPages = (totalBooks + _pageSize - 1) / _pageSize;
                    int start = currentPage * _pageSize;
                    int end = Math.Min(start + _pageSize, totalBooks);

                    Console.WriteLine($"书架目录（第 {currentPage + 1}/{totalPages} 页）\n");

                    for (int i = start; i < end; i++)
                    {
                        int indexInPage = i - start;
                        var book = books[i];
                       
                        string progressStr = book.BookMarkProgress.ToString("P0").PadRight(6);
                        string dateStr = book.LastReadTime.ToString("yy/MM/dd");

                        string indexStr = $"[{indexInPage + 1}]";
                        string prefix = "  ";
                        if (indexInPage == selectedIndexInPage)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            prefix = ">>"; // 高亮当前选中项
                        }

                        string titleStr = $"{prefix} {indexStr} {Truncate(book.BookName, 30)}";
                        string padStr = PadRightDisplay(titleStr, 40);
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
                        Console.WriteLine(" P 设置环境变量");
                    }
                }

                var key = Console.ReadKey(true);

                if (key.KeyChar == BOSSKEY1 || key.KeyChar == BOSSKEY2)
                {
                    bossKeyDown = !bossKeyDown;
                    continue;
                }
                if (bossKeyDown)
                    continue;

                var booksList = bookService.GetBooks();
                int booksCount = booksList.Count;
                int currentPageCount = Math.Min(_pageSize, booksCount - currentPage * _pageSize);
                int globalIndex = currentPage * _pageSize + selectedIndexInPage;
                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        ConsoleHelper.ClearAll(); // 清空控制台缓冲区
                        return;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        if ((currentPage + 1) * _pageSize < booksCount)
                        {
                            currentPage++;
                            selectedIndexInPage = 0;
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        if (currentPage > 0)
                        {
                            currentPage--;
                            selectedIndexInPage = 0;
                        }
                        break;

                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (selectedIndexInPage > 0)
                            selectedIndexInPage--;
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (selectedIndexInPage < currentPageCount - 1)
                            selectedIndexInPage++;
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
                                if (selectedIndexInPage == currentPageCount - 1)
                                {
                                    selectedIndexInPage--;
                                }
                                bookService.RemoveBook(booksList[globalIndex].BookName);
                                bookService.SaveBooksToFile();
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
                    default:
                        // 数字键选择书籍
                        if (char.IsDigit(key.KeyChar))
                        {
                            int selectedIndex = key.KeyChar - '0' - 1; // 转成0基索引
                            var books = bookService.GetBooks();
                            int index = currentPage * _pageSize + selectedIndex;
                            if (selectedIndex >= 0 && index < books.Count)
                            {
                                ReadBook(books[index]);
                            }
                        }
                        break;
                }
            }

        }

        // 截断中文书名（按显示宽度）
        private static string Truncate(string text, int maxDisplayWidth)
        {
            int width = 0;
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                int w = IsFullWidth(ch) ? 2 : 1;
                if (width + w > maxDisplayWidth)
                {
                    sb.Append("…");
                    break;
                }
                sb.Append(ch);
                width += w;
            }
            return sb.ToString();
        }

        // 补足显示宽度到指定宽度
        private static string PadRightDisplay(string text, int totalDisplayWidth)
        {
            int currentWidth = GetDisplayWidth(text);
            return text + new string(' ', Math.Max(0, totalDisplayWidth - currentWidth));
        }

        // 获取字符串显示宽度
        private static int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (var ch in text)
                width += IsFullWidth(ch) ? 2 : 1;
            return width;
        }

        // 判断是否为全角字符（如中文、日文）
        private static bool IsFullWidth(char ch)
        {
            return ch >= 0x4E00 && ch <= 0x9FFF // 中日韩统一表意文字
                || ch >= 0xFF01 && ch <= 0xFF60 // 全角标点
                || ch >= 0x3000 && ch <= 0x303F; // 中日韩符号
        }

        private static void AddBook()
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
                // 是文件，直接添加
                bookService.AddBook(inputPath);
                bookService.SaveBooksToFile();
                Console.WriteLine("添加文件成功！");
            }
            else if (Directory.Exists(inputPath))
            {
                // 是文件夹，遍历添加所有txt文件
                var txtFiles = Directory.GetFiles(inputPath, "*.txt", SearchOption.TopDirectoryOnly);
                if (txtFiles.Length == 0)
                {
                    Console.WriteLine("文件夹内无txt文件！");
                }
                else
                {
                    foreach (var file in txtFiles)
                    {
                        bookService.AddBook(file);
                    }
                    bookService.SaveBooksToFile();
                    Console.WriteLine($"文件夹内共添加 {txtFiles.Length} 本小说！");
                }
            }
            else
            {
                Console.WriteLine("路径无效！");
            }
            Thread.Sleep(500);
        }

        private static void ReadBook(BookInfo book)
        {
            ConsoleHelper.ClearAll(); // 清空控制台缓冲区
            Console.WriteLine("加载中，请稍候...");
            bookService.ReadBook(book);
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
                    string pageContent = bookService.GetPage();
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
                    // 如果按下了Boss键，直接忽略其他按键
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        bookService.NextPage(book);
                        bookService.SaveBooksToFile();
                        break;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        bookService.PrevPage(book);
                        bookService.SaveBooksToFile();
                        break;
                    case ConsoleKey.T:
                        bookService.ShowChapters(book);
                        break;
                    case ConsoleKey.Escape:
                        exit = true;
                        break;
                    default:
                        break;
                }
            }
        }

    }
}
