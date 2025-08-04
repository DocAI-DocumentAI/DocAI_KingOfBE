using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class BookmarkController : ControllerBase
    {
        private readonly IBookmarkService _bookmarkService;

        public BookmarkController(IBookmarkService bookmarkService)
        {
            _bookmarkService = bookmarkService;
        }

        [HttpPost(ApiEndPointConstant.Bookmark.AddBookmark)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddBookmark([FromRoute(Name = "documentId")] string documentId)
        {
            await _bookmarkService.AddBookmarkAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Document bookmarked successfully.", 200));
        }

        [HttpDelete(ApiEndPointConstant.Bookmark.RemoveBookmark)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveBookmark([FromRoute(Name = "documentId")] string documentId)
        {
            await _bookmarkService.RemoveBookmarkAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Bookmark removed successfully.", 200));
        }

        [HttpGet(ApiEndPointConstant.Bookmark.GetBookmarks)]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<BookmarkResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBookmarks(int pageNumber = 1, int pageSize = 10)
        {
            var result = await _bookmarkService.GetBookmarksAsync(pageNumber, pageSize);
            return Ok(ApiResponse<object>.Success(result));
        }
    }
}