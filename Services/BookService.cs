using Moyu.Models;
using Moyu.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using VersOne.Epub;

namespace Moyu.Services
{
    public class BookService
    {
        private List<string> _originalLines = new List<string>();
        private List<string> _wrappedLines = new List<string>();

        private List<int> _originalLineToWrappedLineMap = new List<int>();
        private List<int> _wrappedLineToOriginalLineMap = new List<int>();

        private int currentLineCount = 0;
        private int TotalLineCount => _originalLines.Count;
        private List<ChapterInfo> _chapters { get; set; } = new List<ChapterInfo>();
        private int _pageSize = 10;

        public List<BookInfo> GetBooks() => Config.Instance.bookInfos;

        public void AddBook(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (GetBooks().Find(b => b.BookFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)) != null)
            {
                // 如果书籍已存在，则不添加
                return;
            }

            if (filePath.EndsWith(".txt"))
            {
                GetBooks().Add(new BookInfo
                {
                    BookFilePath = filePath,
                    BookName = Path.GetFileNameWithoutExtension(filePath),
                    BookFormat = BookFormatEnum.Txt
                });
            }
            else if (filePath.EndsWith(".epub"))
            {
                try
                {
                    using (EpubBookRef epubBookRef = EpubReader.OpenBook(filePath))
                    {
                        GetBooks().Add(new BookInfo
                        {
                            BookName = $"{epubBookRef.Title} -- {epubBookRef.Author}",
                            BookFilePath = filePath,
                            BookFormat = BookFormatEnum.Epub
                        });

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EPUB 解析失败: {ex.Message}");
                }
            }
        }

        public void RemoveBook(string bookName)
        {
            GetBooks().RemoveAll(b => b.BookName.Equals(bookName, StringComparison.OrdinalIgnoreCase));
        }

        public void LoadBooksFromFile()
        {
            Config.Instance.LoadConfig();

            Config.Instance.bookInfos = Config.Instance.bookInfos.OrderByDescending(b => b.LastReadTime).ToList();

        }

        public void SaveBooksToFile()
        {
            Config.Instance.SaveConfig();
        }

