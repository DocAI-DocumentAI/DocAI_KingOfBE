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

/// <summary>
/// Response containing department employee emails
/// </summary>
public class DepartmentEmployeeResponse
{
    /// <summary>
    /// List of employee email addresses
    /// </summary>
    public List<string> EmployeeEmails { get; set; } = new();

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

/// <summary>
/// Response containing all departments
/// </summary>
public class GetAllDepartmentsResponse
{
    /// <summary>
    /// List of departments
    /// </summary>
    public List<DepartmentDto> Departments { get; set; } = new();

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

/// <summary>
/// Department information DTO
/// </summary>
public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Response containing all company employee emails
/// </summary>
public class CompanyEmployeeResponse
{
    /// <summary>
    /// List of all employee email addresses
    /// </summary>
    public List<string> EmployeeEmails { get; set; } = new();

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

/// <summary>
/// Response containing user email
/// </summary>
public class UserEmailResponse
{
    /// <summary>
    /// User email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

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
