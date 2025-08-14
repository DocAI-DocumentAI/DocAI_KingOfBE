namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for editor's approval history with approval log details
    /// </summary>
    public class EditorApprovalHistoryResponse
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
        /// Original file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// File type/extension
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Current document status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Version name/identifier
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Department ID that owns the document
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Department name that owns the document
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Document type ID
        /// </summary>
        public string DocumentTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Document type name
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Document creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last update time
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// When the document was submitted for approval
        /// </summary>
        public DateTime? LastSubmitted { get; set; }

        /// <summary>
        /// User ID who submitted the document
        /// </summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Name of user who submitted the document
        /// </summary>
        public string? SubmittedByName { get; set; }

        /// <summary>
        /// Manager who reviewed (approved/rejected) the document
        /// </summary>
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// Name of manager who reviewed the document
        /// </summary>
        public string? ReviewedByName { get; set; }

        /// <summary>
        /// When the document was reviewed
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Review comments/reason for approval or rejection
        /// </summary>
        public string? ReviewComments { get; set; }

        /// <summary>
        /// Person or authority who signed the document
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date from which the document is valid
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until which the document is valid
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }

        /// <summary>
        /// Whether the document is public or department-restricted
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Whether the document is official
        /// </summary>
        public bool IsOfficial { get; set; }

        /// <summary>
        /// Download count for the document
        /// </summary>
        public int? TotalDownloads { get; set; }
    }
}
