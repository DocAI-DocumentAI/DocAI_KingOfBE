using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for managing user document bookmarks
    /// </summary>
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

        /// <summary>
        /// Add a document to user's bookmarks
        /// </summary>
        /// <param name="documentId">The ID of the document to bookmark</param>
        /// <returns>Success confirmation</returns>
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

        /// <summary>
        /// Remove a document from user's bookmarks
        /// </summary>
        /// <param name="documentId">The ID of the document to remove from bookmarks</param>
        /// <returns>Success confirmation</returns>
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

        /// <summary>
        /// Get user's bookmarked documents with pagination
        /// </summary>
        /// <param name="pageNumber">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 10)</param>
        /// <returns>Paginated list of user's bookmarked documents</returns>
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