using Moyu.Models;
using System.Collections.Generic;

namespace Moyu.Services
{
    internal interface IBookSearch
    {
        List<BookInfo> SearchBooks(string keyword);
    }
}
