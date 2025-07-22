namespace Shared.DTOs;

/// <summary>
/// Request for bulk name lookups to optimize performance
/// </summary>
public class NameLookupRequest
{
    /// <summary>
    /// List of user IDs to get names for
    /// </summary>
    public List<string> UserIds { get; set; } = new();
    
    /// <summary>
    /// List of department IDs to get names for
    /// </summary>
    public List<string> DepartmentIds { get; set; } = new();
    
    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}
