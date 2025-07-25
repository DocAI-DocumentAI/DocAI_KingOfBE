using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ChatService
{
    public class StreamChatRequest
    {
        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Message { get; set; }

        public Guid? SessionId { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public bool EnableStreaming { get; set; } = true;
    }
}
