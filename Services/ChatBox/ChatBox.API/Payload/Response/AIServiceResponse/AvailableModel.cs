namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class AvailableModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; } // generation, embedding, classification
        public int MaxTokens { get; set; }
        public bool SupportsStreaming { get; set; }
        public Dictionary<string, object> Capabilities { get; set; } = new();
        public string Status { get; set; } // available, maintenance, deprecated
    }
}
