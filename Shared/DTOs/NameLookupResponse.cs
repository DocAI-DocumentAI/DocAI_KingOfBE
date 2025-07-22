namespace Shared.DTOs;

/// <summary>
/// Response containing user and department names
/// </summary>
public class NameLookupResponse
{
    /// <summary>
    /// Dictionary mapping user IDs to user names
    /// </summary>
    public Dictionary<string, string> UserNames { get; set; } = new();
    
    /// <summary>
    /// Dictionary mapping department IDs to department names
    /// </summary>
    public Dictionary<string, string> DepartmentNames { get; set; } = new();
    
    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the lookup was successful
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if lookup failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
