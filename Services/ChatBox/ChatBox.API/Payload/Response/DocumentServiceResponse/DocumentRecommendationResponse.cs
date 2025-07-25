namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentRecommendationResponse
    {
        public List<DocumentRecommendation> Recommendations { get; set; } = new();
        public string RecommendationType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
