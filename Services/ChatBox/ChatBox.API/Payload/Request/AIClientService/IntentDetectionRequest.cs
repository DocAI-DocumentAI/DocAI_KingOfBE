using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class IntentDetectionRequest
    {
        [Required]
        public string Text { get; set; }

        public List<string> PossibleIntents { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
