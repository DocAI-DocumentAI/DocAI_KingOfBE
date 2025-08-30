using Document.Domain.Enums;

namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for manager approval log entries
    /// </summary>
    public class ManagerApprovalLogResponse
    {
        /// <summary>
        /// Approval log ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Approval action taken (Approved, Rejected, etc.)
        /// </summary>
        public ApprovalAction Action { get; set; }

        /// <summary>
        /// Comments provided during approval/rejection
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Document version ID that was reviewed
        /// </summary>
        public string DocumentVersionId { get; set; } = string.Empty;

        /// <summary>
        /// Document file ID
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string DocumentTitle { get; set; } = string.Empty;

        /// <summary>
        /// Document description
        /// </summary>
        public string? DocumentDescription { get; set; }

        /// <summary>
        /// Version name
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// File name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Document type ID
        /// </summary>
        public string DocumentTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Document type name
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// User ID who submitted the document
        /// </summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Name of user who submitted the document
        /// </summary>
        public string? SubmittedByName { get; set; }

        /// <summary>
        /// When the document was submitted for approval
        /// </summary>
        public DateTime? SubmittedAt { get; set; }

        /// <summary>
        /// Manager who performed the approval action
        /// </summary>
        public string ReviewedBy { get; set; } = string.Empty;

        /// <summary>
        /// Name of manager who performed the approval action
        /// </summary>
        public string? ReviewedByName { get; set; }

        /// <summary>
        /// When the approval action was taken
        /// </summary>
        public DateTime ReviewedAt { get; set; }

        /// <summary>
        /// Current status of the document
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Whether the document is public
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Department ID
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Department name
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

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
        /// Current folder ID where the document is located
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Current folder name where the document is located
        /// </summary>
        public string? FolderName { get; set; }
    }
}
