using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ChatService
{
    public class FeedbackRequest
    {
        [Required]
        public Guid MessageId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        public string Comment { get; set; }
        public string FeedbackType { get; set; } = "quality";
    }
}
