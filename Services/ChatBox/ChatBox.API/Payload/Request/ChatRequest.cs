using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request
{
    public class ChatRequest
    {
        [Required]
        [StringLength(2000, ErrorMessage = "Message too long")]
        public string Message { get; set; } = "";

        public string? SessionId { get; set; }

        [StringLength(100)]
        public string? ModelName { get; set; }
        public string? DocumentId { get; set; }
    }
}
