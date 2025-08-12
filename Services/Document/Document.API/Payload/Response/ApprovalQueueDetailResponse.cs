namespace Document.API.Payload.Response
{
    /// <summary>
    /// Enhanced approval queue detail response with complete document information for manager review
    /// Includes all fields available in official document view for comprehensive evaluation
    /// </summary>
    public class ApprovalQueueDetailResponse
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
        /// File path in storage
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

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
        /// Department ID
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Department name
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Document owner ID
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// Document owner name
        /// </summary>
        public string? OwnerName { get; set; }

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Document type ID
        /// </summary>
        public string DocumentTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Document type name
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Document creation timestamp
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// User who created the document
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Name of user who created the document
        /// </summary>
        public string? CreatedByName { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// User who last updated the document
        /// </summary>
        public string? LastUpdatedBy { get; set; }

        /// <summary>
        /// Name of user who last updated the document
        /// </summary>
        public string? LastUpdatedByName { get; set; }

        /// <summary>
        /// Replacement document ID
        /// </summary>
        public string? ReplacementId { get; set; }

        /// <summary>
        /// Replacement document details
        /// </summary>
        public DocumentResponse? ReplacementDocument { get; set; }

        /// <summary>
        /// Replacement document name
        /// </summary>
        public string? ReplacementDocumentName { get; set; }

        /// <summary>
        /// Whether this document has been replaced
        /// </summary>
        public bool IsReplaced { get; set; }

        /// <summary>
        /// Whether the document is public or private
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Last submission timestamp
        /// </summary>
        public DateTime? LastSubmitted { get; set; }

        /// <summary>
        /// User who submitted for approval
        /// </summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Name of user who submitted for approval
        /// </summary>
        public string? SubmittedByName { get; set; }

        /// <summary>
        /// Manager who claimed the document for review
        /// </summary>
        public string? ClaimedBy { get; set; }

        /// <summary>
        /// Name of manager who claimed the document
        /// </summary>
        public string? ClaimedByName { get; set; }

        /// <summary>
        /// Timestamp when document was claimed
        /// </summary>
        public DateTime? ClaimedAt { get; set; }

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
        /// Manager who reviewed (approved/rejected) the document
        /// </summary>
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// Name of manager who reviewed the document
        /// </summary>
        public string? ReviewedByName { get; set; }

        /// <summary>
        /// Timestamp when document was reviewed
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Review comments/reason for approval or rejection
        /// </summary>
        public string? ReviewComments { get; set; }

        /// <summary>
        /// Number of times this document has been resubmitted
        /// </summary>
        public int ResubmissionCount { get; set; }

        /// <summary>
        /// Previous rejection reason if this is a resubmission
        /// </summary>
        public string? PreviousRejectionReason { get; set; }

        /// <summary>
        /// Days since submission (for urgency assessment)
        /// </summary>
        public int DaysSinceSubmission { get; set; }

        /// <summary>
        /// Whether the document is approaching expiration deadline
        /// </summary>
        public bool IsApproachingExpiration { get; set; }

        /// <summary>
        /// Priority level for processing
        /// </summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Download count for the document
        /// </summary>
        public int DownloadCount { get; set; }

        /// <summary>
        /// View count for the document
        /// </summary>
        public int ViewCount { get; set; }
    }
}