using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class EstimateTokenRequest
    {
        /// <summary>
        /// The user's message/query
        /// </summary>
        [Required]
        public string Message { get; set; }

        /// <summary>
        /// The system prompt/instructions
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Previous conversation history for context
        /// </summary>
        public List<string> ConversationHistory { get; set; } = new List<string>();
    }
}
