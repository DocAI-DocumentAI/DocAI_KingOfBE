using Shared.DTOs;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for looking up user emails from Auth service
/// </summary>
public interface IUserEmailLookupService
{
    /// <summary>
    /// Get user emails for the provided user IDs
    /// Uses caching and bulk requests for optimal performance
    /// </summary>
    /// <param name="userIds">List of user IDs to lookup emails for</param>
    /// <returns>Dictionary mapping user IDs to email addresses</returns>
    Task<Dictionary<string, string>> GetUserEmailsAsync(List<string> userIds);
    
    /// <summary>
    /// Get user email by ID with caching
    /// </summary>
    /// <param name="userId">User ID to lookup email for</param>
    /// <returns>User email or null if not found</returns>
    Task<string?> GetUserEmailAsync(string userId);
    
    /// <summary>
    /// Clear cache for specific user email
    /// </summary>
    /// <param name="userId">User ID to clear from cache</param>
    Task ClearUserEmailCacheAsync(string userId);
    
    /// <summary>
    /// Clear all cached user emails
    /// </summary>
    Task ClearAllUserEmailCacheAsync();
}
