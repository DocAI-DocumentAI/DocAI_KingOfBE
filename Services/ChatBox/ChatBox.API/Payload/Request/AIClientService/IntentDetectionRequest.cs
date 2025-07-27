using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class IntentDetectionRequest
    {
        public string Text { get; set; }
        public Guid? UserId { get; set; }
        public List<string> PossibleIntents { get; set; }
        public string Context { get; set; }
    }
}
