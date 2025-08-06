namespace Document.API.Payload.Response;

/// <summary>
/// Response model for regenerated document summary with enhanced structured format
/// </summary>
public class RegenerateSummaryResponse
{
    /// <summary>
    /// The structured summary in HTML format with sections for key points, details, actions, and conclusions
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the summary generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if summary generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Token usage information for the AI request
    /// </summary>
    public TokenUsageInfo? TokenUsage { get; set; }

    /// <summary>
    /// Timestamp when the summary was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Information about token usage for AI service calls
/// </summary>
public class TokenUsageInfo
{
    /// <summary>
    /// Number of tokens used in the request/prompt
    /// </summary>
    public int RequestTokens { get; set; }

    /// <summary>
    /// Number of tokens used in the response
    /// </summary>
    public int ResponseTokens { get; set; }

    /// <summary>
    /// Total tokens used (request + response)
    /// </summary>
    public int TotalTokens => RequestTokens + ResponseTokens;

    /// <summary>
    /// Estimated cost for the AI service call (if available)
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Model used for the AI service call
    /// </summary>
    public string? ModelUsed { get; set; }
}
