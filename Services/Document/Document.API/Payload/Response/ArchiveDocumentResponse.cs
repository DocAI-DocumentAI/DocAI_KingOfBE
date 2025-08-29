using Document.API.Payload.Response.Folder;

namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for document archival operation
    /// </summary>
    public class ArchiveDocumentResponse
    {
        /// <summary>
        /// ID of the archived document version
        /// </summary>
        public string DocumentVersionId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the document file
        /// </summary>
        public string DocumentFileId { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Document version name
        /// </summary>
        public string VersionName { get; set; } = string.Empty;

        /// <summary>
        /// Status after archival (should be "Archived")
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the document was archived
        /// </summary>
        public DateTime ArchivedAt { get; set; }

        /// <summary>
        /// User who archived the document
        /// </summary>
        public string ArchivedBy { get; set; } = string.Empty;

        /// <summary>
        /// Name of the user who archived the document (enriched)
        /// </summary>
        public string? ArchivedByName { get; set; }

        /// <summary>
        /// Reason for archiving
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Additional comments for the archival
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// Folder information where the document is located
        /// </summary>
        public FolderSummaryResponse? Folder { get; set; }

        /// <summary>
        /// Whether the document was removed from Kernel Memory index
        /// </summary>
        public bool RemovedFromKernelMemory { get; set; }

        /// <summary>
        /// Whether there were any warnings during the archival process
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Success message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
