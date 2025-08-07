namespace Document.API.Payload.Response
{
    public class SemanticSearchResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedTime { get; set; }
        public string? LastUpdatedby { get; set; }
        public string? LastUpdatedByName { get; set; }
        public DateTime? LastUpdatedTime { get; set; }
        public string? FilePath { get; set; }
        public string? FileType { get; set; }
        public long FileSize { get; set; }
        public string? Version { get; set; }
        public List<string> Tags { get; set; } = new();
        public string? ReplacementId { get; set; }
        public DocumentResponse? ReplacementDocument { get; set; }
        public bool IsReplaced { get; set; }
        public double Relevance { get; set; }
        public string DocumentTypeId { get; set; } = string.Empty;
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Person or authority who signed the document
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date from which the document is valid
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until which the document is valid
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }

        /// <summary>
        /// Detailed scoring breakdown for transparency (only included when hybrid scoring is enabled)
        /// </summary>
        public SemanticSearchScoring? Scoring { get; set; }

        /// <summary>
        /// Indicates if this result was boosted due to department preference
        /// </summary>
        public bool IsDepartmentBoosted { get; set; }

        /// <summary>
        /// Search result rank position
        /// </summary>
        public int Rank { get; set; }
    }

    /// <summary>
    /// Detailed scoring breakdown for semantic search results
    /// </summary>
    public class SemanticSearchScoring
    {
        /// <summary>
        /// Raw semantic similarity score from Kernel Memory
        /// </summary>
        public double SemanticSimilarity { get; set; }

        /// <summary>
        /// Metadata matching score
        /// </summary>
        public double MetadataScore { get; set; }

        /// <summary>
        /// Contextual factors score
        /// </summary>
        public double ContextualScore { get; set; }

        /// <summary>
        /// Final weighted score
        /// </summary>
        public double FinalScore { get; set; }

        /// <summary>
        /// Applied boost factors
        /// </summary>
        public List<string> AppliedBoosts { get; set; } = new();

        /// <summary>
        /// Matching tags that contributed to the score
        /// </summary>
        public List<string> MatchingTags { get; set; } = new();
    }
}
