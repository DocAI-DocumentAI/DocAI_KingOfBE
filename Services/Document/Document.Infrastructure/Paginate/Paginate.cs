using System;
using System.Collections.Generic;

namespace Document.Infrastructure.Paginate;

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

    public Paginate(IEnumerable<TResult> items, int page, int size, int total)
    {
        Page = page;
        Size = size;
        Total = total;
        TotalPages = (int)Math.Ceiling(total / (double)size);
        Items = new List<TResult>(items);
    }
}