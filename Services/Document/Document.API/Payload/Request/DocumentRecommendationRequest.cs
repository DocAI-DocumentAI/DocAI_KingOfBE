namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for getting document recommendations
    /// </summary>
    public class DocumentRecommendationRequest
    {
        /// <summary>
        /// Number of recommendations to return (default: 5, maximum: 20)
        /// </summary>
        public int Count { get; set; } = 5;

        /// <summary>
        /// Whether to include only documents from the same department (default: false)
        /// </summary>
        public bool IncludeSameDepartmentOnly { get; set; } = false;

        /// <summary>
        /// Optional filter to specific document type ID
        /// </summary>
        public string? DocumentTypeFilter { get; set; }
    }
}
