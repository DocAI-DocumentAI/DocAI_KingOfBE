
using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class AIRequest
    {
        [Required]
        [MaxLength(10000)]
        public string Prompt { get; set; }

        public string UserId { get; set; }

        public string SessionId { get; set; }

        public string Source { get; set; }

        // Generation settings
        [Range(1, 4096)]
        public int? MaxTokens { get; set; }

        [Range(0.0, 2.0)]
        public double? Temperature { get; set; }

        [Range(0.0, 1.0)]
        public double? TopP { get; set; }

        public bool Stream { get; set; } = false;
    }
}
