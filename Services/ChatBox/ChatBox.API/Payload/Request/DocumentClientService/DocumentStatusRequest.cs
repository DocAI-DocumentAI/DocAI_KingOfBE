using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.DocumentClientService
{
    public class DocumentStatusRequest
    {
        [Required]
        public string DocumentId { get; set; }

        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeVersionHistory { get; set; } = false;
    }
}
