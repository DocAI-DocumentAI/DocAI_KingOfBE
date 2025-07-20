using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class EmbeddingRequest
    {
        [Required(ErrorMessage = "DocumentId is required")]
        public string DocumentId { get; set; }

        [Required(ErrorMessage = "VersionId is required")]
        public string VersionId { get; set; }

        [Required(ErrorMessage = "Content is required")]
        [StringLength(8000, ErrorMessage = "Content cannot exceed 8000 characters")]
        public string Content { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string TypeName { get; set; }
        public int? DepartmentId { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
    }
}
