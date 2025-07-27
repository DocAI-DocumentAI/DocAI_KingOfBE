using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ConversationOrchestrationService
{
    public class ProcessMessageRequest
    {
        public Guid UserId { get; set; }
        public string Message { get; set; }
        public Guid SessionId { get; set; }
        public string Context { get; set; }
        public string AIModelId { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
