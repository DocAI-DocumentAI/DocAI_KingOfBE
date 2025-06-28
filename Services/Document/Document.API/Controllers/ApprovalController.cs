using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
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
        public async Task<IActionResult> SubmitDocument([FromRoute(Name = "id")] string documentId, string userId)
        {
            await _approvalService.SubmitForApprovalAsync(documentId, userId);
            return Ok(ApiResponse<object>.Success(null, "Document submited successfully", 200));
        }

        [HttpPost(ApiEndPointConstant.Approval.ApproveOrReject)]
        public async Task<IActionResult> ApproveOrRejectDocument([FromRoute(Name = "id")] string documentId, [FromBody] ReviewDocumentRequest request, string userId)
        {
            await _approvalService.ReviewDocument(documentId, request, userId);
            return Ok(ApiResponse<object>.Success(null, "Document approved successfully", 200));
        }

        [HttpGet(ApiEndPointConstant.Approval.GetApprovalQueue)]
        public async Task<IActionResult> GetApprovalQueue(string departmentId, int pageNumber = 1, int pageSize = 10)
        {
            await _approvalService.GetApprovalQueueAsync(departmentId, pageNumber, pageSize);
            return Ok(ApiResponse<object>.Success(null, "Document approved successfully", 200));
        }
    }
}
