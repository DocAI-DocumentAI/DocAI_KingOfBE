using Shared.DTOs;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for looking up user and department names from Auth service
/// </summary>
public interface INameLookupService
{
    /// <summary>
    /// Get user and department names for the provided IDs
    /// Uses caching and bulk requests for optimal performance
    /// </summary>
    /// <param name="userIds">List of user IDs to lookup</param>
    /// <param name="departmentIds">List of department IDs to lookup</param>
    /// <returns>Dictionary mapping IDs to names</returns>
    Task<NameLookupResponse> GetNamesAsync(List<string> userIds, List<string> departmentIds);
    
    /// <summary>
    /// Get user name by ID with caching
    /// </summary>
    /// <param name="userId">User ID to lookup</param>
    /// <returns>User name or null if not found</returns>
    Task<string?> GetUserNameAsync(string userId);
    
    /// <summary>
    /// Get department name by ID with caching
    /// </summary>
    /// <param name="departmentId">Department ID to lookup</param>
    /// <returns>Department name or null if not found</returns>
    Task<string?> GetDepartmentNameAsync(string departmentId);
    
    /// <summary>
    /// Clear cache for specific user
    /// </summary>
    /// <param name="userId">User ID to clear from cache</param>
    Task ClearUserCacheAsync(string userId);
    
    /// <summary>
    /// Clear cache for specific department
    /// </summary>
    /// <param name="departmentId">Department ID to clear from cache</param>
    Task ClearDepartmentCacheAsync(string departmentId);
}
