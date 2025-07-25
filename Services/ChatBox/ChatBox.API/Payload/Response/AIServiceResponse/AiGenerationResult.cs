namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class AiGenerationResult
    {
        public bool Success { get; set; }
        public string Response { get; set; }
        public int TokensUsed { get; set; }
        public string Model { get; set; }
        public double ConfidenceScore { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
