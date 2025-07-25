namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class AdvancedAiGenerationRequest
    {
        public string Query { get; set; }
        public string Context { get; set; }
        public List<string> ConversationHistory { get; set; } = new();
        public Dictionary<string, object> UserPreferences { get; set; } = new();
        public int MaxTokens { get; set; } = 4000;
        public double Temperature { get; set; } = 0.5;
        public string Model { get; set; } = "default";
    }
}
