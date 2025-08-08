namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for iframe viewing URL generation
    /// </summary>
    public class IframeViewingResponse
    {
        /// <summary>
        /// Document version ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Secure iframe URL for viewing the document
        /// </summary>
        public string IframeUrl { get; set; } = string.Empty;

        /// <summary>
        /// Original file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File type/extension
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the file can be viewed inline in browser
        /// </summary>
        public bool CanViewInline { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// Google Drive file ID
        /// </summary>
        public string FileId { get; set; } = string.Empty;

        /// <summary>
        /// Instructions for using the iframe URL
        /// </summary>
        public string Instructions { get; set; } = "Use this URL in an iframe to display the document securely";

        /// <summary>
        /// When the URL was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who requested the iframe URL
        /// </summary>
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>
        /// Department ID of the requesting user
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;
    }
}
