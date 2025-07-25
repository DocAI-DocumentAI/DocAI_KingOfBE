using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace AI.API.Services.Implement
{
    public class ConfigurationService : IAIConfigurationService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly IMapper _mapper;
        private const string CONFIG_CACHE_PREFIX = "config:";
        private const string MODEL_CONFIG_CACHE_PREFIX = "modelconfig:";

        public ConfigurationService(
            IUnitOfWork<DocAIDbContext> unitOfWork,
            ICacheService cacheService,
            ILogger<ConfigurationService> logger,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        #region System Configuration

        public async Task<AIModelConfig> GetActiveAIModelAsync()
        {
            try
            {
                var repo = _unitOfWork.GetRepository<AIModelConfig>();
                var activeModel = await repo.SingleOrDefaultAsync(predicate: x => x.IsActive);
                return activeModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active AI model configuration");
                return null;
            }
        }

        public async Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be empty", nameof(key));

            try
            {
                // Check cache first
                var cacheKey = $"{CONFIG_CACHE_PREFIX}{key.ToLowerInvariant()}";
                var cachedValue = await _cacheService.GetAsync<T>(cacheKey);
                if (cachedValue != null && !cachedValue.Equals(default(T)))
                {
                    _logger.LogDebug("Configuration cache hit for key {Key}", key);
                    return cachedValue;
                }

                // Get from database using repository
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Key == key);

                if (config == null)
                {
                    _logger.LogDebug("Configuration key {Key} not found, using default value", key);
                    return defaultValue;
                }

                // Convert and cache
                var value = ConvertValue<T>(config.Value, defaultValue);
                await _cacheService.SetAsync(cacheKey, value, TimeSpan.FromMinutes(15));

                _logger.LogDebug("Configuration loaded for key {Key}: {Value}", key, value);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for key {Key}", key);
                return defaultValue;
            }
        }

        public async Task<string> GetConfigurationAsync(string key)
        {
            return await GetConfigurationAsync<string>(key, null);
        }

        public async Task<List<ConfigurationResponse>> GetAllConfigurationsAsync(string category = null)
        {
            try
            {
                var cacheKey = $"{CONFIG_CACHE_PREFIX}all:{category ?? "null"}";
                var cached = await _cacheService.GetAsync<List<ConfigurationResponse>>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var configs = await repo.GetListAsync(
                    predicate: string.IsNullOrEmpty(category) ? null : c => c.Category == category,
                    orderBy: q => q.OrderBy(c => c.Category).ThenBy(c => c.Key));

                var result = _mapper.Map<List<ConfigurationResponse>>(configs);
                result.ForEach(r => r.Success = true);

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));

                _logger.LogInformation("Retrieved {Count} configurations for category '{Category}'", result.Count, category);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all configurations for category {Category}", category);
                return new List<ConfigurationResponse>();
            }
        }

        public async Task SetConfigurationAsync(string key, string value, string category = null, string description = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be empty", nameof(key));

            try
            {
                var repo = _unitOfWork.GetRepository<SystemConfiguration>();
                var config = await repo.SingleOrDefaultAsync(predicate: c => c.Key == key);

                if (config == null)
                {
                    config = new SystemConfiguration
                    {
                        Key = key,
                        Value = value,
                        Category = category ?? "General",
                        Description = description,
                        CreatedAt = DateTime.UtcNow
                    };
                    await repo.InsertAsync(config);
                    _logger.LogInformation("Created new configuration {Key} = {Value}", key, value);
                }
                else
                {
                    var oldValue = config.Value;
                    config.Value = value;
                    config.Category = category ?? config.Category;
                    config.Description = description ?? config.Description;
                    config.UpdatedAt = DateTime.UtcNow;
                    repo.UpdateAsync(config);
                    _logger.LogInformation("Updated configuration {Key}: {OldValue} -> {NewValue}", key, oldValue, value);
                }

                await _unitOfWork.CommitAsync();

                // Invalidate cache
                await _cacheService.RemoveAsync($"{CONFIG_CACHE_PREFIX}{key.ToLowerInvariant()}");
                await _cacheService.RemoveByPrefixAsync($"{CONFIG_CACHE_PREFIX}all:");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration {Key}", key);
                throw;
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
                await _cacheService.RemoveAsync($"{CONFIG_CACHE_PREFIX}{key.ToLowerInvariant()}");
                await _cacheService.RemoveByPrefixAsync($"{CONFIG_CACHE_PREFIX}all:");

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

        #region AI Model Configuration

        public async Task<AIModelConfigResponse> GetActiveTextGenerationConfigAsync()
        {
            try
            {
                var activeModel = await GetActiveAIModelAsync();
                if (activeModel == null)
                {
                    return new AIModelConfigResponse
                    {
                        Success = false,
                        Message = "No active AI model configuration found"
                    };
                }

                var response = _mapper.Map<AIModelConfigResponse>(activeModel);
                response.Success = true;
                response.Message = "Active configuration retrieved successfully";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active text generation config");
                return new AIModelConfigResponse
                {
                    Success = false,
                    Message = $"Error retrieving configuration: {ex.Message}"
                };
            }
        }

        public async Task<AIModelConfigResponse> SetTextGenerationConfigAsync(SetAIModelConfigRequest request)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<AIModelConfig>();

                // Deactivate all existing configs
                var existingConfigs = await repo.GetListAsync();
                foreach (var config in existingConfigs)
                {
                    config.IsActive = false;
                }

                // Create or update the new config
                var newConfig = _mapper.Map<AIModelConfig>(request);
                newConfig.IsActive = true;

                await repo.InsertAsync(newConfig);
                await _unitOfWork.CommitAsync();

                var response = _mapper.Map<AIModelConfigResponse>(newConfig);
                response.Success = true;
                response.Message = "Configuration set successfully";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting text generation config");
                return new AIModelConfigResponse
                {
                    Success = false,
                    Message = $"Error setting configuration: {ex.Message}"
                };
            }
        }

        public async Task<bool> TestTextGenerationConfigAsync(string modelId, string apiKey)
        {
            try
            {
                // Simple validation - in real implementation, you would test the actual connection
                return !string.IsNullOrEmpty(modelId) && !string.IsNullOrEmpty(apiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing text generation config");
                return false;
            }
        }

        public async Task<AIModelConfigResponse> GetCurrentConfigAsync()
        {
            return await GetActiveTextGenerationConfigAsync();
        }

        public async Task<bool> DeactivateCurrentConfigAsync()
        {
            try
            {
                var repo = _unitOfWork.GetRepository<AIModelConfig>();
                var activeConfigs = await repo.GetListAsync(predicate: x => x.IsActive);

                foreach (var config in activeConfigs)
                {
                    config.IsActive = false;
                }

                await _unitOfWork.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating current config");
                return false;
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

                // Handle nullable types
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType);
                }

                // Handle common types
                if (targetType == typeof(string))
                    return (T)(object)value;

                if (targetType == typeof(bool))
                    return (T)(object)bool.Parse(value);

                if (targetType == typeof(int))
                    return (T)(object)int.Parse(value);

                if (targetType == typeof(double))
                    return (T)(object)double.Parse(value);

                if (targetType == typeof(decimal))
                    return (T)(object)decimal.Parse(value);

                if (targetType == typeof(DateTime))
                    return (T)(object)DateTime.Parse(value);

                // Use Convert.ChangeType for other types
                return (T)Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert value '{Value}' to type {Type}, using default", value, typeof(T).Name);
                return defaultValue;
            }
        }

        #endregion

    }
}
