using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Infrastructure.Paginate
{
    public class Paginate<TResult> : IPaginate<TResult>
    {
        public int Size { get; set; }
        public int Page { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public IList<TResult> Items { get; set; }

        public Paginate()
        {
            Items = Array.Empty<TResult>();
        }

        public Paginate(IList<TResult> items, int pageNumber, int pageSize, int totalCount)
        {
            Items = items;
            Page = pageNumber;
            Size = pageSize;
            Total = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        }
    }
}
