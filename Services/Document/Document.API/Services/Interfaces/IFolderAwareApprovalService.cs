using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Folder;
using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Enhanced approval service that maintains folder context during status changes
    /// Integrates with the new folder management system while preserving approval workflows
    /// </summary>
    public interface IFolderAwareApprovalService
    {
        /// <summary>
        /// Submit document for approval while maintaining folder context
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="targetFolderId">Optional target folder for approved document</param>
        /// <returns>Submission result with folder information</returns>
        Task<ApprovalSubmissionResponse> SubmitForApprovalAsync(string versionId, string? targetFolderId = null);

        /// <summary>
        /// Review document (approve/reject) with folder-aware logic
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="request">Review request</param>
        /// <returns>Review result with folder information</returns>
        Task<ApprovalReviewResponse> ReviewDocumentAsync(string versionId, ReviewDocumentRequest request);

        /// <summary>
        /// Get approval queue with folder context
        /// </summary>
        /// <param name="departmentId">Department ID filter</param>
        /// <param name="folderId">Folder ID filter</param>
        /// <param name="includeSubfolders">Include documents from subfolders</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Approval queue with folder information</returns>
        Task<FolderAwareApprovalQueueResponse> GetApprovalQueueAsync(
            string? departmentId = null, 
            string? folderId = null, 
            bool includeSubfolders = false, 
            int page = 1, 
            int pageSize = 20);

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
        Task<FolderApprovalHistoryResponse> GetFolderApprovalHistoryAsync(
            string folderId, 
            bool includeSubfolders = false, 
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            int page = 1, 
            int pageSize = 20);

        /// <summary>
        /// Move document to appropriate system folder based on status
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <param name="newStatus">New document status</param>
        /// <param name="targetFolderId">Optional target folder (for approved documents)</param>
        /// <returns>Success status and new folder information</returns>
        Task<FolderMoveResult> MoveDocumentToStatusFolderAsync(string documentVersionId, StatusEnum newStatus, string? targetFolderId = null);

        /// <summary>
        /// Get system folder for a specific status and department
        /// </summary>
        /// <param name="status">Document status</param>
        /// <param name="departmentId">Department ID</param>
        /// <param name="isPublic">Whether document is public</param>
        /// <returns>System folder ID</returns>
        Task<string> GetSystemFolderForStatusAsync(StatusEnum status, string? departmentId, bool isPublic);

        /// <summary>
        /// Bulk approve/reject documents with folder context
        /// </summary>
        /// <param name="requests">List of bulk approval requests</param>
        /// <returns>Bulk approval results</returns>
        Task<BulkApprovalResponse> BulkReviewDocumentsAsync(List<BulkReviewRequest> requests);

        /// <summary>
        /// Get approval statistics for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <returns>Approval statistics</returns>
        Task<FolderApprovalStatistics> GetFolderApprovalStatisticsAsync(
            string folderId, 
            bool includeSubfolders = false, 
            DateTime? fromDate = null, 
            DateTime? toDate = null);

        /// <summary>
        /// Set default approval folder for a department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="folderId">Default folder ID for approved documents</param>
        /// <returns>Success status</returns>
        Task<bool> SetDepartmentDefaultApprovalFolderAsync(string departmentId, string folderId);

        /// <summary>
        /// Get documents pending approval in a specific folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders</param>
        /// <returns>List of pending documents</returns>
        Task<List<DocumentApprovalInfo>> GetPendingDocumentsInFolderAsync(string folderId, bool includeSubfolders = false);
    }



    /// <summary>
    /// Result of approval submission with folder context
    /// </summary>
    public class ApprovalSubmissionResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Document title
        /// </summary>
        public string DocumentTitle { get; set; }

        /// <summary>
        /// Previous status
        /// </summary>
        public string PreviousStatus { get; set; }

        /// <summary>
        /// New status
        /// </summary>
        public string NewStatus { get; set; }

        /// <summary>
        /// Source folder information
        /// </summary>
        public FolderSummaryResponse? SourceFolder { get; set; }

        /// <summary>
        /// Target folder information (where document was moved for approval)
        /// </summary>
        public FolderSummaryResponse? TargetFolder { get; set; }

        /// <summary>
        /// Submission timestamp
        /// </summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>
        /// User who submitted
        /// </summary>
        public string SubmittedBy { get; set; }

        /// <summary>
        /// Expected approval deadline
        /// </summary>
        public DateTime? ApprovalDeadline { get; set; }
    }

    /// <summary>
    /// Result of approval review with folder context
    /// </summary>
    public class ApprovalReviewResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Document title
        /// </summary>
        public string DocumentTitle { get; set; }

        /// <summary>
        /// Review decision (Approved/Rejected)
        /// </summary>
        public string Decision { get; set; }

        /// <summary>
        /// Review comments
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Previous status
        /// </summary>
        public string PreviousStatus { get; set; }

        /// <summary>
        /// New status
        /// </summary>
        public string NewStatus { get; set; }

        /// <summary>
        /// Source folder (where document was during review)
        /// </summary>
        public FolderSummaryResponse? SourceFolder { get; set; }

        /// <summary>
        /// Target folder (where document was moved after review)
        /// </summary>
        public FolderSummaryResponse? TargetFolder { get; set; }

        /// <summary>
        /// Review timestamp
        /// </summary>
        public DateTime ReviewedAt { get; set; }

        /// <summary>
        /// User who reviewed
        /// </summary>
        public string ReviewedBy { get; set; }

        /// <summary>
        /// Approval log ID
        /// </summary>
        public string ApprovalLogId { get; set; }
    }

    /// <summary>
    /// Folder-aware approval queue response
    /// </summary>
    public class FolderAwareApprovalQueueResponse
    {
        /// <summary>
        /// Documents pending approval
        /// </summary>
        public List<DocumentApprovalInfo> PendingDocuments { get; set; } = new List<DocumentApprovalInfo>();

        /// <summary>
        /// Total number of pending documents
        /// </summary>
        public int TotalPending { get; set; }

        /// <summary>
        /// Current page
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Filter information
        /// </summary>
        public ApprovalQueueFilters? AppliedFilters { get; set; }

        /// <summary>
        /// Folders containing pending documents
        /// </summary>
        public List<FolderSummaryResponse> FoldersWithPendingDocuments { get; set; } = new List<FolderSummaryResponse>();
    }

    /// <summary>
    /// Document approval information with folder context (enhanced to match PendingDocumentResponse)
    /// </summary>
    public class DocumentApprovalInfo
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Document file ID (parent document)
        /// </summary>
        public string DocumentFileId { get; set; } = string.Empty;

        /// <summary>
        /// Version ID (same as Id for backward compatibility)
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Version name
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// User who submitted (ID)
        /// </summary>
        public string SubmittedBy { get; set; }

        /// <summary>
        /// ✅ ADDED: Name of user who submitted (enriched)
        /// </summary>
        public string? SubmittedByName { get; set; }

        /// <summary>
        /// Submission date
        /// </summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Department ID
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// ✅ ADDED: Department name (enriched)
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Document type ID
        /// </summary>
        public string DocumentTypeId { get; set; } = string.Empty;

        /// <summary>
        /// ✅ ADDED: Document type name (enriched)
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Whether document is public
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Person who signed the document
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date from
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }

        /// <summary>
        /// ✅ ADDED: Whether document is being reviewed
        /// </summary>
        public bool IsBeingReviewed { get; set; }

        /// <summary>
        /// ✅ ADDED: ID of reviewer
        /// </summary>
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// ✅ ADDED: When document was claimed
        /// </summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>
        /// ✅ ADDED: Name of reviewer (enriched)
        /// </summary>
        public string? ReviewedByName { get; set; }

        /// <summary>
        /// ✅ ADDED: Document description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// ✅ ADDED: Document summary
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// ✅ ADDED: File type/extension
        /// </summary>
        public string? FileType { get; set; }

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// ✅ ADDED: Document creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// ✅ ADDED: Last update time
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// ✅ ADDED: Owner ID
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// ✅ ADDED: Owner name (enriched)
        /// </summary>
        public string? OwnerName { get; set; }

        /// <summary>
        /// ✅ ADDED: Priority level
        /// </summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Days since submission
        /// </summary>
        public int DaysSinceSubmission { get; set; }

        /// <summary>
        /// ✅ ADDED: Whether approaching expiration
        /// </summary>
        public bool IsApproachingExpiration { get; set; }

        /// <summary>
        /// ✅ ADDED: Resubmission count
        /// </summary>
        public int ResubmissionCount { get; set; }

        /// <summary>
        /// ✅ ADDED: Previous rejection reason
        /// </summary>
        public string? PreviousRejectionReason { get; set; }

        /// <summary>
        /// Folder containing the document
        /// </summary>
        public FolderSummaryResponse? ContainingFolder { get; set; }

        /// <summary>
        /// Current folder ID where the document is located
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Target folder ID where the document will be moved when approved
        /// </summary>
        public string? TargetFolderId { get; set; }

        /// <summary>
        /// Current folder name where the document is located
        /// </summary>
        public string? FolderName { get; set; }

        /// <summary>
        /// Target folder name where the document will be moved when approved
        /// </summary>
        public string? TargetFolderName { get; set; }

        /// <summary>
        /// Approval deadline (legacy field)
        /// </summary>
        public DateTime? ApprovalDeadline { get; set; }

        /// <summary>
        /// Whether deadline is approaching (legacy field)
        /// </summary>
        public bool IsUrgent { get; set; }

        /// <summary>
        /// Current claim information (legacy field)
        /// </summary>
        public ApprovalClaimInfo? CurrentClaim { get; set; }

        /// <summary>
        /// ID of the document that this document replaces (forward relationship)
        /// </summary>
        public string? ReplacementId { get; set; }

        /// <summary>
        /// Name of the document that this document replaces (forward relationship)
        /// </summary>
        public string? ReplacementDocumentName { get; set; }

        /// <summary>
        /// Whether this document has been replaced by another document
        /// </summary>
        public bool IsReplaced { get; set; }

        /// <summary>
        /// ID of the document that replaces this document (reverse relationship)
        /// </summary>
        public string? ReplacedById { get; set; }

        /// <summary>
        /// Name of the document that replaces this document (reverse relationship)
        /// </summary>
        public string? ReplacedByDocumentName { get; set; }
    }

    /// <summary>
    /// Approval queue filters
    /// </summary>
    public class ApprovalQueueFilters
    {
        /// <summary>
        /// Department ID filter
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Folder ID filter
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Include subfolders
        /// </summary>
        public bool IncludeSubfolders { get; set; }

        /// <summary>
        /// Document type filter
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Submitted by filter
        /// </summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Urgent documents only
        /// </summary>
        public bool UrgentOnly { get; set; }
    }

    /// <summary>
    /// Approval claim information
    /// </summary>
    public class ApprovalClaimInfo
    {
        /// <summary>
        /// User who claimed the document
        /// </summary>
        public string ClaimedBy { get; set; }

        /// <summary>
        /// When the document was claimed
        /// </summary>
        public DateTime ClaimedAt { get; set; }

        /// <summary>
        /// Whether the claim is still active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Time remaining before claim expires
        /// </summary>
        public TimeSpan? TimeRemaining { get; set; }
    }

    /// <summary>
    /// Folder approval history response
    /// </summary>
    public class FolderApprovalHistoryResponse
    {
        /// <summary>
        /// Folder information
        /// </summary>
        public FolderSummaryResponse Folder { get; set; }

        /// <summary>
        /// Approval history entries
        /// </summary>
        public List<ApprovalHistoryEntry> ApprovalHistory { get; set; } = new List<ApprovalHistoryEntry>();

        /// <summary>
        /// Total number of entries
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Current page
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Date range of the history
        /// </summary>
        public DateRange? DateRange { get; set; }

        /// <summary>
        /// Whether subfolders were included
        /// </summary>
        public bool IncludedSubfolders { get; set; }
    }

    /// <summary>
    /// Approval history entry
    /// </summary>
    public class ApprovalHistoryEntry
    {
        /// <summary>
        /// Approval log ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Document version ID
        /// </summary>
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Document title
        /// </summary>
        public string DocumentTitle { get; set; }

        /// <summary>
        /// Approval action (Submitted, Approved, Rejected)
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Comments from reviewer
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// User who performed the action
        /// </summary>
        public string ActionBy { get; set; }

        /// <summary>
        /// When the action was performed
        /// </summary>
        public DateTime ActionAt { get; set; }

        /// <summary>
        /// Folder where the document was located
        /// </summary>
        public FolderSummaryResponse? DocumentFolder { get; set; }

        /// <summary>
        /// Previous status
        /// </summary>
        public string? PreviousStatus { get; set; }

        /// <summary>
        /// New status
        /// </summary>
        public string NewStatus { get; set; }

        /// <summary>
        /// Target folder ID where the document was moved when approved
        /// </summary>
        public string? TargetFolderId { get; set; }

        /// <summary>
        /// Target folder name where the document was moved when approved
        /// </summary>
        public string? TargetFolderName { get; set; }
    }

    /// <summary>
    /// Date range filter
    /// </summary>
    public class DateRange
    {
        /// <summary>
        /// Start date
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date
        /// </summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Folder move result
    /// </summary>
    public class FolderMoveResult
    {
        /// <summary>
        /// Whether the move was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Source folder information
        /// </summary>
        public FolderSummaryResponse? SourceFolder { get; set; }

        /// <summary>
        /// Target folder information
        /// </summary>
        public FolderSummaryResponse? TargetFolder { get; set; }

        /// <summary>
        /// Error message if move failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Move timestamp
        /// </summary>
        public DateTime MovedAt { get; set; }
    }

    /// <summary>
    /// Bulk review request
    /// </summary>
    public class BulkReviewRequest
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Whether to approve (true) or reject (false)
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// Review comments
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Target folder for approved documents (optional)
        /// </summary>
        public string? TargetFolderId { get; set; }
    }

    /// <summary>
    /// Bulk approval response
    /// </summary>
    public class BulkApprovalResponse
    {
        /// <summary>
        /// Total number of documents processed
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Number of successful approvals/rejections
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Individual results
        /// </summary>
        public List<BulkApprovalResult> Results { get; set; } = new List<BulkApprovalResult>();

        /// <summary>
        /// Processing timestamp
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// User who performed the bulk operation
        /// </summary>
        public string ProcessedBy { get; set; }
    }

    /// <summary>
    /// Individual bulk approval result
    /// </summary>
    public class BulkApprovalResult
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Document title
        /// </summary>
        public string DocumentTitle { get; set; }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// New status after operation
        /// </summary>
        public string? NewStatus { get; set; }

        /// <summary>
        /// Target folder information
        /// </summary>
        public FolderSummaryResponse? TargetFolder { get; set; }
    }

    /// <summary>
    /// Folder approval statistics
    /// </summary>
    public class FolderApprovalStatistics
    {
        /// <summary>
        /// Folder information
        /// </summary>
        public FolderSummaryResponse Folder { get; set; }

        /// <summary>
        /// Total documents submitted for approval
        /// </summary>
        public int TotalSubmitted { get; set; }

        /// <summary>
        /// Total documents approved
        /// </summary>
        public int TotalApproved { get; set; }

        /// <summary>
        /// Total documents rejected
        /// </summary>
        public int TotalRejected { get; set; }

        /// <summary>
        /// Documents currently pending approval
        /// </summary>
        public int CurrentlyPending { get; set; }

        /// <summary>
        /// Average approval time in hours
        /// </summary>
        public double AverageApprovalTimeHours { get; set; }

        /// <summary>
        /// Approval rate percentage
        /// </summary>
        public double ApprovalRate { get; set; }

        /// <summary>
        /// Statistics by month
        /// </summary>
        public List<MonthlyApprovalStats> MonthlyStats { get; set; } = new List<MonthlyApprovalStats>();

        /// <summary>
        /// Statistics generation date
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Date range for statistics
        /// </summary>
        public DateRange? DateRange { get; set; }

        /// <summary>
        /// Whether subfolders were included
        /// </summary>
        public bool IncludedSubfolders { get; set; }
    }

    /// <summary>
    /// Monthly approval statistics
    /// </summary>
    public class MonthlyApprovalStats
    {
        /// <summary>
        /// Year and month
        /// </summary>
        public DateTime Month { get; set; }

        /// <summary>
        /// Documents submitted in this month
        /// </summary>
        public int Submitted { get; set; }

        /// <summary>
        /// Documents approved in this month
        /// </summary>
        public int Approved { get; set; }

        /// <summary>
        /// Documents rejected in this month
        /// </summary>
        public int Rejected { get; set; }

        /// <summary>
        /// Average approval time for this month
        /// </summary>
        public double AverageApprovalTimeHours { get; set; }
    }
}
