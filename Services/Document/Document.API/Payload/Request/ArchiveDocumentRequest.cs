using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for archiving approved documents
    /// Allows managers to manually archive documents that are no longer current
    /// </summary>
    public class ArchiveDocumentRequest
    {
        /// <summary>
        /// Reason for archiving the document (mandatory for audit purposes)
        /// </summary>
        [Required(ErrorMessage = "Archive reason is required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Archive reason must be between 10 and 500 characters")]
        public string ArchiveReason { get; set; } = string.Empty;

        /// <summary>
        /// Whether to notify the document owner about the archival
        /// </summary>
        public bool NotifyOwner { get; set; } = true;

        /// <summary>
        /// Whether to notify users with access to the document
        /// </summary>
        public bool NotifyUsers { get; set; } = false;
    }
}
