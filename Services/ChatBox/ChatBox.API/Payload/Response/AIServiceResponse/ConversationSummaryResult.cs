namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class ConversationSummaryResult
    {
        public string Summary { get; set; }
        public List<string> KeyPoints { get; set; } = new();
        public List<string> ActionItems { get; set; } = new();
        public List<string> Topics { get; set; } = new();
        public int OriginalLength { get; set; }
        public int SummaryLength { get; set; }
        public double CompressionRatio { get; set; }
        public string SummaryType { get; set; }
    }
}
