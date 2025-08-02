namespace ChatBox.API.Payload.Request
{
    public class AIConfigurationRequest
    {
        public string Provider { get; set; }
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public string Endpoint { get; set; }
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public double? TopK { get; set; }
        public int MaxTokens { get; set; } = 4000;
        public string SystemPrompt { get; set; }

    }
}
