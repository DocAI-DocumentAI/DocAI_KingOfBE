namespace Document.API.Payload.Response.Document
{
    /// <summary>
    /// Summary response model for document information
    /// Used in lists and search results where full document details are not needed
    /// </summary>
    public class DocumentSummaryResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Document file ID (parent document)
        /// </summary>
        public string DocumentFileId { get; set; } = string.Empty;

        /// <summary>
        /// Version ID (same as Id for backward compatibility)
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Version name
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Document summary/description
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Document status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Document type name
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdatedTime { get; set; }

        /// <summary>
        /// User who created the document
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Whether the document is public
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Department ID
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// File type/extension
        /// </summary>
        public string? FileType { get; set; }

        /// <summary>
        /// Signed by information
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date range
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective until date
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }
    }
}
