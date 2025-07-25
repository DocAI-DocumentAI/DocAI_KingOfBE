namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class PopularityMetrics
    {
        public int PopularityRank { get; set; }
        public double PopularityScore { get; set; }
        public string Category { get; set; }
        public List<string> TrendingKeywords { get; set; } = new();
    }
}
