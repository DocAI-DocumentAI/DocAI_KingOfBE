namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for manually archiving an approved document
    /// </summary>
    public class ArchiveDocumentRequest
    {
        /// <summary>
        /// Reason for archiving the document (mandatory for audit trail)
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Optional additional comments for the archival
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Force archive even if there are active replacements or dependencies
        /// Only admins can use this flag
        /// </summary>
        public bool ForceArchive { get; set; } = false;
    }
}
