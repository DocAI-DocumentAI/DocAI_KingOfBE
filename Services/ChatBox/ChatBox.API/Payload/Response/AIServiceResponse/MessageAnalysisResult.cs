namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class MessageAnalysisResult
    {
        public string Intent { get; set; }
        public double IntentConfidence { get; set; }
        public string Sentiment { get; set; }
        public double SentimentScore { get; set; }
        public List<string> DetectedEntities { get; set; } = new();
        public List<string> DetectedTopics { get; set; } = new();
        public string Language { get; set; }
        public double LanguageConfidence { get; set; }
        public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
    }
}
