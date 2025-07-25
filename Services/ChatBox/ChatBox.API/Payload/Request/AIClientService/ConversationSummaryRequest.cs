using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    public class ConversationSummaryRequest
    {
        [Required]
        public List<string> ConversationHistory { get; set; }

        public int MaxLength { get; set; } = 500;
        public string SummaryType { get; set; } = "detailed"; // detailed, brief, bullet-points
        public string Language { get; set; } = "en";
    }
}
