namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentUsageStats
    {
        public int TotalViews { get; set; }
        public int UniqueViewers { get; set; }
        public int SearchAppearances { get; set; }
        public int CitationCount { get; set; }
        public TimeSpan AverageViewDuration { get; set; }
        public double UserRating { get; set; }
    }
}
