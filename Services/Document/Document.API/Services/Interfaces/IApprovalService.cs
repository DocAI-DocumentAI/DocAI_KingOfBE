using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;

namespace Document.API.Services.Interfaces
{
    public interface IApprovalService
    {
        Task SubmitForApprovalAsync(string versionId);

        /// <summary>
        /// Get approval queue with enhanced filtering and summary statistics
        /// </summary>
        Task<ApprovalQueueSummaryResponse> GetApprovalQueueAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize);

        /// <summary>
        /// Backward compatibility method - returns only the paginated documents without statistics
        /// </summary>
        Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueLegacyAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize);

        Task ReviewDocument(string versionId, ReviewDocumentRequest reviewDocumentRequest);
        Task ClaimDocumentForReviewAsync(string versionId);
        Task ReleaseClaimAsync(string versionId);
        Task KeepClaimAliveAsync(string versionId);
        Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId);
        Task ProcessExpiredSubmissionsAsync(); // BR-214: Auto-reject expired submissions
        Task ProcessInactiveClaimsAsync(); // Auto-release inactive claims
    }
}
