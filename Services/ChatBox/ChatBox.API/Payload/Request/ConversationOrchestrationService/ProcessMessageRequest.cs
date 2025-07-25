using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ConversationOrchestrationService
{
    public class ProcessMessageRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Message { get; set; }

        public Guid? SessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
