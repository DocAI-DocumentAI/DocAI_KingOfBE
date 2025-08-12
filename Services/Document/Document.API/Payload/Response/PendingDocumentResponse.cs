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
    }
}
