namespace Document.API.Payload.Request;

using Microsoft.AspNetCore.Mvc;

public class SemanticSearchRequest
{
    /// <summary>
    /// The search query text for semantic similarity matching
    /// </summary>
    [FromQuery]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Minimum relevance score threshold (0.0 to 1.0)
    /// </summary>
    [FromQuery]
    public double MinRelevance { get; set; } = 0.3;

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    [FromQuery]
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Enable hybrid scoring that combines semantic similarity with metadata and contextual factors
    /// </summary>
    [FromQuery]
    public bool EnableHybridScoring { get; set; } = true;

    /// <summary>
    /// Boost results from user's department
    /// </summary>
    [FromQuery]
    public bool BoostDepartmentResults { get; set; } = true;

    /// <summary>
    /// Include only latest versions of documents
    /// </summary>
    [FromQuery]
    public bool LatestVersionsOnly { get; set; } = true;

    /// <summary>
    /// Search scope: All, PublicOnly, DepartmentOnly
    /// </summary>
    [FromQuery]
    public SearchScope Scope { get; set; } = SearchScope.All;

    /// <summary>
    /// Filter by document type ID (optional)
    /// </summary>
    [FromQuery]
    public string? DocumentTypeId { get; set; }

    /// <summary>
    /// Filter by signed by (optional)
    /// </summary>
    [FromQuery]
    public string? SignedBy { get; set; }

    /// <summary>
    /// Filter documents created from this date (optional)
    /// </summary>
    [FromQuery]
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter documents created until this date (optional)
    /// </summary>
    [FromQuery]
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Filter documents effective from this date (optional)
    /// </summary>
    [FromQuery]
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    /// Filter documents effective until this date (optional)
    /// </summary>
    [FromQuery]
    public DateTime? EffectiveUntil { get; set; }
}

public enum SearchScope
{
    All = 0,
    PublicOnly = 1,
    DepartmentOnly = 2
}