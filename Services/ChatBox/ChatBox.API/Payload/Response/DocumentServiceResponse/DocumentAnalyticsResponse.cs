namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentAnalyticsResponse
    {
        public string DocumentId { get; set; }
        public DocumentUsageStats UsageStats { get; set; }
        public List<UserInteraction> RecentInteractions { get; set; } = new();
        public PopularityMetrics Popularity { get; set; }
        public DateTime AnalysisDate { get; set; }
    }
}
