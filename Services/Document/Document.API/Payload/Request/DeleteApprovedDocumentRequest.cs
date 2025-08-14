using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for deleting approved or archived documents
    /// Requires confirmation due to permanent nature of the operation
    /// </summary>
    public class DeleteApprovedDocumentRequest
    {
        /// <summary>
        /// Confirmation that user understands this is a permanent deletion
        /// Must be true to proceed with deletion
        /// </summary>
        [Required(ErrorMessage = "Confirmation is required to delete approved documents")]
        public bool ConfirmPermanentDeletion { get; set; }

        /// <summary>
        /// Optional reason for deletion (for audit purposes)
        /// </summary>
        [StringLength(500, ErrorMessage = "Deletion reason cannot exceed 500 characters")]
        public string? DeletionReason { get; set; }

        /// <summary>
        /// Whether to force deletion even if there are active replacements
        /// Only available for Admin users
        /// </summary>
        public bool ForceDelete { get; set; } = false;
    }
}
