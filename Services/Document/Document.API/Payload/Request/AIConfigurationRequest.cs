using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request;

/// <summary>
/// Request model for creating AI configuration
/// </summary>
public class CreateAIConfigurationRequest
{
    /// <summary>
    /// Model name (e.g., "openai/gpt-4o-mini")
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier for display purposes
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens for the model
    /// </summary>
    [Range(1, 32000)]
    public int MaxToken { get; set; } = 2000;

    /// <summary>
    /// System prompt for document analysis (only used for AnalyzeDocumentAsync)
    /// </summary>
    [StringLength(10000)]
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Whether this configuration should be set as default
    /// </summary>
    public bool IsDefault { get; set; } = false;
}

/// <summary>
/// Request model for updating AI configuration
/// </summary>
public class UpdateAIConfigurationRequest
{
    /// <summary>
    /// Model name (e.g., "openai/gpt-4o-mini")
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier for display purposes
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens for the model
    /// </summary>
    [Range(1, 32000)]
    public int MaxToken { get; set; } = 2000;

    /// <summary>
    /// System prompt for document analysis (only used for AnalyzeDocumentAsync)
    /// </summary>
    [StringLength(10000)]
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Whether this configuration should be set as default
    /// </summary>
    public bool IsDefault { get; set; } = false;
}