        /// <summary>
        /// 读取书籍内容并进行换行处理
        /// </summary>
        /// <param name="book"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public void ReadBook(BookInfo book)
        {
            if (book == null)
            {
                throw new ArgumentNullException(nameof(book));
            }

            if (!File.Exists(book.BookFilePath))
            {
                throw new FileNotFoundException("书籍文件不存在", book.BookFilePath);
            }

            if (book.BookFormat == BookFormatEnum.Txt)
            {
                _originalLines.Clear();
                _wrappedLines.Clear();
                _originalLineToWrappedLineMap.Clear();
                _wrappedLineToOriginalLineMap.Clear();
                _originalLines = TextFileReader.ReadTextFileLines(book.BookFilePath);

                int width = Console.WindowWidth;
                for (int i = 0; i < _originalLines.Count; i++)
                {
                    string line = _originalLines[i];
                    _originalLineToWrappedLineMap.Add(_wrappedLines.Count);

                    if (string.IsNullOrEmpty(line))
                    {
                        _wrappedLines.Add(string.Empty);
                        _wrappedLineToOriginalLineMap.Add(i);
                        continue;
                    }

                    var sb = new StringBuilder();
                    int currentWidth = 0;

                    foreach (char c in line)
                    {
                        int charWidth = GetCharDisplayWidth(c);

                        if (currentWidth + charWidth > width)
                        {
                            _wrappedLines.Add(sb.ToString());
                            _wrappedLineToOriginalLineMap.Add(i);
                            sb.Clear();
                            currentWidth = 0;
                        }

                        sb.Append(c);
                        currentWidth += charWidth;
                    }

                    if (sb.Length > 0)
                    {
                        _wrappedLines.Add(sb.ToString());
                        _wrappedLineToOriginalLineMap.Add(i);
                    }
                }

                currentLineCount = _originalLineToWrappedLineMap[book.BookMarkLoc];

                DetectChapters(book);
                // 更新书签进度
                book.BookMarkProgress = (float)book.BookMarkLoc / TotalLineCount;
                book.LastReadTime = DateTime.Now;
            }
            else if (book.BookFormat == BookFormatEnum.Epub)
            {
                _originalLines.Clear();
                _wrappedLines.Clear();
                _originalLineToWrappedLineMap.Clear();
                _wrappedLineToOriginalLineMap.Clear();

                var epubBook = EpubReader.ReadBook(book.BookFilePath);
                // 读取所有章节内容
                foreach (var spineItem in epubBook.ReadingOrder)
                {
                    var lines = HtmlToPlainText(spineItem.Content);
                    foreach (var line in lines)
                    {
                        _originalLines.Add(line.Trim());
                    }
                }
                int width = Console.WindowWidth;
                for (int i = 0; i < _originalLines.Count; i++)
                {
                    string line = _originalLines[i];
                    _originalLineToWrappedLineMap.Add(_wrappedLines.Count);
                    if (string.IsNullOrEmpty(line))
                    {
                        _wrappedLines.Add(string.Empty);
                        _wrappedLineToOriginalLineMap.Add(i);
                        continue;
                    }
                    var sb = new StringBuilder();
                    int currentWidth = 0;
                    foreach (char c in line)
                    {
                        int charWidth = GetCharDisplayWidth(c);
                        if (currentWidth + charWidth > width)
                        {
                            _wrappedLines.Add(sb.ToString());
                            _wrappedLineToOriginalLineMap.Add(i);
                            sb.Clear();
                            currentWidth = 0;
                        }
                        sb.Append(c);
                        currentWidth += charWidth;
                    }
                    if (sb.Length > 0)
                    {
                        _wrappedLines.Add(sb.ToString());
                        _wrappedLineToOriginalLineMap.Add(i);
                    }
                }
                currentLineCount = _originalLineToWrappedLineMap[book.BookMarkLoc];
                DetectChapters(book);
                // 更新书签进度
                book.BookMarkProgress = (float)book.BookMarkLoc / TotalLineCount;
                book.LastReadTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 获取字符的显示宽度
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static int GetCharDisplayWidth(char c)
        {
            // 基于 Unicode EastAsianWidth 判断宽度
            if (char.IsControl(c))
            {
                return 0;
            }

            if (c == '\t')
            {
                return 4; // 可根据实际设定
            }

            var unicode = (int)c;

            // 简单判定：中文、全角符号等为宽字符
            if (unicode >= 0x4E00 && unicode <= 0x9FFF)
            {
                return 2; // 中日韩汉字
            }

            if (unicode >= 0xFF00 && unicode <= 0xFFEF)
            {
                return 2; // 全角符号
            }

            if (unicode >= 0x3000 && unicode <= 0x303F)
            {
                return 2; // 中文标点
            }

            return 1; // 默认半角
        }

        /// <summary>
        /// 获取当前页内容
        /// </summary>
        /// <returns></returns>
        public string GetPage()
        {
            int startLine = currentLineCount;
            if (startLine >= _wrappedLines.Count)
            {
                return string.Empty;
            }

            // 计算当前页的行数
            if (Config.Instance.ShowHelpInfo)
            {
                _pageSize = Console.WindowHeight - 5;
            }
            else
            {
                _pageSize = Console.WindowHeight - 1;
            }
            int count = Math.Min(_pageSize, _wrappedLines.Count - startLine);
            var pageLines = _wrappedLines.GetRange(startLine, count);

            return string.Join(Environment.NewLine, pageLines);
        }

        /// <summary>
        /// 翻页：下一页，更新书签
        /// </summary>
        public void NextPage(BookInfo book)
        {
            if (book == null)
            {
                return;
            }

            currentLineCount += _pageSize;
            book.BookMarkLoc = _wrappedLineToOriginalLineMap[currentLineCount];
            book.BookMarkProgress = (float)book.BookMarkLoc / TotalLineCount;
            book.LastReadTime = DateTime.Now;
        }

        /// <summary>
        /// 翻页：上一页，更新书签
        /// </summary>
        public void PrevPage(BookInfo book)
        {
            if (book == null)
            {
                return;
            }

            currentLineCount = Math.Max(0, currentLineCount - _pageSize);
            book.BookMarkLoc = _wrappedLineToOriginalLineMap[currentLineCount];
            book.BookMarkProgress = (float)book.BookMarkLoc / TotalLineCount;
            book.LastReadTime = DateTime.Now;
        }

        /// <summary>
        /// 检测章节信息
        /// </summary>
        /// <param name="book"></param>
        private void DetectChapters(BookInfo book)
        {
            _chapters.Clear();

            var regex = new Regex(@"^(第[零一二三四五六七八九十百千\d]+[章节回卷])|(^Chapter\s+\d+)", RegexOptions.Compiled);

            for (int i = 0; i < _originalLines.Count; i++)
            {
                string line = _originalLines[i].Trim();
                if (regex.IsMatch(line))
                {
                    _chapters.Add(new ChapterInfo
                    {
                        Title = line,
                        LineIndex = i
                    });
                }
            }
        }

        /// <summary>
        /// 显示章节列表，支持翻页和选择章节跳转
        /// </summary>
        /// <param name="book"></param>
        public void ShowChapters(BookInfo book)
        {
            if (_chapters.Count == 0)
            {
                Console.WriteLine("未检测到章节。");
                Thread.Sleep(1000);
                return;
            }
            // 计算当前页的行数
            if (Config.Instance.ShowHelpInfo)
            {
                _pageSize = Console.WindowHeight - 8;
            }
            else
            {
                _pageSize = Console.WindowHeight - 1;
            }
            // 当前章节索引 & 当前页码
            int currentLine = book.BookMarkLoc;
            int currentChapterIndex = _chapters.FindLastIndex(c => c.LineIndex <= currentLine);
            if (currentChapterIndex < 0)
            {
                currentChapterIndex = 0;
            }

            int chapterPage = currentChapterIndex / _pageSize;
            int selectedIndexInPage = currentChapterIndex % _pageSize;  // 当前页中选中的行号
            int totalChapterPages = (_chapters.Count + _pageSize - 1) / _pageSize;

            while (true)
            {
                Console.Clear();

                if (Config.Instance.ShowHelpInfo)
                {
                    Console.WriteLine($"章节目录 （第 {chapterPage + 1}/{totalChapterPages} 页）\n");
                }

                int start = chapterPage * _pageSize;
                int end = Math.Min(start + _pageSize, _chapters.Count);
                int count = end - start;

                for (int i = 0; i < count; i++)
                {
                    int chapterIndex = start + i;
                    var chapter = _chapters[chapterIndex];
                    if (i == selectedIndexInPage)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($">>[{i + 1}] {chapter.Title}");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        Console.WriteLine($"  [{i + 1}] {chapter.Title}");
                    }
                }

                if (Config.Instance.ShowHelpInfo)
                {
                    Console.WriteLine("\n操作说明：");
                    Console.WriteLine(" ↑/↓ 移动选择   ←/A 上一页   →/D 下一页");
                    Console.WriteLine(" Enter 跳转章节    ESC 返回");
                }

                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (selectedIndexInPage > 0)
                        {
                            selectedIndexInPage--;
                        }
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (selectedIndexInPage < (end - start - 1))
                        {
                            selectedIndexInPage++;
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        if (chapterPage > 0)
                        {
                            chapterPage--;
                            selectedIndexInPage = 0;
                        }
                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        if ((chapterPage + 1) * _pageSize < _chapters.Count)
                        {
                            chapterPage++;
                            selectedIndexInPage = 0;
                        }
                        break;

                    case ConsoleKey.Enter:
                        int globalIndex = chapterPage * _pageSize + selectedIndexInPage;
                        if (globalIndex < _chapters.Count)
                        {
                            JumpToLine(book, _chapters[globalIndex].LineIndex);
                            return;
                        }
                        break;

                    case ConsoleKey.Escape:
                        return;
                    default:
                        if (char.IsDigit(key.KeyChar))
                        {
                            int selIndex = key.KeyChar - '0' - 1;
                            int gIndex = chapterPage * _pageSize + selIndex;

                            if (selIndex >= 0 && gIndex < _chapters.Count)
                            {
                                JumpToLine(book, _chapters[gIndex].LineIndex);
                                return;
                            }
                        }
                        break;
                }
            }
        }


        /// <summary>
        /// 跳转到指定行
        /// </summary>
        /// <param name="book"></param>
        /// <param name="lineIndex"></param>
        public void JumpToLine(BookInfo book, int lineIndex)
        {
            if (_originalLineToWrappedLineMap != null && lineIndex >= 0 &&
                lineIndex < _originalLineToWrappedLineMap.Count)
            {
                currentLineCount = _originalLineToWrappedLineMap[lineIndex];
                book.BookMarkLoc = lineIndex;
                book.BookMarkProgress = (float)book.BookMarkLoc / TotalLineCount;
                book.LastReadTime = DateTime.Now;
            }
        }

        private string[] HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return new string[0];
            }

            // 1. 移除 script/style 标签及其内容
            html = Regex.Replace(html, @"<script[^>]*?>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*?>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<head[^>]*?>[\s\S]*?</head>", string.Empty, RegexOptions.IgnoreCase);

            // 2. 替换常用段落/换行标签为换行符（注意顺序）
            html = Regex.Replace(html, @"(<br\s*/?>|</p>|</div>|</h\d>)", "\n", RegexOptions.IgnoreCase);

            // 3. 移除所有其他 HTML 标签
            html = Regex.Replace(html, @"<[^>]+>", string.Empty, RegexOptions.IgnoreCase);

            // 4. 解码 HTML 实体
            html = System.Net.WebUtility.HtmlDecode(html);

            // 5. 去掉连续空行（3 行以上的情况）
            html = Regex.Replace(html, @"\n{3,}", "\n\n");
            return html.Split('\n');
        }


    }
}