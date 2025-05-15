using System.Collections.Generic;

namespace Notification.Infrastructure.Paginate;

public interface IPaginate<TResult>
{
    int Size { get; }
    int Page { get; }
    int Total { get; }
    int TotalPages { get; }
    IList<TResult> Items { get; }
}