using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;
using static Document.API.Services.Interfaces.IFolderAwareApprovalService; // ✅ FOLDER-AWARE: For ApprovalReviewResponse

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

        /// <summary>
        /// ✅ FOLDER-AWARE: Review document (approve/reject) with folder-aware logic and complete Kernel Memory integration
        /// </summary>
        Task<ApprovalReviewResponse> ReviewDocument(string versionId, ReviewDocumentRequest reviewDocumentRequest);
        Task ClaimDocumentForReviewAsync(string versionId);
        Task ReleaseClaimAsync(string versionId);
        Task KeepClaimAliveAsync(string versionId);
        Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId);
        Task ProcessExpiredSubmissionsAsync(); // BR-214: Auto-reject expired submissions
        Task ProcessInactiveClaimsAsync(); // Auto-release inactive claims

        /// <summary>
        /// Archive an approved document manually (Manager only)
        /// Changes status from Approved to Archived and removes from Kernel Memory
        /// </summary>
        /// <param name="versionId">Version ID to archive</param>
        /// <param name="request">Archive request with reason</param>
        Task ArchiveDocumentAsync(string versionId, ArchiveDocumentRequest request);

        /// <summary>
        /// Permanently delete an archived document (Manager only)
        /// Removes from database, storage, and Kernel Memory
        /// </summary>
        /// <param name="versionId">Archived version ID to delete</param>
        /// <param name="request">Delete request with confirmation</param>
        Task DeleteArchivedDocumentAsync(string versionId, DeleteArchivedDocumentRequest request);

        /// <summary>
        /// Permanently delete an entire document with all its versions (Manager only)
        /// Removes all versions from database, storage, and Kernel Memory
        /// </summary>
        /// <param name="documentId">Document ID to delete entirely</param>
        /// <param name="request">Delete request with confirmation</param>
        Task DeleteEntireDocumentAsync(string documentId, DeleteArchivedDocumentRequest request);
    }
}
