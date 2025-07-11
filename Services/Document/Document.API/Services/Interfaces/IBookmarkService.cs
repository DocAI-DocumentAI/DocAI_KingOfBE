using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;

namespace Document.API.Services.Interfaces
{
    public interface IBookmarkService
    {
        Task AddBookmarkAsync(string documentVersionId, string userId);
        Task RemoveBookmarkAsync(string documentVersionId, string userId);
        Task<IPaginate<BookmarkResponse>> GetBookmarksAsync(string userId, int pageNumber, int pageSize);
    }
}