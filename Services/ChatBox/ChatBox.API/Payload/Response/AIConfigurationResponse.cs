namespace ChatBox.API.Payload.Response
{
    public class AIConfigurationResponse
    {
        public string Id { get; set; }
        public string Provider { get; set; }
        public string ModelName { get; set; }
        public string Endpoint { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public double? TopK { get; set; }
        public int MaxTokens { get; set; }
        public string SystemPrompt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
