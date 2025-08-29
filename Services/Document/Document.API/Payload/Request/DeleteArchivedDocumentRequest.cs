namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for permanently deleting an archived document
    /// </summary>
    public class DeleteArchivedDocumentRequest
    {
        /// <summary>
        /// Confirmation that the manager understands this is permanent deletion
        /// </summary>
        public bool ConfirmPermanentDeletion { get; set; }

        /// <summary>
        /// Reason for deleting the archived document (mandatory for audit trail)
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Force delete even if there are dependencies or constraints
        /// Only admins can use this flag
        /// </summary>
        public bool ForceDelete { get; set; } = false;
    }
}
