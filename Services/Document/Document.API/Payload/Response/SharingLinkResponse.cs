namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for time-limited sharing link generation
    /// </summary>
    public class SharingLinkResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Time-limited sharing URL
        /// </summary>
        public string SharingUrl { get; set; } = string.Empty;

        /// <summary>
        /// Original file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File type/extension
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Number of hours until the link expires
        /// </summary>
        public int ExpirationHours { get; set; }

        /// <summary>
        /// Exact expiration date and time
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// Google Drive file ID
        /// </summary>
        public string FileId { get; set; } = string.Empty;

        /// <summary>
        /// Instructions for using the sharing link
        /// </summary>
        public string Instructions { get; set; } = "This link provides time-limited access to the document";

        /// <summary>
        /// When the link was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who requested the sharing link
        /// </summary>
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>
        /// Department ID of the requesting user
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the link is still valid
        /// </summary>
        public bool IsValid => DateTime.UtcNow < ExpiresAt;

        /// <summary>
        /// Time remaining until expiration
        /// </summary>
        public TimeSpan TimeRemaining => ExpiresAt > DateTime.UtcNow ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
    }
}
