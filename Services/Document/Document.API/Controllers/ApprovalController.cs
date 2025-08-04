using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
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
        public async Task<IActionResult> SubmitDocument([FromRoute(Name = "id")] string documentId, string userId)
        {
            await _approvalService.SubmitForApprovalAsync(documentId, userId);
            return Ok(ApiResponse<object>.Success(null, "Document submited successfully", 200));
        }

        [HttpPost(ApiEndPointConstant.Approval.ApproveOrReject)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveOrRejectDocument([FromRoute(Name = "id")] string documentId, [FromBody] ReviewDocumentRequest request, string userId)
        {
            await _approvalService.ReviewDocument(documentId, request, userId);
            return Ok(ApiResponse<object>.Success(null, "Document approved successfully", 200));
        }

        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueue)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<PendingDocumentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueue(string departmentId, [FromQuery] ApprovalQueueFilter filter, int pageNumber = 1, int pageSize = 10)
        {
            var result = await _approvalService.GetApprovalQueueAsync(departmentId, filter, pageNumber, pageSize);
            return Ok(ApiResponse<object>.Success(result, "Approval queue retrieved successfully", 200));
        }

        [HttpPost(ApiEndPointConstant.Approval.Claim)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ClaimDocument([FromRoute(Name = "id")] string documentId, string userId)
        {
            await _approvalService.ClaimDocumentForReviewAsync(documentId, userId);
            return Ok(ApiResponse<object>.Success(null, "Document claimed successfully", 200));
        }

        [HttpPost(ApiEndPointConstant.Approval.ReleaseClaim)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReleaseClaimDocument([FromRoute(Name = "id")] string documentId, string userId)
        {
            await _approvalService.ReleaseClaimAsync(documentId, userId);
            return Ok(ApiResponse<object>.Success(null, "Document claim released successfully", 200));
        }

        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueueDetail)]
        [CustomAuthorize(Roles = new[] { Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<PendingDocumentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalQueueDetail([FromRoute(Name = "id")] string versionId)
        {
            var result = await _approvalService.GetApprovalQueueDetailAsync(versionId);
            return Ok(ApiResponse<object>.Success(result, "Approval queue detail retrieved successfully", 200));
        }
    }
}
