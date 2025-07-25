using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    /// <summary>
    /// Simplified embedding request for frontend usage
    /// </summary>
    public class EmbeddingRequest
    {
        [Required]
        [StringLength(8000, ErrorMessage = "Text cannot exceed 8000 characters")]
        public string Text { get; set; }

        public string UserId { get; set; }

        public string SessionId { get; set; }

        public string Source { get; set; }

        public string? DocumentId { get; set; }
        public string? VersionId { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? TypeName { get; set; }
        public int? DepartmentId { get; set; }
    }
}
