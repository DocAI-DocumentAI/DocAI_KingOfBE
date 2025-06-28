namespace Document.API.Payload.Response
{
    public class DocumentDraftResponse
    {
        public string DocumentId { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Sumary { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileSize { get; set; }
        public string FileType { get; set; }
        public string Status { get; set; }
        public string VersionName { get; set; }
        public string DepartmentId { get; set; }
        public string OwnerId { get; set; }
        public DateTime CreatedTime { get; set; }
    }
}
