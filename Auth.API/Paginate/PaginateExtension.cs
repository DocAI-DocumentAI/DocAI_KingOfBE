using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Paginate;

public static class PaginateExtension
{
    public static async Task<IPaginate<T>> ToPaginateAsync<T>(this IQueryable<T> query, int page, int size, int firstPage = 1)
    {
        if (firstPage > page)
        {
            throw new AggregateException($"page ({page}) must be greater than or equal to firstPage ({firstPage})");
        }

        var total = await query.CountAsync();
        var items = await query.Skip((page - firstPage) * size).Take(size).ToListAsync();
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return new Paginate<T>
        {
            Page = page,
            Size = size,
            Total = total,
            Items = items,
            TotalPages = totalPages
        };
    }
}