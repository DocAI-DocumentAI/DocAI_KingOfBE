namespace Document.API.Payload.Response
{
    /// <summary>
    /// Complete result for document recommendations
    /// </summary>
    public class DocumentRecommendationsResult
    {
        /// <summary>
        /// Source document ID that recommendations are based on
        /// </summary>
        public string SourceDocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Source document title
        /// </summary>
        public string SourceDocumentTitle { get; set; } = string.Empty;

        /// <summary>
        /// List of recommended documents
        /// </summary>
        public List<DocumentRecommendationResponse> Recommendations { get; set; } = new List<DocumentRecommendationResponse>();

        /// <summary>
        /// Total number of recommendations found
        /// </summary>
        public int TotalFound { get; set; }

        /// <summary>
        /// Number of recommendations requested
        /// </summary>
        public int RequestedCount { get; set; }

        /// <summary>
        /// When the recommendations were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether results were served from cache
        /// </summary>
        public bool FromCache { get; set; }
    }
}
