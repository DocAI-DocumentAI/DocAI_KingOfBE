using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;

namespace Document.API.Services.Interfaces
{
    public interface IApprovalService
    {
        Task SubmitForApprovalAsync(string versionId, string userId);
        Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueAsync(string departmentId, Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize);
        Task ReviewDocument(string versionId, ReviewDocumentRequest reviewDocumentRequest, string userId);
        Task ClaimDocumentForReviewAsync(string versionId, string userId);
        Task ReleaseClaimAsync(string versionId, string userId);
        Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId);
    }
}
