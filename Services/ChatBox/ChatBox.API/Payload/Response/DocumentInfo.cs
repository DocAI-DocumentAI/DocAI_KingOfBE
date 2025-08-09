namespace ChatBox.API.Payload.Response
{
    public class DocumentInfo
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public double RelevanceScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
    }
}
