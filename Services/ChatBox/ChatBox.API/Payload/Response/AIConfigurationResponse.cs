namespace ChatBox.API.Payload.Response
{
    public class AIConfigurationResponse
    {
        public string Id { get; set; }
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsFree { get; set; }
        public bool IsDefault { get; set; } // ✅ ADD: New field
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
        public float TopP { get; set; }
        public string SystemPrompt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }

    }
}
