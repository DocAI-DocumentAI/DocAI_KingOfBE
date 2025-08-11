namespace ChatBox.API.Payload.Response
{
    public class DocumentInfo
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;

        public string Description { get; set; }
        public List<string>? Tags { get; set; } = new List<string> { string.Empty };
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public double RelevanceScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
    }
}
