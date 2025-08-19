using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Folder;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Microsoft.AspNetCore.Mvc;
using static Document.API.Attributes.AuthorizeExtensions;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for folder-based document operations
    /// Provides document browsing, searching, and management within folder context
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FolderDocumentController : ControllerBase
    {
        private readonly IFolderDocumentService _folderDocumentService;
        private readonly ILogger<FolderDocumentController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FolderDocumentController(
            IFolderDocumentService folderDocumentService,
            ILogger<FolderDocumentController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _folderDocumentService = folderDocumentService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Browse folder contents (documents and subfolders)
        /// </summary>
        /// <param name="request">Browse request parameters</param>
        /// <returns>Folder contents with documents and subfolders</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.BrowseFolderContents)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderContentsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BrowseFolderContents([FromQuery] FolderBrowseRequest request)
        {
            try
            {
                var result = await _folderDocumentService.BrowseFolderContentsAsync(request);
                return Ok(ApiResponse<FolderContentsResponse>.Success(result, "Folder contents retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing folder contents");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while browsing folder contents"));
            }
        }

        /// <summary>
        /// Search documents within a specific folder
        /// </summary>
        /// <param name="request">Search request parameters</param>
        /// <returns>Search results within folder context</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.SearchFolderDocuments)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderDocumentSearchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchDocumentsInFolder([FromQuery] FolderDocumentSearchRequest request)
        {
            try
            {
                var result = await _folderDocumentService.SearchDocumentsInFolderAsync(request);
                return Ok(ApiResponse<FolderDocumentSearchResponse>.Success(result, 
                    $"Found {result.TotalResults} documents in {result.ExecutionTimeMs}ms"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents in folder");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while searching documents"));
            }
        }

        /// <summary>
        /// Get ALL documents in a folder with filtering and sorting (no pagination)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="page">Page number (ignored - returns all documents)</param>
        /// <param name="pageSize">Page size (ignored - returns all documents)</param>
        /// <param name="status">Document status filter</param>
        /// <param name="documentTypeId">Document type filter</param>
        /// <param name="sortBy">Sort field</param>
        /// <param name="sortDirection">Sort direction</param>
        /// <returns>All documents in folder</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.GetFolderDocuments)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderDocumentSearchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderDocuments(
            [FromRoute] string folderId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? documentTypeId = null,
            [FromQuery] string? sortBy = "LastUpdatedTime",
            [FromQuery] string? sortDirection = "desc")
        {
            try
            {
                // ✅ FIXED: Ignore pagination parameters - always return all documents
                var result = await _folderDocumentService.GetFolderDocumentsAsync(
                    folderId, page, pageSize, status, documentTypeId, sortBy, sortDirection);

                return Ok(ApiResponse<FolderDocumentSearchResponse>.Success(result,
                    $"Retrieved all {result.Documents.Count} documents from folder"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting folder documents"));
            }
        }

        /// <summary>
        /// Get recent documents across accessible folders for current user
        /// </summary>
        /// <param name="limit">Maximum number of documents to return</param>
        /// <param name="departmentId">Filter by specific department (optional)</param>
        /// <returns>List of recent documents with folder context</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.GetRecentDocuments)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<DocumentSearchResultResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRecentDocuments(
            [FromQuery] int limit = 10,
            [FromQuery] string? departmentId = null)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var result = await _folderDocumentService.GetRecentDocumentsAsync(userId, userDepartmentId, limit, departmentId);
                return Ok(ApiResponse<List<DocumentSearchResultResponse>>.Success(result, 
                    $"Retrieved {result.Count} recent documents"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent documents");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting recent documents"));
            }
        }

        /// <summary>
        /// Get document statistics for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders in statistics</param>
        /// <returns>Document statistics</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.GetFolderDocumentStats)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderDocumentStatistics>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderDocumentStatistics(
            [FromRoute] string folderId,
            [FromQuery] bool includeSubfolders = false)
        {
            try
            {
                var result = await _folderDocumentService.GetFolderDocumentStatisticsAsync(folderId, includeSubfolders);
                return Ok(ApiResponse<FolderDocumentStatistics>.Success(result, "Statistics retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder statistics for {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting folder statistics"));
            }
        }

        /// <summary>
        /// Move document to a different folder
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <param name="targetFolderId">Target folder ID</param>
        /// <returns>Success status</returns>
        [HttpPut(ApiEndPointConstant.FolderDocument.MoveDocument)]
        [CustomAuthorize(Roles = new[] { Roles.Editor, Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MoveDocumentToFolder(
            [FromRoute] string documentVersionId,
            [FromQuery] string targetFolderId)
        {
            try
            {
                if (string.IsNullOrEmpty(targetFolderId))
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Target folder ID is required"));
                }

                var success = await _folderDocumentService.MoveDocumentToFolderAsync(documentVersionId, targetFolderId);
                
                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { DocumentVersionId = documentVersionId, TargetFolderId = targetFolderId }, 
                        "Document moved successfully"));
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        ApiResponse<object>.Error("MOVE_FAILED", "Failed to move document"));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("DOCUMENT_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving document {DocumentVersionId} to folder {FolderId}", documentVersionId, targetFolderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while moving document"));
            }
        }

        /// <summary>
        /// Get folder path for a document (breadcrumb navigation)
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <returns>Folder breadcrumb path from root to document's folder</returns>
        [HttpGet(ApiEndPointConstant.FolderDocument.GetDocumentFolderPath)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<FolderBreadcrumbResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentFolderPath([FromRoute] string documentVersionId)
        {
            try
            {
                var result = await _folderDocumentService.GetDocumentFolderPathAsync(documentVersionId);

                if (result == null || !result.Any())
                {
                    return NotFound(ApiResponse<object>.Error("DOCUMENT_NOT_FOUND",
                        "Document not found or not located in any folder"));
                }

                return Ok(ApiResponse<List<FolderBreadcrumbResponse>>.Success(result,
                    "Document folder path retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("DOCUMENT_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder path for document {DocumentVersionId}", documentVersionId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving document folder path"));
            }
        }
    }
}
