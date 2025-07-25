using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class EstimateTokenRequest
    {
        [Required]
        public string Input { get; set; }

        public string SystemPrompt { get; set; }
        public List<string> ConversationHistory { get; set; } = new();
        public string Model { get; set; } = "default";
        public int MaxResponseTokens { get; set; } = 1000;
        public bool IncludeSpecialTokens { get; set; } = true;
    }
}
