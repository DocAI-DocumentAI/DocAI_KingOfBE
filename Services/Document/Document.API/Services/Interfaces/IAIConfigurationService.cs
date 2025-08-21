using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service interface for managing AI configurations
/// </summary>
public interface IAIConfigurationService
{
    /// <summary>
    /// Get the default AI configuration
    /// </summary>
    /// <returns>Default AI configuration response or null if none exists</returns>
    Task<AIConfigurationResponse?> GetDefaultConfigurationAsync();

    /// <summary>
    /// Get AI configuration by ID
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <returns>AI configuration response or null if not found</returns>
    Task<AIConfigurationResponse?> GetConfigurationByIdAsync(string id);

    /// <summary>
    /// Get all AI configurations
    /// </summary>
    /// <returns>List of all AI configuration responses</returns>
    Task<List<AIConfigurationResponse>> GetAllConfigurationsAsync();

    /// <summary>
    /// Create a new AI configuration
    /// </summary>
    /// <param name="request">AI configuration creation request</param>
    /// <returns>Created AI configuration response</returns>
    Task<AIConfigurationResponse> CreateConfigurationAsync(CreateAIConfigurationRequest request);

    /// <summary>
    /// Update an existing AI configuration
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="request">AI configuration update request</param>
    /// <returns>Updated AI configuration response</returns>
    Task<AIConfigurationResponse> UpdateConfigurationAsync(string id, UpdateAIConfigurationRequest request);

    /// <summary>
    /// Delete an AI configuration
    /// </summary>
    /// <param name="id">Configuration ID to delete</param>
    Task DeleteConfigurationAsync(string id);

    /// <summary>
    /// Set a configuration as default (and unset others)
    /// </summary>
    /// <param name="id">Configuration ID to set as default</param>
    /// <returns>Updated AI configuration response</returns>
    Task<AIConfigurationResponse> SetDefaultConfigurationAsync(string id);

    /// <summary>
    /// Initialize a default AI configuration if none exists
    /// </summary>
    /// <returns>Default AI configuration response</returns>
    Task<AIConfigurationResponse> InitializeDefaultConfigurationAsync();
}
