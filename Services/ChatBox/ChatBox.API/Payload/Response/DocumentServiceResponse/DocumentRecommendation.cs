namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentRecommendation
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double RelevanceScore { get; set; }
        public string RecommendationReason { get; set; }
        public List<string> Tags { get; set; } = new();
        public DateTime LastModified { get; set; }
    }
}
