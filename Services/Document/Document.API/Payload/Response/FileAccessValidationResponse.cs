namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for file access validation
    /// </summary>
    public class FileAccessValidationResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user has access to the file
        /// </summary>
        public bool HasAccess { get; set; }

        /// <summary>
        /// Email of the user requesting access
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Department ID of the requesting user
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Original file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File type/extension
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the file can be viewed in browser
        /// </summary>
        public bool CanViewInBrowser { get; set; }

        /// <summary>
        /// Whether the file requires conversion for viewing
        /// </summary>
        public bool RequiresConversion { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// When the file was last modified
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Access level description
        /// </summary>
        public string AccessLevel { get; set; } = string.Empty;

        /// <summary>
        /// List of supported viewing methods for this user
        /// </summary>
        public string[] SupportedViewingMethods { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Google Drive file ID
        /// </summary>
        public string FileId { get; set; } = string.Empty;

        /// <summary>
        /// Document status (Draft, Pending, Approved, etc.)
        /// </summary>
        public string DocumentStatus { get; set; } = string.Empty;

        /// <summary>
        /// Whether the document is public or department-restricted
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Department that owns the document
        /// </summary>
        public string DocumentDepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Owner of the document
        /// </summary>
        public string DocumentOwnerId { get; set; } = string.Empty;

        /// <summary>
        /// When the validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Reason for access denial (if applicable)
        /// </summary>
        public string? AccessDenialReason { get; set; }

        /// <summary>
        /// Additional metadata about the file
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
