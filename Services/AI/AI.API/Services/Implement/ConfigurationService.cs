using System.Text.Json;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Enums;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace AI.API.Services.Implement
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ConfigurationService> _logger;
        private const string CONFIG_CACHE_PREFIX = "config:";
        private const string MODEL_CONFIG_CACHE_PREFIX = "modelconfig:";

        public ConfigurationService(
             IUnitOfWork<DocAIDbContext> unitOfWork,
             IMapper mapper,
             ICacheService cacheService,
             ILogger<ConfigurationService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _cacheService = cacheService;
            _logger = logger;
        }
        #region System Configuration


        public async Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be empty", nameof(key));

            try
            {
                // Check cache first
                var cacheKey = $"{CONFIG_CACHE_PREFIX}{key}";
                var cachedValue = await _cacheService.GetAsync<T>(cacheKey);
                if (cachedValue != null && !cachedValue.Equals(default(T)))
                {
                    return cachedValue;
                }

                // Get from database
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Key == key);

                if (config == null)
                {
                    _logger.LogDebug("Configuration key {Key} not found, using default value", key);
                    return defaultValue;
                }

                // Convert and cache
                T value = ConvertValue<T>(config.Value, defaultValue);
                await _cacheService.SetAsync(cacheKey, value, TimeSpan.FromMinutes(10));

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for key {Key}", key);
                return defaultValue;
            }
        }

        public async Task<ConfigurationResponse> SetConfigurationAsync(UpdateConfigurationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Key == request.Key);

                if (config == null)
                {
                    config = _mapper.Map<SystemConfiguration>(request);
                    config.CreatedAt = DateTime.UtcNow;
                    await repo.InsertAsync(config);

                    _logger.LogInformation("Created new configuration {Key}", request.Key);
                }
                else
                {
                    _mapper.Map(request, config);
                    repo.UpdateAsync(config);

                    _logger.LogInformation("Updated configuration {Key}", request.Key);
                }

                await _unitOfWork.CommitAsync();

                // Invalidate cache
                await _cacheService.RemoveAsync($"{CONFIG_CACHE_PREFIX}{config.Key}");

                return _mapper.Map<ConfigurationResponse>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration {Key}", request.Key);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GetAllConfigurationsAsync(string category = null)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var configs = await repo.GetListAsync(
                    predicate: string.IsNullOrEmpty(category) ? null : c => c.Category == category,
                    orderBy: q => q.OrderBy(c => c.Category).ThenBy(c => c.Key));

                return configs.ToDictionary(c => c.Key, c => c.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all configurations for category {Category}", category);
                return new Dictionary<string, string>();
            }
        }

        public async Task<bool> DeleteConfigurationAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be empty", nameof(key));

            try
            {
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Key == key);

                if (config == null)
                {
                    _logger.LogWarning("Configuration {Key} not found for deletion", key);
                    return false;
                }

                repo.DeleteAsync(config);
                await _unitOfWork.CommitAsync();

                // Invalidate cache
                await _cacheService.RemoveAsync($"{CONFIG_CACHE_PREFIX}{key}");

                _logger.LogInformation("Configuration {Key} deleted successfully", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting configuration {Key}", key);
                throw;
            }
        }

        #endregion

        #region Model Configuration

        public async Task<ModelConfigurationResponse> GetActiveModelConfigurationAsync(string modelType)
        {
            if (string.IsNullOrWhiteSpace(modelType))
                throw new ArgumentException("Model type cannot be empty", nameof(modelType));

            try
            {
                // Parse model type
                if (!Enum.TryParse<ModelType>(modelType, true, out var type))
                {
                    _logger.LogWarning("Invalid model type: {ModelType}", modelType);
                    return null;
                }

                // Check cache
                var cacheKey = $"{MODEL_CONFIG_CACHE_PREFIX}active:{modelType.ToLower()}";
                var cached = await _cacheService.GetAsync<ModelConfigurationResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                // Get from database with provider info
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var config = await repo.SingleOrDefaultAsync(
                    predicate: c => c.ModelType == type && c.IsActive,
                    orderBy: q => q.OrderByDescending(c => c.UpdatedAt),
                    include: source => source.Include(c => c.ModelProvider));

                if (config == null)
                {
                    _logger.LogWarning("No active configuration found for model type {ModelType}", modelType);
                    return null;
                }

                var response = _mapper.Map<ModelConfigurationResponse>(config);

                // Cache for 30 minutes
                await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(30));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active model configuration for type {ModelType}", modelType);
                throw;
            }
        }

        public async Task<List<ModelConfigurationResponse>> GetAllModelConfigurationsAsync(bool activeOnly = false)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var configs = await repo.GetListAsync(
                    predicate: activeOnly ? c => c.IsActive : null,
                    orderBy: q => q.OrderBy(c => c.ModelType).ThenByDescending(c => c.UpdatedAt),
                    include: source => source.Include(c => c.ModelProvider));

                return _mapper.Map<List<ModelConfigurationResponse>>(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model configurations");
                throw;
            }
        }

        public async Task<ModelConfigurationResponse> CreateModelConfigurationAsync(UpdateModelConfigurationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Validate model type
                if (!Enum.TryParse<ModelType>(request.ModelType, true, out var modelType))
                {
                    throw new ArgumentException($"Invalid model type: {request.ModelType}");
                }

                // Get default provider
                var providerRepo = _unitOfWork.GetRepository<ModelProvider>();
                var provider = await providerRepo.SingleOrDefaultAsync(
                    predicate: p => p.Name == "HuggingFace" && p.IsActive);

                if (provider == null)
                {
                    throw new InvalidOperationException("No active HuggingFace provider found");
                }

                var config = _mapper.Map<ModelConfiguration>(request);
                config.ModelProviderId = provider.Id;
                config.CreatedAt = DateTime.UtcNow;

                // Deactivate other models if this is active
                if (config.IsActive)
                {
                    await DeactivateOtherModelsAsync(config.ModelType, 0);
                }

                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                await repo.InsertAsync(config);
                await _unitOfWork.CommitAsync();

                // Clear cache
                await _cacheService.RemoveByPrefixAsync(MODEL_CONFIG_CACHE_PREFIX);

                _logger.LogInformation("Created model configuration for {ModelType}: {ModelName}",
                    config.ModelType, config.ModelName);

                // Load with provider for response
                config.ModelProvider = provider;
                return _mapper.Map<ModelConfigurationResponse>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model configuration");
                throw;
            }
        }

        public async Task<ModelConfigurationResponse> UpdateModelConfigurationAsync(int id, UpdateModelConfigurationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var config = await repo.SingleOrDefaultAsync(
                    predicate: c => c.Id == id,
                    include: source => source.Include(c => c.ModelProvider));

                if (config == null)
                {
                    throw new KeyNotFoundException($"Model configuration with ID {id} not found");
                }

                // Log changes for audit
                var originalActive = config.IsActive;
                var originalName = config.ModelName;

                // Update fields
                config.ModelName = request.ModelName;
                config.Endpoint = request.Endpoint;
                config.MaxTokens = request.MaxTokens;
                config.Temperature = request.Temperature;
                config.TopP = request.TopP;
                config.UpdatedAt = DateTime.UtcNow;

                // Handle activation changes
                if (request.IsActive && !originalActive)
                {
                    await DeactivateOtherModelsAsync(config.ModelType, config.Id);
                    config.IsActive = true;
                    _logger.LogInformation("Activated model configuration {Id} - {Name}", id, config.ModelName);
                }
                else if (!request.IsActive && originalActive)
                {
                    config.IsActive = false;
                    _logger.LogWarning("Deactivated model configuration {Id} - {Name}. No active model for type {Type}",
                        id, config.ModelName, config.ModelType);
                }

                repo.UpdateAsync(config);
                await _unitOfWork.CommitAsync();

                // Clear all model config cache
                await _cacheService.RemoveByPrefixAsync(MODEL_CONFIG_CACHE_PREFIX);

                _logger.LogInformation("Updated model configuration {Id}: {OldName} -> {NewName}",
                    id, originalName, config.ModelName);

                return _mapper.Map<ModelConfigurationResponse>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model configuration {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeleteModelConfigurationAsync(int id)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Id == id);

                if (config == null)
                {
                    _logger.LogWarning("Model configuration {Id} not found for deletion", id);
                    return false;
                }

                // Prevent deleting active configuration
                if (config.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Cannot delete active model configuration. Deactivate it first or activate another {config.ModelType} model.");
                }

                // Check if this is the last configuration for this model type
                var sameTypeCount = await repo.GetListAsync(
                    predicate: c => c.ModelType == config.ModelType && c.Id != id);

                if (!sameTypeCount.Any())
                {
                    _logger.LogWarning("Cannot delete the last configuration for model type {Type}", config.ModelType);
                    throw new InvalidOperationException(
                        $"Cannot delete the last configuration for model type {config.ModelType}. At least one configuration must exist.");
                }

                repo.DeleteAsync(config);
                await _unitOfWork.CommitAsync();

                // Clear cache
                await _cacheService.RemoveByPrefixAsync(MODEL_CONFIG_CACHE_PREFIX);

                _logger.LogInformation("Deleted model configuration {Id} - {Name}", id, config.ModelName);
                return true;
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw business logic exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model configuration {Id}", id);
                throw;
            }
        }

        public async Task<bool> ActivateModelConfigurationAsync(int id)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Id == id);

                if (config == null)
                {
                    throw new KeyNotFoundException($"Model configuration with ID {id} not found");
                }

                if (config.IsActive)
                {
                    _logger.LogInformation("Model configuration {Id} is already active", id);
                    return true;
                }

                // Test connection before activation
                var canConnect = await TestModelConnectionAsync(id);
                if (!canConnect)
                {
                    throw new InvalidOperationException(
                        $"Cannot activate model configuration {config.ModelName}. Connection test failed.");
                }

                // Deactivate others of the same type
                await DeactivateOtherModelsAsync(config.ModelType, config.Id);

                // Activate this one
                config.IsActive = true;
                config.UpdatedAt = DateTime.UtcNow;
                repo.UpdateAsync(config);
                await _unitOfWork.CommitAsync();

                // Clear cache
                await _cacheService.RemoveByPrefixAsync(MODEL_CONFIG_CACHE_PREFIX);

                _logger.LogInformation("Activated model configuration {Id} - {Name} for type {Type}",
                    id, config.ModelName, config.ModelType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating model configuration {Id}", id);
                throw;
            }
        }

        #endregion
        #region Model Providers

        public async Task<List<ModelProviderResponse>> GetModelProvidersAsync(bool activeOnly = false)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<ModelProvider>();
                var providers = await repo.GetListAsync(
                    predicate: activeOnly ? p => p.IsActive : null,
                    orderBy: q => q.OrderBy(p => p.Name),
                    include: source => source.Include(p => p.ModelConfigurations));

                var responses = new List<ModelProviderResponse>();
                foreach (var provider in providers)
                {
                    var response = _mapper.Map<ModelProviderResponse>(provider);

                    // Add statistics
                    response.Metadata = new Dictionary<string, object>
                    {
                        ["totalModels"] = provider.ModelConfigurations?.Count ?? 0,
                        ["activeModels"] = provider.ModelConfigurations?.Count(m => m.IsActive) ?? 0,
                        ["modelTypes"] = provider.ModelConfigurations?
                            .GroupBy(m => m.ModelType)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count()) ?? new Dictionary<string, int>()
                    };

                    responses.Add(response);
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model providers");
                throw;
            }
        }

        public async Task<bool> TestModelConnectionAsync(int modelConfigId)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<ModelConfiguration>();
                var config = await repo.SingleOrDefaultAsync(
                    predicate: c => c.Id == modelConfigId,
                    include: source => source.Include(c => c.ModelProvider));

                if (config == null)
                {
                    throw new KeyNotFoundException($"Model configuration with ID {modelConfigId} not found");
                }

                // Create HTTP client with timeout
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                // Add API key if provider requires it
                if (config.ModelProvider != null && !string.IsNullOrEmpty(config.ModelProvider.ApiKeyName))
                {
                    var apiKey = await GetConfigurationAsync<string>(config.ModelProvider.ApiKeyName, null);
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }
                }

                // Test endpoint with HEAD request first, then GET
                HttpResponseMessage response;
                try
                {
                    response = await httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, config.Endpoint));
                }
                catch
                {
                    // If HEAD fails, try GET
                    response = await httpClient.GetAsync(config.Endpoint);
                }

                var isSuccess = response.IsSuccessStatusCode ||
                               response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                               response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed;

                _logger.LogInformation("Model connection test for {ModelName} ({Id}): {Status} - {StatusCode}",
                    config.ModelName, modelConfigId, isSuccess ? "Success" : "Failed", response.StatusCode);

                return isSuccess;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Model connection test failed for ID {ModelConfigId} - Network error", modelConfigId);
                return false;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Model connection test timed out for ID {ModelConfigId}", modelConfigId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing model connection for ID {ModelConfigId}", modelConfigId);
                throw;
            }
        }

        #endregion

        #region Private Methods

        private T ConvertValue<T>(string value, T defaultValue)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return defaultValue;

                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // Handle common types
                if (underlyingType == typeof(string))
                    return (T)(object)value;

                if (underlyingType == typeof(bool))
                    return (T)(object)bool.Parse(value);

                if (underlyingType == typeof(int))
                    return (T)(object)int.Parse(value);

                if (underlyingType == typeof(long))
                    return (T)(object)long.Parse(value);

                if (underlyingType == typeof(double))
                    return (T)(object)double.Parse(value);

                if (underlyingType == typeof(decimal))
                    return (T)(object)decimal.Parse(value);

                if (underlyingType == typeof(DateTime))
                    return (T)(object)DateTime.Parse(value);

                if (underlyingType == typeof(TimeSpan))
                    return (T)(object)TimeSpan.Parse(value);

                if (underlyingType.IsEnum)
                    return (T)Enum.Parse(underlyingType, value, true);

                // Try JSON deserialization for complex types
                return JsonSerializer.Deserialize<T>(value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert value '{Value}' to type {Type}, using default",
                    value, typeof(T).Name);
                return defaultValue;
            }
        }

        private async Task DeactivateOtherModelsAsync(ModelType modelType, int excludeId)
        {
            var repo = _unitOfWork.GetRepository<ModelConfiguration>();
            var activeConfigs = await repo.GetListAsync(
                predicate: c => c.ModelType == modelType && c.IsActive && c.Id != excludeId);

            foreach (var config in activeConfigs)
            {
                config.IsActive = false;
                config.UpdatedAt = DateTime.UtcNow;
                repo.UpdateAsync(config);

                _logger.LogInformation("Deactivated model configuration {Id} - {Name}",
                    config.Id, config.ModelName);
            }
        }

        #endregion
    }
}