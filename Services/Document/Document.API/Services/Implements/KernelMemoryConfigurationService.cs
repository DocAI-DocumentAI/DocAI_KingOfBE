using Document.API.Constants;
using Document.API.Models;
using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;

using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.OpenAI;
using Microsoft.KernelMemory.SemanticKernel;
using Shared.Exceptions;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for dynamically configuring Kernel Memory with database AI settings
/// </summary>
public class KernelMemoryConfigurationService : IKernelMemoryConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<KernelMemoryConfigurationService> _logger;
    private const string CACHE_KEY_DEFAULT = "KernelMemory_Default";
    private const string CACHE_KEY_PREFIX = "KernelMemory_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    public KernelMemoryConfigurationService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<KernelMemoryConfigurationService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IKernelMemory> GetConfiguredKernelMemoryAsync()
    {
        // Try to get from cache first
        if (_memoryCache.TryGetValue(CACHE_KEY_DEFAULT, out IKernelMemory? cachedMemory) && cachedMemory != null)
        {
            _logger.LogDebug("Using cached default Kernel Memory configuration");
            return cachedMemory;
        }

        // Get default AI configuration from database
        var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.IsDefault && ai.DeletedTime == null);

        if (defaultConfig == null)
        {
            _logger.LogWarning("No default AI configuration found in database, using fallback configuration");
            return await CreateFallbackKernelMemoryAsync();
        }

        var kernelMemory = await CreateKernelMemoryAsync(defaultConfig);
        
        // Cache the configured instance
        _memoryCache.Set(CACHE_KEY_DEFAULT, kernelMemory, _cacheExpiration);
        
        _logger.LogInformation("Kernel Memory configured with database AI configuration: {ModelName} (MaxTokens: {MaxTokens})", 
            defaultConfig.ModelName, defaultConfig.MaxToken);

        return kernelMemory;
    }

    public async Task<IKernelMemory> GetConfiguredKernelMemoryAsync(string configurationId)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}{configurationId}";
        
        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out IKernelMemory? cachedMemory) && cachedMemory != null)
        {
            _logger.LogDebug("Using cached Kernel Memory configuration for ID: {ConfigurationId}", configurationId);
            return cachedMemory;
        }

        // Get specific AI configuration from database
        var config = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.Id == configurationId && ai.DeletedTime == null);

        if (config == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, 
                MessageConstant.AIConfigurationNotFound);
        }

        var kernelMemory = await CreateKernelMemoryAsync(config);
        
        // Cache the configured instance
        _memoryCache.Set(cacheKey, kernelMemory, _cacheExpiration);
        
        _logger.LogInformation("Kernel Memory configured with specific AI configuration: {ModelName} (MaxTokens: {MaxTokens})", 
            config.ModelName, config.MaxToken);

        return kernelMemory;
    }

    public async Task RefreshConfigurationAsync()
    {
        _logger.LogInformation("Refreshing Kernel Memory configuration cache");
        
        // Clear all cached configurations
        _memoryCache.Remove(CACHE_KEY_DEFAULT);
        
        // Clear specific configuration caches (this is a simple approach)
        // In a more sophisticated implementation, you might track cache keys
        
        _logger.LogInformation("Kernel Memory configuration cache cleared");
        await Task.CompletedTask;
    }

    private async Task<IKernelMemory> CreateKernelMemoryAsync(AIConfiguration config)
    {
        try
        {
            // Keep API keys and endpoints from appsettings (as requested)
            var openRouterConfig = _configuration.GetSection("OpenRouter").Get<OpenRouterConfigSetting>();
            var openAIConfig = _configuration.GetSection("OpenAI").Get<OpenAIConfigSetting>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Use database configuration for model and token settings
            var openRouterTextGenerationConfig = new OpenAIConfig
            {
                TextModel = config.ModelName, // From database
                APIKey = openRouterConfig?.APIKey ?? "",
                Endpoint = openRouterConfig?.Endpoint ?? "https://openrouter.ai/api/v1",
                TextModelMaxTokenTotal = 8192, // Limit to prevent rate limiting
                MaxRetries = 3 // Add retry logic for rate limiting
            };

            var openAITextEmbeddingConfig = new OpenAIConfig
            {
                EmbeddingModel = openAIConfig?.EmbeddingModel ?? "text-embedding-3-small",
                Endpoint = "https://gpt1.shupremium.com/v1",
                APIKey = openAIConfig?.APIKey ?? ""
            };

            var postgresConfig = new PostgresConfig
            {
                ConnectionString = connectionString ?? ""
            };

            KernelMemoryBuilderBuildOptions kmbOptions = new()
            {
                AllowMixingVolatileAndPersistentData = true
            };

            var memory = new KernelMemoryBuilder()
                .WithPostgresMemoryDb(postgresConfig)
                .WithOpenAITextGeneration(openRouterTextGenerationConfig, new CL100KTokenizer())
                .WithOpenAITextEmbeddingGeneration(openAITextEmbeddingConfig, new CL100KTokenizer())
                .WithSearchClientConfig(new()
                {
                    EmptyAnswer = "No results found. Please try again.",
                    AnswerTokens = config.MaxToken, // From database
                    MaxMatchesCount = 30,
                })
                .Build<MemoryServerless>(kmbOptions);

            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Kernel Memory with AI configuration: {ModelName}", config.ModelName);
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                $"Failed to configure Kernel Memory with AI configuration: {config.ModelName}");
        }
    }

    private async Task<IKernelMemory> CreateFallbackKernelMemoryAsync()
    {
        try
        {
            // Fallback to appsettings configuration
            var openRouterConfig = _configuration.GetSection("OpenRouter").Get<OpenRouterConfigSetting>();
            var openAIConfig = _configuration.GetSection("OpenAI").Get<OpenAIConfigSetting>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            var openRouterTextGenerationConfig = new OpenAIConfig
            {
                TextModel = openRouterConfig?.Model ?? "openai/gpt-oss-120b",
                APIKey = openRouterConfig?.APIKey ?? "",
                Endpoint = openRouterConfig?.Endpoint ?? "https://openrouter.ai/api/v1",
                TextModelMaxTokenTotal = 8192, // Conservative limit
                MaxRetries = 3 // Add retry logic for rate limiting
            };

            var openAITextEmbeddingConfig = new OpenAIConfig
            {
                EmbeddingModel = openAIConfig?.EmbeddingModel ?? "text-embedding-3-small",
                Endpoint = "https://gpt1.shupremium.com/v1",
                APIKey = openAIConfig?.APIKey ?? ""
            };

            var postgresConfig = new PostgresConfig
            {
                ConnectionString = connectionString ?? ""
            };

            KernelMemoryBuilderBuildOptions kmbOptions = new()
            {
                AllowMixingVolatileAndPersistentData = true
            };

            var memory = new KernelMemoryBuilder()
                .WithPostgresMemoryDb(postgresConfig)
                .WithOpenAITextGeneration(openRouterTextGenerationConfig, new CL100KTokenizer())
                .WithOpenAITextEmbeddingGeneration(openAITextEmbeddingConfig, new CL100KTokenizer())
                .WithSearchClientConfig(new()
                {
                    EmptyAnswer = "No results found. Please try again.",
                    AnswerTokens = 3500,
                    MaxMatchesCount = 30,
                })
                .Build<MemoryServerless>(kmbOptions);

            _logger.LogWarning("Using fallback Kernel Memory configuration from appsettings");
            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fallback Kernel Memory configuration");
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                "Failed to configure Kernel Memory with fallback settings");
        }
    }
}
