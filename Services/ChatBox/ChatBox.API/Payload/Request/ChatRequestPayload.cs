using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request
{
    public class ChatRequestPayload
    {
        [Required(ErrorMessage = "Question is required.")]
        public string Question { get; set; }
    }
}
