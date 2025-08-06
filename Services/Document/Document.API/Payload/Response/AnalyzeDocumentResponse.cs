namespace Document.API.Payload.Response
{
    public class AnalyzeDocumentResponse
    {
        public string? Title { get; set; }
        public string? VersionName { get; set; }
        public string? Description { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public string? SignedBy { get; set; }
        public string Summary { get; set; }
        public List<string> Tags { get; set; }
        public TokenUsageInfo? TokenUsage { get; set; }
    }
}
