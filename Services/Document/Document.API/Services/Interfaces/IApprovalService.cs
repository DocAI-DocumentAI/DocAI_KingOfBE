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
        /// ✅ NEW: Manually archive an approved document
        /// BR-300: Managers can manually archive approved documents within their department
        /// </summary>
        Task<ArchiveDocumentResponse> ArchiveDocumentAsync(string versionId, ArchiveDocumentRequest request);

        /// <summary>
        /// ✅ NEW: Permanently delete an archived document
        /// BR-301: Managers can permanently delete archived documents within their department
        /// </summary>
        Task<DeleteArchivedDocumentResponse> DeleteArchivedDocumentAsync(string versionId, DeleteArchivedDocumentRequest request);

        /// <summary>
        /// Get approval logs for managers to view department approval history with filtering
        /// </summary>
        Task<IPaginate<ManagerApprovalLogResponse>> GetApprovalLogsAsync(Document.Infrastructure.Filter.ManagerApprovalLogFilter filter, int pageNumber, int pageSize);
        
        /// <summary>
        /// ✅ UTILITY: Fix circular and orphaned replacement relationships in the database
        /// This method should be called to clean up existing bad data
        /// </summary>
        Task<string> FixReplacementRelationshipsAsync();
    }
}
