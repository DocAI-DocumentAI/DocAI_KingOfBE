namespace Document.API.Payload.Request
{
    public class DocumentReplacementSuggestionRequest
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string DocumentTypeId { get; set; } = string.Empty;

        public List<string>? Tags { get; set; }

        public bool IsPublic { get; set; } = false;

        public int MaxSuggestions { get; set; } = 10;

        public double MinSimilarityThreshold { get; set; } = 0.45;

        public bool SameDepartmentOnly { get; set; } = false;
    }
}
