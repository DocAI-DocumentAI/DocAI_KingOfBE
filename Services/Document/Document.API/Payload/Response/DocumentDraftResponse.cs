namespace Document.API.Payload.Response
{
    public class DocumentDraftResponse
    {
        public string DocumentId { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Summary { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public string Status { get; set; }
        public string VersionName { get; set; }
        public string DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public List<string> Tags { get; set; }
        public DateTime CreatedTime { get; set; }
        public string DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? ReplacementId { get; set; }
        public DocumentResponse? ReplacementDocument { get; set; }
        public string? ReplacementDocumentName { get; set; }
        public bool IsReplaced { get; set; }
        public DateTime? LastSubmitted { get; set; }
        public string? SubmittedBy { get; set; }
        public string? SubmittedByName { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Person or authority who signed the document
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date from which the document is valid
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until which the document is valid
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }

        /// <summary>
        /// Current folder ID where the document is located
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Target folder ID where the document will be moved when approved
        /// </summary>
        public string? TargetFolderId { get; set; }

        /// <summary>
        /// Current folder name where the document is located
        /// </summary>
        public string? FolderName { get; set; }

        /// <summary>
        /// Target folder name where the document will be moved when approved
        /// </summary>
        public string? TargetFolderName { get; set; }
    }
}
