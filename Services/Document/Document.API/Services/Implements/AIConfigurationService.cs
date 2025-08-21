using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shared.Exceptions;

namespace Document.API.Services.Implements;

/// <summary>
/// Service implementation for managing AI configurations
/// </summary>
public class AIConfigurationService : IAIConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<AIConfigurationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _memoryCache;

    public AIConfigurationService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<AIConfigurationService> logger,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Gets the current user ID from JWT token
    /// </summary>
    /// <returns>Current user ID</returns>
    private string GetCurrentUserId()
    {
        return JwtTokenHelper.GetUserId(_httpContextAccessor);
    }

    public async Task<AIConfigurationResponse?> GetDefaultConfigurationAsync()
    {
        var configuration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.IsDefault && ai.DeletedTime == null);

        if (configuration == null)
            return null;

        return _mapper.Map<AIConfigurationResponse>(configuration);
    }

    public async Task<AIConfigurationResponse?> GetConfigurationByIdAsync(string id)
    {
        var configuration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.Id == id && ai.DeletedTime == null);

        if (configuration == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.AIConfigurationNotFound);
        }

        return _mapper.Map<AIConfigurationResponse>(configuration);
    }

    public async Task<List<AIConfigurationResponse>> GetAllConfigurationsAsync()
    {
        var configurations = await _unitOfWork.GetRepository<AIConfiguration>()
            .GetListAsync(
                predicate: ai => ai.DeletedTime == null,
                orderBy: q => q.OrderByDescending(ai => ai.IsDefault).ThenBy(ai => ai.ModelName)
            );

        return _mapper.Map<List<AIConfigurationResponse>>(configurations);
    }

    public async Task<AIConfigurationResponse> CreateConfigurationAsync(CreateAIConfigurationRequest request)
    {
        // Map request to domain entity
        var configuration = _mapper.Map<AIConfiguration>(request);
        configuration.Id = Guid.NewGuid().ToString();

        // Validate model name uniqueness
        var existingConfiguration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.ModelName.ToLower() == request.ModelName.ToLower() && ai.DeletedTime == null);

        if (existingConfiguration != null)
        {
            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.AIConfigurationModelNameExists);
        }

        // Set audit fields
        var currentUserId = GetCurrentUserId();
        configuration.CreatedBy = currentUserId;
        configuration.CreatedTime = DateTime.UtcNow;

        // If this is set as default, unset others
        if (configuration.IsDefault)
        {
            await UnsetAllDefaultsAsync();
        }

        await _unitOfWork.GetRepository<AIConfiguration>().InsertAsync(configuration);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created AI configuration: {ModelName} ({Id}) by user {UserId}",
            configuration.ModelName, configuration.Id, currentUserId);

        return _mapper.Map<AIConfigurationResponse>(configuration);
    }

    public async Task<AIConfigurationResponse> UpdateConfigurationAsync(string id, UpdateAIConfigurationRequest request)
    {
        // Check if configuration exists
        var existingConfiguration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.Id == id && ai.DeletedTime == null);

        if (existingConfiguration == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.AIConfigurationNotFound);
        }

        // Validate model name uniqueness (excluding current configuration)
        var duplicateConfiguration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.ModelName.ToLower() == request.ModelName.ToLower()
                                                  && ai.Id != id
                                                  && ai.DeletedTime == null);

        if (duplicateConfiguration != null)
        {
            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.AIConfigurationModelNameExists);
        }

        // Update properties from request
        _mapper.Map(request, existingConfiguration);

        // Set audit fields
        var currentUserId = GetCurrentUserId();
        existingConfiguration.LastUpdatedBy = currentUserId;
        existingConfiguration.LastUpdatedTime = DateTime.UtcNow;

        // If this is set as default, unset others
        if (existingConfiguration.IsDefault)
        {
            await UnsetAllDefaultsAsync();
        }

        await _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(existingConfiguration);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Updated AI configuration: {ModelName} ({Id}) by user {UserId}",
            existingConfiguration.ModelName, existingConfiguration.Id, currentUserId);

        return _mapper.Map<AIConfigurationResponse>(existingConfiguration);
    }

    public async Task DeleteConfigurationAsync(string id)
    {
        var configuration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.Id == id && ai.DeletedTime == null);

        if (configuration == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.AIConfigurationNotFound);
        }

        // Set audit fields for soft delete
        var currentUserId = GetCurrentUserId();
        configuration.DeletedBy = currentUserId;
        configuration.DeletedTime = DateTime.UtcNow;
        configuration.IsDefault = false; // Remove default status if deleted

        await _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(configuration);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Deleted AI configuration: {ModelName} ({Id}) by user {UserId}",
            configuration.ModelName, configuration.Id, currentUserId);
    }

    public async Task<AIConfigurationResponse> SetDefaultConfigurationAsync(string id)
    {
        var configuration = await _unitOfWork.GetRepository<AIConfiguration>()
            .SingleOrDefaultAsync(predicate: ai => ai.Id == id && ai.DeletedTime == null);

        if (configuration == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.AIConfigurationNotFound);
        }

        // Unset all defaults first
        await UnsetAllDefaultsAsync();

        // Set this one as default
        var currentUserId = GetCurrentUserId();
        configuration.IsDefault = true;
        configuration.LastUpdatedBy = currentUserId;
        configuration.LastUpdatedTime = DateTime.UtcNow;

        await _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(configuration);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Set AI configuration as default: {ModelName} ({Id}) by user {UserId}",
            configuration.ModelName, configuration.Id, currentUserId);

        return _mapper.Map<AIConfigurationResponse>(configuration);
    }

    public async Task<AIConfigurationResponse> InitializeDefaultConfigurationAsync()
    {
        // Check if default configuration already exists
        var existingDefault = await GetDefaultConfigurationAsync();
        if (existingDefault != null)
        {
            return existingDefault;
        }

        // Create default configuration
        var defaultRequest = new CreateAIConfigurationRequest
        {
            ModelName = "openai/gpt-4o-mini",
            ModelId = "GPT-4o Mini",
            MaxToken = 2000,
            SystemPrompt = "You are a helpful document analysis assistant. Analyze the provided document and extract the requested information accurately and concisely.",
            IsDefault = true
        };

        return await CreateConfigurationAsync(defaultRequest);
    }

    private async Task UnsetAllDefaultsAsync()
    {
        var defaultConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
            .GetListAsync(predicate: ai => ai.IsDefault && ai.DeletedTime == null);

        var currentUserId = GetCurrentUserId();
        foreach (var config in defaultConfigs)
        {
            config.IsDefault = false;
            config.LastUpdatedBy = currentUserId;
            config.LastUpdatedTime = DateTime.UtcNow;
            await _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
        }
    }

}
