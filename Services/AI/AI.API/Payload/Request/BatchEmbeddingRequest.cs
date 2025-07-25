using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class BatchEmbeddingRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<EmbeddingItem> Items { get; set; }

        [MaxLength(50)]
        public string SourceService { get; set; } = "DocumentService";

    }
    public class EmbeddingItem
    {
        [Required(ErrorMessage = "Text is required")]
        [StringLength(8000, ErrorMessage = "Text cannot exceed 8000 characters")]
        public string Text { get; set; }

        public string DocumentId { get; set; }
        public string VersionId { get; set; }

        // Content (backward compatibility)
        public string Content
        {
            get => Text;
            set => Text = value;
        }

        public string Title { get; set; }
        public string Summary { get; set; }
        public string TypeName { get; set; }
        public int? DepartmentId { get; set; }
    }
}
