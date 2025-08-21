using Microsoft.KernelMemory;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for dynamically configuring Kernel Memory with database AI settings
/// </summary>
public interface IKernelMemoryConfigurationService
{
    /// <summary>
    /// Gets a configured Kernel Memory instance using the default AI configuration from database
    /// </summary>
    /// <returns>Configured IKernelMemory instance</returns>
    Task<IKernelMemory> GetConfiguredKernelMemoryAsync();

    /// <summary>
    /// Gets a configured Kernel Memory instance using specific AI configuration
    /// </summary>
    /// <param name="configurationId">AI configuration ID</param>
    /// <returns>Configured IKernelMemory instance</returns>
    Task<IKernelMemory> GetConfiguredKernelMemoryAsync(string configurationId);

    /// <summary>
    /// Refreshes the cached Kernel Memory configuration
    /// </summary>
    Task RefreshConfigurationAsync();
}
