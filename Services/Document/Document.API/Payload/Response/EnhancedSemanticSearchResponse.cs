namespace Document.API.Payload.Response
{
    /// <summary>
    /// Enhanced semantic search response that includes AI-powered conversational answers with relevant document sources
    /// </summary>
    public class EnhancedSemanticSearchResponse
    {
        /// <summary>
        /// Unique identifier for this search request
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// The original search query
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// AI-generated conversational answer based on the query and relevant documents
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the AI was able to provide a meaningful answer
        /// </summary>
        public bool HasAnswer { get; set; }

        /// <summary>
        /// List of relevant document sources that contributed to the answer
        /// </summary>
        public List<SemanticSearchResponse> RelevantDocuments { get; set; } = new();

        /// <summary>
        /// Total number of documents found (before pagination)
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Search metadata and configuration used
        /// </summary>
        public SearchMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Error message if the search failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Indicates if the search was successful
        /// </summary>
        public bool Success { get; set; } = true;
    }

    /// <summary>
    /// Metadata about the search configuration and results
    /// </summary>
    public class SearchMetadata
    {
        /// <summary>
        /// Minimum relevance threshold used
        /// </summary>
        public double MinRelevance { get; set; }

        /// <summary>
        /// Maximum results requested
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Whether hybrid scoring was enabled
        /// </summary>
        public bool HybridScoringEnabled { get; set; }

        /// <summary>
        /// Search scope used (All, PublicOnly, DepartmentOnly)
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Department filter applied (if any)
        /// </summary>
        public string? DepartmentFilter { get; set; }

        /// <summary>
        /// Document type filter applied (if any)
        /// </summary>
        public string? DocumentTypeFilter { get; set; }

        /// <summary>
        /// Date range filters applied
        /// </summary>
        public DateRangeFilter? DateRange { get; set; }
    }

    /// <summary>
    /// Date range filter information
    /// </summary>
    public class DateRangeFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
    }
}
