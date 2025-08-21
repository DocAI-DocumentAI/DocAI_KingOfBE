using System.ComponentModel.DataAnnotations;

namespace Document.Domain.Models;

/// <summary>
/// AI Configuration entity for managing Kernel Memory AI model settings
/// </summary>
public class AIConfiguration : BaseEntity
{
    /// <summary>
    /// Model name (e.g., "openai/gpt-oss-120b")
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ModelName { get; set; }

    /// <summary>
    /// Model identifier for display purposes
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ModelId { get; set; }

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
    /// Whether this configuration is the default one
    /// </summary>
    public bool IsDefault { get; set; } = false;
}
