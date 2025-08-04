namespace Document.API.Payload.Response
{
    public class DocumentSourceResponse
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? FileType { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public double RelevanceScore { get; set; }
    }
}
