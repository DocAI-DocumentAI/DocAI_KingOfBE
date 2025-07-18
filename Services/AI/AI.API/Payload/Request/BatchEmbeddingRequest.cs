using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class BatchEmbeddingRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one document is required")]
        [MaxLength(100, ErrorMessage = "Cannot process more than 100 documents at once")]
        public List<EmbeddingRequest> Documents { get; set; }

    }
}
