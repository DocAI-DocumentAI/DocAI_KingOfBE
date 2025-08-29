namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for archived document deletion operation
    /// </summary>
    public class DeleteArchivedDocumentResponse
    {
        /// <summary>
        /// ID of the deleted document version
        /// </summary>
        public string DocumentVersionId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the deleted document file
        /// </summary>
        public string DocumentFileId { get; set; } = string.Empty;

        /// <summary>
        /// Document title that was deleted
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Document version name that was deleted
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the document was deleted
        /// </summary>
        public DateTime DeletedAt { get; set; }

        /// <summary>
        /// User who deleted the document
        /// </summary>
        public string DeletedBy { get; set; } = string.Empty;

        /// <summary>
        /// Name of the user who deleted the document (enriched)
        /// </summary>
        public string? DeletedByName { get; set; }

        /// <summary>
        /// Reason for deletion
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether the physical file was successfully deleted from storage
        /// </summary>
        public bool FileDeletedFromStorage { get; set; }

        /// <summary>
        /// Whether the document was removed from Kernel Memory index
        /// </summary>
        public bool RemovedFromKernelMemory { get; set; }

        /// <summary>
        /// Number of database records deleted
        /// </summary>
        public int DatabaseRecordsDeleted { get; set; }

        /// <summary>
        /// Whether there were any warnings during the deletion process
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
