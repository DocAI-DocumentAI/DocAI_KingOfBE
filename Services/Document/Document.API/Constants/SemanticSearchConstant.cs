namespace Document.API.Constants;

/// <summary>
/// Constants for semantic search configuration and scoring
/// </summary>
public static class SemanticSearchConstant
{
    /// <summary>
    /// Default search configuration values
    /// </summary>
    public static class Defaults
    {
        public const double MinRelevanceThreshold = 0.3;
        public const int MaxResults = 20;
        public const int SearchLimit = 100;
        public const bool EnableHybridScoring = true;
        public const bool BoostDepartmentResults = true;
        public const bool LatestVersionsOnly = true;
    }

    /// <summary>
    /// Hybrid scoring weights for semantic search
    /// </summary>
    public static class ScoringWeights
    {
        // Primary scoring components
        public const double SemanticSimilarityWeight = 0.65;
        public const double MetadataMatchWeight = 0.20;
        public const double ContextualFactorsWeight = 0.15;

        // Semantic similarity sub-weights
        public const double EmbeddingRelevanceWeight = 0.80;
        public const double TitleMatchWeight = 0.15;
        public const double DescriptionMatchWeight = 0.05;

        // Metadata matching sub-weights
        public const double TagSimilarityWeight = 0.40;
        public const double DocumentTypeMatchWeight = 0.30;
        public const double DepartmentCompatibilityWeight = 0.20;
        public const double StatusRelevanceWeight = 0.10;

        // Contextual factors sub-weights
        public const double RecencyWeight = 0.50;
        public const double DepartmentBonusWeight = 0.30;
        public const double PopularityWeight = 0.20;
    }

    /// <summary>
    /// Boost factors for different scenarios
    /// </summary>
    public static class BoostFactors
    {
        public const double SameDepartmentBoost = 1.2;
        public const double PublicDocumentBoost = 1.1;
        public const double RecentDocumentBoost = 1.15;
        public const double ExactTagMatchBoost = 1.25;
        public const double ApprovedStatusBoost = 1.1;
    }

    /// <summary>
    /// Performance and caching configuration
    /// </summary>
    public static class Performance
    {
        public const int BatchSize = 50;
        public const int MaxConcurrentBatches = 4;
        public const int ProcessingTimeoutMs = 5000;
        public const string CachePrefix = "semantic_search:";
        public static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Relevance thresholds for different quality levels
    /// </summary>
    public static class RelevanceThresholds
    {
        public const double HighQuality = 0.7;
        public const double MediumQuality = 0.5;
        public const double LowQuality = 0.3;
        public const double Minimum = 0.2;
    }

    /// <summary>
    /// Search scope configurations
    /// </summary>
    public static class SearchScopes
    {
        public const string All = "all";
        public const string PublicOnly = "public";
        public const string DepartmentOnly = "department";
    }

    /// <summary>
    /// Kernel Memory tag names for filtering
    /// </summary>
    public static class MemoryTags
    {
        public const string DocumentId = "documentId";
        public const string DepartmentId = "departmentId";
        public const string IsPublic = "isPublic";
        public const string DocumentType = "documentType";
        public const string Status = "status";
        public const string Tags = "tags";
        public const string SignedBy = "signedBy";
        public const string EffectiveFrom = "effectiveFrom";
        public const string EffectiveUntil = "effectiveUntil";
        public const string FileType = "fileType";
        public const string CreatedBy = "createdBy";
        public const string Version = "version";
    }

    /// <summary>
    /// Error messages for semantic search
    /// </summary>
    public static class ErrorMessages
    {
        public const string QueryTooShort = "Search query must be at least 3 characters long";
        public const string QueryTooLong = "Search query cannot exceed 500 characters";
        public const string InvalidRelevanceThreshold = "Relevance threshold must be between 0.0 and 1.0";
        public const string InvalidMaxResults = "Max results must be between 1 and 100";
        public const string SearchTimeout = "Search operation timed out";
        public const string NoResultsFound = "No documents found matching your search criteria";
        public const string InsufficientPermissions = "You don't have permission to access the requested documents";
    }
}
