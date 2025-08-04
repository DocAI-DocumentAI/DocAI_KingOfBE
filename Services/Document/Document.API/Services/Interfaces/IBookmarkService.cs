using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;

namespace Document.API.Services.Interfaces
{
    public interface IBookmarkService
    {
        Task AddBookmarkAsync(string documentId);
        Task RemoveBookmarkAsync(string documentId);
        Task<IPaginate<BookmarkResponse>> GetBookmarksAsync(int pageNumber, int pageSize);
    }
}