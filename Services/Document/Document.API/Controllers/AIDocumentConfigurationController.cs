using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.Exceptions;

namespace Document.API.Controllers;

/// <summary>
/// Controller for managing AI configurations
/// </summary>
[Route(ApiEndPointConstant.ApiEndpoint)]
[ApiController]
[CustomAuthorize]
public class AIDocumentConfigurationController : ControllerBase
{
    private readonly IAIConfigurationService _aiConfigurationService;
    private readonly ILogger<AIDocumentConfigurationController> _logger;

    public AIDocumentConfigurationController(IAIConfigurationService aiConfigurationService, ILogger<AIDocumentConfigurationController> logger)
    {
        _aiConfigurationService = aiConfigurationService;
        _logger = logger;
    }

    /// <summary>
    /// (Document analyze) Get all AI configurations
    /// </summary>
    [HttpGet(ApiEndPointConstant.AIConfiguration.GetAll)]
    [ProducesResponseType(typeof(ApiResponse<List<AIConfigurationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllConfigurations()
    {
        var configurations = await _aiConfigurationService.GetAllConfigurationsAsync();
        return Ok(ApiResponse<object>.Success(configurations, "AI configurations retrieved successfully", StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Get default AI configuration
    /// </summary>
    [HttpGet(ApiEndPointConstant.AIConfiguration.GetDefault)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDefaultConfiguration()
    {
        var configuration = await _aiConfigurationService.GetDefaultConfigurationAsync();
        if (configuration == null)
        {
            return NotFound(ApiResponse<object>.Error(ErrorCode.NOT_FOUND, MessageConstant.AIConfigurationNotFound, StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.Success(configuration, "Default AI configuration retrieved successfully", StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Get AI configuration by ID
    /// </summary>
    [HttpGet(ApiEndPointConstant.AIConfiguration.GetById)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurationById(string id)
    {
        var configuration = await _aiConfigurationService.GetConfigurationByIdAsync(id);
        return Ok(ApiResponse<object>.Success(configuration, "AI configuration retrieved successfully", StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Create a new AI configuration
    /// </summary>
    [HttpPost(ApiEndPointConstant.AIConfiguration.Create)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateConfiguration([FromBody] CreateAIConfigurationRequest request)
    {
        var createdConfiguration = await _aiConfigurationService.CreateConfigurationAsync(request);
        return Ok(ApiResponse<object>.Success(createdConfiguration, MessageConstant.AIConfigurationCreatedSuccessfully, StatusCodes.Status201Created));
    }

    /// <summary>
    /// (Document analyze) Update an existing AI configuration
    /// </summary>
    [HttpPut(ApiEndPointConstant.AIConfiguration.Update)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfiguration(string id, [FromBody] UpdateAIConfigurationRequest request)
    {
        var updatedConfiguration = await _aiConfigurationService.UpdateConfigurationAsync(id, request);
        return Ok(ApiResponse<object>.Success(updatedConfiguration, MessageConstant.AIConfigurationUpdatedSuccessfully, StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Delete an AI configuration
    /// </summary>
    [HttpDelete(ApiEndPointConstant.AIConfiguration.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfiguration(string id)
    {
        await _aiConfigurationService.DeleteConfigurationAsync(id);
        return Ok(ApiResponse<object>.Success(null, MessageConstant.AIConfigurationDeletedSuccessfully, StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Set an AI configuration as default
    /// </summary>
    [HttpPost(ApiEndPointConstant.AIConfiguration.SetDefault)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetDefaultConfiguration(string id)
    {
        var updatedConfiguration = await _aiConfigurationService.SetDefaultConfigurationAsync(id);
        return Ok(ApiResponse<object>.Success(updatedConfiguration, MessageConstant.AIConfigurationSetAsDefaultSuccessfully, StatusCodes.Status200OK));
    }

    /// <summary>
    /// (Document analyze) Initialize default AI configuration (for setup)
    /// </summary>
    [HttpPost(ApiEndPointConstant.AIConfiguration.Initialize)]
    [ProducesResponseType(typeof(ApiResponse<AIConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InitializeDefaultConfiguration()
    {
        var defaultConfiguration = await _aiConfigurationService.InitializeDefaultConfigurationAsync();
        return Ok(ApiResponse<object>.Success(defaultConfiguration, MessageConstant.AIConfigurationDefaultInitialized, StatusCodes.Status200OK));
    }
}
