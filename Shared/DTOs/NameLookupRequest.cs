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

/// <summary>
/// Request for department employee information for permissions
/// </summary>
public class DepartmentEmployeeRequest
{
    /// <summary>
    /// Department ID to get employees for
    /// </summary>
    public string DepartmentId { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include only managers
    /// </summary>
    public bool ManagersOnly { get; set; } = false;

    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Request for all company employees
/// </summary>
public class CompanyEmployeeRequest
{
    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Request for user email by user ID
/// </summary>
public class UserEmailRequest
{
    /// <summary>
    /// User ID to get email for
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}
