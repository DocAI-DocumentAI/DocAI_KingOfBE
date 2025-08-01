namespace Document.API.Payload.Response
{
    public class DocumentVersionResponse
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
        public string VersionName { get; set; }
        public string DepartmentId { get; set; }
        public string OwnerId { get; set; }
        public string? ReplacementId { get; set; }
        public DocumentResponse? ReplacementDocument { get; set; }
        public string? ReplacementDocumentName { get; set; }
        public bool IsReplaced { get; set; }
        public DateTime CreatedTime { get; set; }
        public string Status { get; set; }
        public List<string> Tags { get; set; }
        public DateTime? LastSubmitted { get; set; }
        public string? SubmittedBy { get; set; }
        public string DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
    }
}
