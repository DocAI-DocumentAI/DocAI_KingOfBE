using Document.API.Payload.Response;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Detailed response model for a document within folder context
    /// Combines document details with folder navigation and permissions
    /// </summary>
    public class FolderDocumentDetailResponse
    {
        /// <summary>
        /// Document file ID
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Document version ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Document description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Document summary
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Version name/number
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Document status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Whether document is public or department-restricted
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Whether this is the official/latest approved version
        /// </summary>
        public bool IsOfficial { get; set; }

        /// <summary>
        /// File information
        /// </summary>
        public DocumentFileInfo FileInfo { get; set; } = new DocumentFileInfo();

        /// <summary>
        /// Document type information
        /// </summary>
        public FolderDocumentTypeInfo DocumentType { get; set; } = new FolderDocumentTypeInfo();

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Document dates
        /// </summary>
        public DocumentDates Dates { get; set; } = new DocumentDates();

        /// <summary>
        /// Document ownership and department information
        /// </summary>
        public OwnershipInfo Ownership { get; set; } = new OwnershipInfo();

        /// <summary>
        /// Folder context information
        /// </summary>
        public FolderContext FolderContext { get; set; } = new FolderContext();

        /// <summary>
        /// User permissions for this document in folder context
        /// </summary>
        public DocumentPermissions Permissions { get; set; } = new DocumentPermissions();

        /// <summary>
        /// Approval information (if applicable)
        /// </summary>
        public ApprovalInfo? ApprovalInfo { get; set; }

        /// <summary>
        /// Google Drive integration information
        /// </summary>
        public GoogleDriveInfo GoogleDrive { get; set; } = new GoogleDriveInfo();
    }

    /// <summary>
    /// File-related information
    /// </summary>
    public class DocumentFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string? FileHash { get; set; }
    }

    /// <summary>
    /// Document type information
    /// </summary>
    public class FolderDocumentTypeInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Document date information
    /// </summary>
    public class DocumentDates
    {
        public DateTime CreatedTime { get; set; }
        public DateTime LastUpdatedTime { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    /// <summary>
    /// Ownership and department information
    /// </summary>
    public class OwnershipInfo
    {
        public string OwnerId { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? OwnerEmail { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string? SignedBy { get; set; }
    }

    /// <summary>
    /// Folder context information
    /// </summary>
    public class FolderContext
    {
        /// <summary>
        /// Current folder containing the document
        /// </summary>
        public FolderSummaryResponse CurrentFolder { get; set; } = new FolderSummaryResponse();

        /// <summary>
        /// Target folder (for documents in approval workflow)
        /// </summary>
        public FolderSummaryResponse? TargetFolder { get; set; }

        /// <summary>
        /// Breadcrumb navigation path
        /// </summary>
        public List<FolderBreadcrumbResponse> Breadcrumb { get; set; } = new List<FolderBreadcrumbResponse>();

        /// <summary>
        /// Whether user can move this document to other folders
        /// </summary>
        public bool CanMoveDocument { get; set; }
    }

    /// <summary>
    /// Document permissions in folder context
    /// </summary>
    public class DocumentPermissions
    {
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanMove { get; set; }
        public bool CanDownload { get; set; }
        public bool CanShare { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
    }

    /// <summary>
    /// Approval workflow information
    /// </summary>
    public class ApprovalInfo
    {
        public string? ReviewerId { get; set; }
        public string? ReviewerName { get; set; }
        public string? ReviewComments { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewAction { get; set; }
        public List<ApprovalLogEntry> ApprovalHistory { get; set; } = new List<ApprovalLogEntry>();
    }

    /// <summary>
    /// Approval log entry
    /// </summary>
    public class ApprovalLogEntry
    {
        public string Action { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public string ReviewerId { get; set; } = string.Empty;
        public string? ReviewerName { get; set; }
        public DateTime ReviewedAt { get; set; }
    }

    /// <summary>
    /// Google Drive integration information
    /// </summary>
    public class GoogleDriveInfo
    {
        public string? GoogleDriveFileId { get; set; }
        public string? GoogleDriveFolderId { get; set; }
        public string? WebViewLink { get; set; }
        public string? DownloadLink { get; set; }
    }
}
