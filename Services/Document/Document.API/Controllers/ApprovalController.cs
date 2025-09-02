using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
using static Document.API.Services.Interfaces.IFolderAwareApprovalService; // ✅ FOLDER-AWARE: For ApprovalReviewResponse
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    /// <summary>
    /// ✅ ENHANCED: Approval controller with complete folder-aware functionality and Kernel Memory integration
    /// Handles document approval workflows with folder management, versioning, replacement, and semantic search indexing
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class ApprovalController : ControllerBase
    {
        private readonly IApprovalService _approvalService;

        public ApprovalController(IApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        [HttpPost(ApiEndPointConstant.Approval.Submit)]
        [CustomAuthorize(Roles = new[] { Roles.Editor })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SubmitDocument([FromRoute(Name = "id")] string documentId)
        {
            await _approvalService.SubmitForApprovalAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Document submited successfully", 200));
        }

        /// <summary>
        /// ✅ FOLDER-AWARE: Review document (approve/reject) with folder-aware logic and complete Kernel Memory integration
        /// </summary>
        [HttpPost(ApiEndPointConstant.Approval.ApproveOrReject)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<ApprovalReviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveOrRejectDocument([FromRoute(Name = "id")] string documentId, [FromBody] ReviewDocumentRequest request)
        {
            var result = await _approvalService.ReviewDocument(documentId, request);
            return Ok(ApiResponse<ApprovalReviewResponse>.Success(result,
                $"Document {result.Decision.ToLower()} successfully"));
        }

        /// <summary>
        /// Get enhanced approval queue with comprehensive filtering and summary statistics
        /// </summary>
        /// <param name="filter">Enhanced filter with multiple criteria</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10, max: 100)</param>
        /// <returns>Approval queue with documents and statistics</returns>
        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueue)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<ApprovalQueueSummaryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueue(
            [FromQuery] ApprovalQueueFilter filter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_PAGE_NUMBER", "Page number must be greater than 0", 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_PAGE_SIZE", "Page size must be between 1 and 100", 400));
            }

            // Validate date range if provided
            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_DATE_RANGE", "From date cannot be greater than to date", 400));
            }

            try
            {
                var result = await _approvalService.GetApprovalQueueAsync(filter, pageNumber, pageSize);
                return Ok(ApiResponse<ApprovalQueueSummaryResponse>.Success(result, "Approval queue retrieved successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving the approval queue", 500));
            }
        }

        /// <summary>
        /// Get approval queue (legacy endpoint for backward compatibility)
        /// </summary>
        /// <param name="filter">Filter criteria</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paginated list of pending documents</returns>
        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueue + "/legacy")]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<PendingDocumentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueueLegacy(
            [FromQuery] ApprovalQueueFilter filter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _approvalService.GetApprovalQueueLegacyAsync(filter, pageNumber, pageSize);
                return Ok(ApiResponse<IPaginate<PendingDocumentResponse>>.Success(result, "Approval queue retrieved successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving the approval queue", 500));
            }
        }

        [HttpPost(ApiEndPointConstant.Approval.Claim)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ClaimDocument([FromRoute(Name = "id")] string documentId)
        {
            await _approvalService.ClaimDocumentForReviewAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Document claimed successfully", 200));
        }

        [HttpPost(ApiEndPointConstant.Approval.ReleaseClaim)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReleaseClaimDocument([FromRoute(Name = "id")] string documentId)
        {
            await _approvalService.ReleaseClaimAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Document claim released successfully", 200));
        }

        [HttpPatch(ApiEndPointConstant.Approval.KeepClaimAlive)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> KeepClaimAlive([FromRoute(Name = "id")] string documentId)
        {
            await _approvalService.KeepClaimAliveAsync(documentId);
            return Ok(ApiResponse<object>.Success(null, "Document claim kept alive successfully", 200));
        }

        /// <summary>
        /// Get detailed information for a specific document in the approval queue
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>Complete document information for manager review</returns>
        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueueDetail)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<ApprovalQueueDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueueDetail([FromRoute(Name = "id")] string versionId)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(versionId))
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_VERSION_ID", "Version ID is required", 400));
            }

            try
            {
                var result = await _approvalService.GetApprovalQueueDetailAsync(versionId);
                return Ok(ApiResponse<ApprovalQueueDetailResponse>.Success(result, "Approval queue detail retrieved successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving the approval queue detail", 500));
            }
        }

        /// <summary>
        /// ✅ NEW: Manually archive an approved document
        /// BR-300: Managers can manually archive approved documents within their department
        /// </summary>
        [HttpPost(ApiEndPointConstant.Approval.ArchiveDocument)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<ArchiveDocumentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ArchiveDocument([FromRoute(Name = "id")] string versionId, [FromBody] ArchiveDocumentRequest request)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(versionId))
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_VERSION_ID", "Version ID is required", 400));
            }

            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Request body is required", 400));
            }

            try
            {
                var result = await _approvalService.ArchiveDocumentAsync(versionId, request);
                return Ok(ApiResponse<ArchiveDocumentResponse>.Success(result, "Document archived successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while archiving the document", 500));
            }
        }

        /// <summary>
        /// Get approval logs for managers to view department approval history with filtering
        /// </summary>
        /// <param name="filter">Filter criteria for approval logs</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10, max: 100)</param>
        /// <returns>Paginated list of approval log entries</returns>
        [HttpGet(ApiEndPointConstant.Approval.GetApprovalLogs)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<ManagerApprovalLogResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalLogs(
            [FromQuery] ManagerApprovalLogFilter filter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_PAGE_NUMBER", "Page number must be greater than 0", 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_PAGE_SIZE", "Page size must be between 1 and 100", 400));
            }

            // Validate date range if provided
            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_DATE_RANGE", "From date cannot be greater than to date", 400));
            }

            try
            {
                var result = await _approvalService.GetApprovalLogsAsync(filter, pageNumber, pageSize);
                return Ok(ApiResponse<IPaginate<ManagerApprovalLogResponse>>.Success(result, "Approval logs retrieved successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving approval logs", 500));
            }
        }

        /// <summary>
        /// ✅ NEW: Permanently delete an archived document
        /// BR-301: Managers can permanently delete archived documents within their department
        /// </summary>
        [HttpDelete(ApiEndPointConstant.Approval.DeleteArchivedDocument)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<DeleteArchivedDocumentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteArchivedDocument([FromRoute(Name = "id")] string versionId, [FromBody] DeleteArchivedDocumentRequest request)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(versionId))
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_VERSION_ID", "Version ID is required", 400));
            }

            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Request body is required", 400));
            }

            try
            {
                var result = await _approvalService.DeleteArchivedDocumentAsync(versionId, request);
                return Ok(ApiResponse<DeleteArchivedDocumentResponse>.Success(result, "Archived document deleted successfully", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while deleting the archived document", 500));
            }
        }

        /// <summary>
        /// ✅ UTILITY: Fix circular and orphaned replacement relationships in the database
        /// This endpoint should be used to clean up existing bad data
        /// </summary>
        [HttpPost("/api/document/approval/fix-replacement-relationships")]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FixReplacementRelationships()
        {
            try
            {
                var result = await _approvalService.FixReplacementRelationshipsAsync();
                return Ok(ApiResponse<string>.Success(result, "Replacement relationships cleanup completed", 200));
            }
            catch (ErrorException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse<object>.Error(ex.ErrorDetail.ErrorCode, ex.ErrorDetail.Message?.ToString() ?? "An error occurred", ex.StatusCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while fixing replacement relationships", 500));
            }
        }
    }
}
