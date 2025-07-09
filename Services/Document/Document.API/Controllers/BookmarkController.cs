using Document.API.Constants;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class BookmarkController : ControllerBase
    {
        private readonly IBookmarkService _bookmarkService;

        public BookmarkController(IBookmarkService bookmarkService)
        {
            _bookmarkService = bookmarkService;
        }

        [HttpPost(ApiEndPointConstant.Bookmark.AddBookmark)]
        public async Task<IActionResult> AddBookmark([FromRoute(Name = "documentVersionId")] string documentVersionId, string userId)
        {
            await _bookmarkService.AddBookmarkAsync(documentVersionId, userId);
            return Ok(ApiResponse<object>.Success(null, "Document bookmarked successfully.", 200));
        }

        [HttpDelete(ApiEndPointConstant.Bookmark.RemoveBookmark)]
        public async Task<IActionResult> RemoveBookmark([FromRoute(Name = "documentVersionId")] string documentVersionId, string userId)
        {
            await _bookmarkService.RemoveBookmarkAsync(documentVersionId, userId);
            return Ok(ApiResponse<object>.Success(null, "Bookmark removed successfully.", 200));
        }

        [HttpGet(ApiEndPointConstant.Bookmark.GetBookmarks)]
        public async Task<IActionResult> GetBookmarks(string userId, int pageNumber = 1, int pageSize = 10)
        {
            var result = await _bookmarkService.GetBookmarksAsync(userId, pageNumber, pageSize);
            return Ok(ApiResponse<object>.Success(result));
        }
    }
}