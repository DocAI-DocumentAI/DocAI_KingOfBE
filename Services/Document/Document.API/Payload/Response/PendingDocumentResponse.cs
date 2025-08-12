using Document.Domain.Enums;

namespace Document.API.Payload.Response
{
    public class PendingDocumentResponse
    {
        public string DocumentFileId { get; set; }
        public string VersionId { get; set; }
        public string VersionName { get; set; }
        public string Title { get; set; }
        public string SubmittedBy { get; set; }
        public string? SubmittedByName { get; set; }
        public DateTime LastSubmitted { get; set; }
        public string Status { get; set; }
        public string DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; }

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
        /// BR-219: Indicates if the document is currently being reviewed by another manager
        /// </summary>
        public bool IsBeingReviewed { get; set; }

        /// <summary>
        /// BR-219: ID of the manager who claimed the document for review
        /// </summary>
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// BR-219: Timestamp when the document was claimed for review
        /// </summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>
        /// Name of the manager who claimed the document for review
        /// </summary>
        public string? ReviewedByName { get; set; }

        /// <summary>
        /// Document description for better context
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Document summary for quick overview
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// File type/extension
        /// </summary>
        public string? FileType { get; set; }

        /// <summary>
        /// Document tags for categorization
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Document creation timestamp
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// Owner ID of the document
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// Owner name of the document
        /// </summary>
        public string? OwnerName { get; set; }

        /// <summary>
        /// Priority level for processing (calculated based on submission date and document type)
        /// </summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Days since submission (for quick assessment of processing urgency)
        /// </summary>
        public int DaysSinceSubmission { get; set; }

        /// <summary>
        /// Indicates if the document is approaching the 7-day expiration deadline
        /// </summary>
        public bool IsApproachingExpiration { get; set; }

        /// <summary>
        /// Number of times this document has been resubmitted
        /// </summary>
        public int ResubmissionCount { get; set; }

        /// <summary>
        /// Previous rejection reason if this is a resubmission
        /// </summary>
        public string? PreviousRejectionReason { get; set; }
    }
}
