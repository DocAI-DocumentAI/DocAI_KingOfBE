namespace Document.API.Payload.Response;

/// <summary>
/// Response model for AI configuration
/// </summary>
public class AIConfigurationResponse
{
    /// <summary>
    /// Configuration ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Model name (e.g., "openai/gpt-4o-mini")
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier for display purposes
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens for the model
    /// </summary>
    public int MaxToken { get; set; }

    /// <summary>
    /// System prompt for document analysis (only used for AnalyzeDocumentAsync)
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Whether this is the default configuration
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime? LastUpdatedTime { get; set; }

    /// <summary>
    /// Created by user ID
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Last updated by user ID
    /// </summary>
    public string? LastUpdatedBy { get; set; }

    /// <summary>
    /// Created by user name (enriched)
    /// </summary>
    public string? CreatedByName { get; set; }

    /// <summary>
    /// Last updated by user name (enriched)
    /// </summary>
    public string? LastUpdatedByName { get; set; }
}
