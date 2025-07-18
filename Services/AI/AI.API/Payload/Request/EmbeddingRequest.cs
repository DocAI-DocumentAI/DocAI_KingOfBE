using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class EmbeddingRequest
    {
        [Required(ErrorMessage = "DocumentId is required")]
        public string DocumentId { get; set; }

        [Required(ErrorMessage = "Content is required")]
        [StringLength(8000, ErrorMessage = "Content cannot exceed 8000 characters")]
        public string Content { get; set; }

        public string Title { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
    }
}
