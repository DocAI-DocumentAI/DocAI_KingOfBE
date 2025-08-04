using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class DocumentReplacementSuggestionRequest
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title must not exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Document type is required")]
        public string DocumentTypeId { get; set; } = string.Empty;

        public List<string>? Tags { get; set; }

        public bool IsPublic { get; set; } = false;

        [Range(1, 20, ErrorMessage = "MaxSuggestions must be between 1 and 20")]
        public int MaxSuggestions { get; set; } = 10;

        [Range(0.0, 1.0, ErrorMessage = "MinSimilarityThreshold must be between 0.0 and 1.0")]
        public double MinSimilarityThreshold { get; set; } = 0.45;

        public bool SameDepartmentOnly { get; set; } = false;
    }
}
