namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for a single document recommendation
    /// </summary>
    public class DocumentRecommendationResponse
    {
        /// <summary>
        /// Document file ID
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Document description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Document type ID
        /// </summary>
        public string DocumentTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Document type name
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Department ID that owns the document
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Department name (enriched)
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Whether the document is public (accessible to all employees)
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Document creation date
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Document tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Relevance score (0.0 to 1.0)
        /// </summary>
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Explanation of why this document was recommended
        /// </summary>
        public string RecommendationReason { get; set; } = string.Empty;

        /// <summary>
        /// Number of tags shared with the source document
        /// </summary>
        public int SharedTagCount { get; set; }

        /// <summary>
        /// Latest version ID for accessing the document
        /// </summary>
        public string LatestVersionId { get; set; } = string.Empty;
    }
}
