using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Microsoft.AspNetCore.Mvc;
using static Document.API.Attributes.AuthorizeExtensions;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for folder-aware approval operations
    /// Provides approval workflows that maintain folder context during status changes
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FolderAwareApprovalController : ControllerBase
    {
        private readonly IFolderAwareApprovalService _folderAwareApprovalService;
        private readonly ILogger<FolderAwareApprovalController> _logger;

        public FolderAwareApprovalController(
            IFolderAwareApprovalService folderAwareApprovalService,
            ILogger<FolderAwareApprovalController> logger)
        {
            _folderAwareApprovalService = folderAwareApprovalService;
            _logger = logger;
        }

        /// <summary>
        /// Submit document for approval while maintaining folder context
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="targetFolderId">Optional target folder for approved document</param>
        /// <returns>Submission result with folder information</returns>
        [HttpPost(ApiEndPointConstant.FolderAwareApproval.SubmitForApproval)]
        [CustomAuthorize(Roles = new[] { Roles.Editor })]
        [ProducesResponseType(typeof(ApiResponse<ApprovalSubmissionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SubmitForApproval(
            [FromRoute] string versionId,
            [FromQuery] string? targetFolderId = null)
        {
            try
            {
                var result = await _folderAwareApprovalService.SubmitForApprovalAsync(versionId, targetFolderId);
                return Ok(ApiResponse<ApprovalSubmissionResponse>.Success(result, "Document submitted for approval successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("DOCUMENT_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_OPERATION", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting document {VersionId} for approval", versionId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while submitting document for approval"));
            }
        }

        /// <summary>
        /// Review document (approve/reject) with folder-aware logic
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="request">Review request</param>
        /// <returns>Review result with folder information</returns>
        [HttpPost(ApiEndPointConstant.FolderAwareApproval.ReviewDocument)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<ApprovalReviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReviewDocument(
            [FromRoute] string versionId,
            [FromBody] ReviewDocumentRequest request)
        {
            try
            {
                var result = await _folderAwareApprovalService.ReviewDocumentAsync(versionId, request);
                return Ok(ApiResponse<ApprovalReviewResponse>.Success(result, 
                    $"Document {result.Decision.ToLower()} successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("DOCUMENT_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_OPERATION", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing document {VersionId}", versionId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while reviewing document"));
            }
        }

        /// <summary>
        /// Get approval queue with folder context
        /// </summary>
        /// <param name="departmentId">Department ID filter</param>
        /// <param name="folderId">Folder ID filter</param>
        /// <param name="includeSubfolders">Include documents from subfolders</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Approval queue with folder information</returns>
        [HttpGet(ApiEndPointConstant.FolderAwareApproval.GetApprovalQueue)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<FolderAwareApprovalQueueResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueue(
            [FromQuery] string? departmentId = null,
            [FromQuery] string? folderId = null,
            [FromQuery] bool includeSubfolders = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _folderAwareApprovalService.GetApprovalQueueAsync(
                    departmentId, folderId, includeSubfolders, page, pageSize);
                
                return Ok(ApiResponse<FolderAwareApprovalQueueResponse>.Success(result, 
                    $"Retrieved {result.PendingDocuments.Count} pending documents"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval queue");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting approval queue"));
            }
        }

        /// <summary>
        /// Get approval history for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Approval history with folder context</returns>
        [HttpGet(ApiEndPointConstant.FolderAwareApproval.GetFolderApprovalHistory)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<FolderApprovalHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderApprovalHistory(
            [FromRoute] string folderId,
            [FromQuery] bool includeSubfolders = false,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _folderAwareApprovalService.GetFolderApprovalHistoryAsync(
                    folderId, includeSubfolders, fromDate, toDate, page, pageSize);
                
                return Ok(ApiResponse<FolderApprovalHistoryResponse>.Success(result, 
                    $"Retrieved {result.ApprovalHistory.Count} approval history entries"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval history for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting approval history"));
            }
        }

        /// <summary>
        /// Get approval statistics for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <returns>Approval statistics</returns>
        [HttpGet(ApiEndPointConstant.FolderAwareApproval.GetFolderApprovalStats)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<FolderApprovalStatistics>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderApprovalStatistics(
            [FromRoute] string folderId,
            [FromQuery] bool includeSubfolders = false,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _folderAwareApprovalService.GetFolderApprovalStatisticsAsync(
                    folderId, includeSubfolders, fromDate, toDate);
                
                return Ok(ApiResponse<FolderApprovalStatistics>.Success(result, "Approval statistics retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statistics for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting approval statistics"));
            }
        }

        /// <summary>
        /// Bulk approve/reject documents with folder context
        /// </summary>
        /// <param name="requests">List of bulk approval requests</param>
        /// <returns>Bulk approval results</returns>
        [HttpPost(ApiEndPointConstant.FolderAwareApproval.BulkReviewDocuments)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<BulkApprovalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkReviewDocuments([FromBody] List<BulkReviewRequest> requests)
        {
            try
            {
                if (!requests.Any())
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "At least one review request is required"));
                }

                var result = await _folderAwareApprovalService.BulkReviewDocumentsAsync(requests);
                
                return Ok(ApiResponse<BulkApprovalResponse>.Success(result, 
                    $"Processed {result.TotalProcessed} documents: {result.SuccessCount} successful, {result.FailureCount} failed"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk review");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while processing bulk review"));
            }
        }

        /// <summary>
        /// Get documents pending approval in a specific folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders</param>
        /// <returns>List of pending documents</returns>
        [HttpGet(ApiEndPointConstant.FolderAwareApproval.GetPendingDocumentsInFolder)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<DocumentApprovalInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPendingDocumentsInFolder(
            [FromRoute] string folderId,
            [FromQuery] bool includeSubfolders = false)
        {
            try
            {
                var result = await _folderAwareApprovalService.GetPendingDocumentsInFolderAsync(folderId, includeSubfolders);
                return Ok(ApiResponse<List<DocumentApprovalInfo>>.Success(result, 
                    $"Found {result.Count} pending documents in folder"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending documents in folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting pending documents"));
            }
        }
    }
}
