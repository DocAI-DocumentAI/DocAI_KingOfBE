using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ChatService
{
    public class SendMessageRequest
    {
        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Message { get; set; }

        public Guid? SessionId { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public bool IncludeSuggestions { get; set; } = true;
        public string Priority { get; set; } = "normal";
    }
}
