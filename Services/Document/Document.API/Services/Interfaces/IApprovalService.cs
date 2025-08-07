using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;

namespace Document.API.Services.Interfaces
{
    public interface IApprovalService
    {
        Task SubmitForApprovalAsync(string versionId);
        Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize);
        Task ReviewDocument(string versionId, ReviewDocumentRequest reviewDocumentRequest);
        Task ClaimDocumentForReviewAsync(string versionId);
        Task ReleaseClaimAsync(string versionId);
        Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId);
    }
}
