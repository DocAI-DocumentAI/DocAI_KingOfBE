using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for permanently deleting archived documents
    /// Requires confirmation due to permanent nature of the operation
    /// </summary>
    public class DeleteArchivedDocumentRequest
    {
        /// <summary>
        /// Confirmation that user understands this is a permanent deletion
        /// Must be true to proceed with deletion
        /// </summary>
        [Required(ErrorMessage = "Confirmation is required to delete archived documents")]
        public bool ConfirmPermanentDeletion { get; set; }

        /// <summary>
        /// Reason for deletion (mandatory for audit purposes)
        /// </summary>
        [Required(ErrorMessage = "Deletion reason is required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Deletion reason must be between 10 and 500 characters")]
        public string DeletionReason { get; set; } = string.Empty;

        /// <summary>
        /// Whether to notify the document owner about the deletion
        /// </summary>
        public bool NotifyOwner { get; set; } = true;

        /// <summary>
        /// Whether to force deletion even if there are dependencies
        /// Only available for Admin users
        /// </summary>
        public bool ForceDelete { get; set; } = false;
    }
}
