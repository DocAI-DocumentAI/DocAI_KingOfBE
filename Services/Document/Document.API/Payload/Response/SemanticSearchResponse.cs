namespace Document.API.Payload.Response
{
    public class SemanticSearchResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedTime { get; set; }
        public string? LastUpdatedby { get; set; }
        public string? LastUpdatedByName { get; set; }
        public DateTime? LastUpdatedTime { get; set; }
        public string? FilePath { get; set; }
        public string? FileType { get; set; }
        public long FileSize { get; set; }
        public string? Version { get; set; }
        public List<string> Tags { get; set; } = new();
        public string? ReplacementId { get; set; }
        public DocumentResponse? ReplacementDocument { get; set; }
        public bool IsReplaced { get; set; }
        public double Relevance { get; set; }
        public string DocumentTypeId { get; set; } = string.Empty;
        public string? DocumentTypeName { get; set; }
    }
}
