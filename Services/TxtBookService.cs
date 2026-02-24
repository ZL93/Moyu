using Moyu.Models;
using Moyu.Utils;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Moyu.Services
{
    /// <summary>
    /// TXT书籍服务 - 整本加载模式
    /// </summary>
    public class TxtBookService : WholeBookService
    {
        public override BookInfo GetBookInfo(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            return new BookInfo
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath),
                Format = BookFormatEnum.Txt,
                CurrentReadOriginalLine = 0,
                CurrentChapterIndex = 0
            };
        }

        public override async Task<BookInfo> GetBookInfoAsync(string filePath)
            => await Task.Run(() => GetBookInfo(filePath));

        public override void LoadBook(BookInfo book)
        {
            OriginalLines = TextFileReader.ReadTextFileLines(book.FilePath);
            base.LoadBook(book);

            if (AutoDetectChapters)
            {
                DetectChapters();
            }
        }

        protected override void UpdateProgress()
        {
            if (CurrentBook == null || OriginalLines.Count == 0)
            {
                return;
            }

            CurrentBook.MarkProgress = (float)CurrentReadOriginalLine / OriginalLines.Count;
        }

        private void DetectChapters()
        {
            Chapters.Clear();

            var regex = new Regex(
                @"(^|\s)(第[零一二三四五六七八九十百千万\d]+[章节回卷篇集部])|" +
                @"(^|\s)(Chapter\s+\d+)|" +
                @"(^|\s)(\d+\.\s)|" +
                @"(^|\s)(【[^】]+】)|" +
                @"(^|\s)(〔[^〕]+〕)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase
            );

            for (int i = 0; i < OriginalLines.Count; i++)
            {
                string line = OriginalLines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line) && regex.IsMatch(line))
                {
                    Chapters.Add(new ChapterInfo
                    {
                        Title = line.Length > 50 ? line.Substring(0, 50) + "..." : line,
                        LineIndex = i,
                        ChapterIndex = Chapters.Count
                    });
                }
            }

            if (Chapters.Count == 0 && OriginalLines.Count > 0)
            {
                Chapters.Add(new ChapterInfo
                {
                    Title = "全文",
                    LineIndex = 0,
                    ChapterIndex = 0
                });
            }
        }
    }
}