using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AI.API.Extensions;

namespace AI.API.Controllers
{
    /// <summary>
    /// Controller for managing system configurations and AI model settings
    /// </summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/configuration")]
    [Produces("application/json")]
    [ApiExplorerSettings(GroupName = "Configuration")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<ConfigurationController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class
        /// </summary>
        /// <param name="configService">Configuration service</param>
        /// <param name="logger">Logger instance</param>
        public ConfigurationController(
            IConfigurationService configService,
            ILogger<ConfigurationController> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all system configurations
        /// </summary>
        /// <param name="category">Optional category to filter by</param>
        /// <returns>List of system configurations</returns>
        [HttpGet("system")]
        [ProducesResponseType(typeof(SystemConfigurationListResponse), 200)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> GetSystemConfigurations([FromQuery] string category = null)
        {
            try
            {
                var configs = await _configService.GetAllConfigurationsAsync(category);
                var response = new SystemConfigurationListResponse
                {
                    Success = true,
                    Configurations = configs,
                    Category = category,
                    Count = configs.Count
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system configurations");
                return BadRequest(new ErrorResponse("Error getting system configurations", ex.Message));
            }
        }

        /// <summary>
        /// Get a specific configuration value
        /// </summary>
        /// <param name="key">The configuration key to retrieve</param>
        /// <returns>Configuration value</returns>
        [HttpGet("system/{key}")]
        [ProducesResponseType(typeof(ConfigurationResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> GetConfiguration(string key)
        {
            try
            {
                // Get the configuration value with strong typing
                var value = await _configService.GetConfigurationAsync<string>(key, null);
                if (value == null)
                {
                    return NotFound(new ErrorResponse("not_found", $"Configuration with key '{key}' not found."));
                }
                
                return Ok(new ConfigurationResponse { Success = true, Key = key, Value = value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration {Key}", key);
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Create or update a system configuration
        /// </summary>
        /// <param name="request">Configuration details</param>
        /// <returns>Updated configuration</returns>
        [HttpPost("system")]
        [ProducesResponseType(typeof(ConfigurationResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> SetConfiguration([FromBody][Required] UpdateConfigurationRequest request)
        {
            try
            {
                var response = await _configService.SetConfigurationAsync(request);
                _logger.LogInformation("Configuration {Key} updated by {User}", request.Key, User.GetUserId());
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponse("invalid_argument", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration");
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Delete a system configuration
        /// </summary>
        /// <param name="key">Configuration key to delete</param>
        /// <returns>Success response</returns>
        [HttpDelete("system/{key}")]
        [ProducesResponseType(typeof(ActionSuccessResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> DeleteConfiguration(string key)
        {
            try
            {
                var deleted = await _configService.DeleteConfigurationAsync(key);
                if (!deleted)
                {
                    return NotFound(new ErrorResponse("not_found", $"Configuration with key '{key}' not found."));
                }
                _logger.LogInformation("Configuration {Key} deleted by {User}", key, User.GetUserId());
                return Ok(new ActionSuccessResponse { Success = true, Message = $"Configuration '{key}' deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting configuration {Key}", key);
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Get all model configurations
        /// </summary>
        /// <param name="activeOnly">If true, returns only active configurations</param>
        /// <returns>List of model configurations</returns>
        [HttpGet("models")]
        [ProducesResponseType(typeof(List<ModelConfigurationResponse>), 200)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> GetModelConfigurations([FromQuery] bool activeOnly = false)
        {
            try
            {
                var configs = await _configService.GetAllModelConfigurationsAsync(activeOnly);

                return Ok(new
                {
                    success = true,
                    configurations = configs,
                    count = configs.Count,
                    activeOnly
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model configurations");
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Get active model configuration by type
        /// </summary>
        /// <param name="modelType">Type of model (Chat, Embedding, etc.)</param>
        /// <returns>Active model configuration</returns>
        [HttpGet("active-model/{modelType}")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> GetActiveModelConfiguration(string modelType)
        {
            try
            {
                var config = await _configService.GetActiveModelConfigurationAsync(modelType);
                if (config == null)
                {
                    return NotFound(new ErrorResponse("not_found", $"Active model configuration for type '{modelType}' not found."));
                }
                return Ok(config);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponse("invalid_argument", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active model configuration");
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Create a new model configuration
        /// </summary>
        /// <param name="request">Model configuration details</param>
        /// <returns>Created model configuration</returns>
        [HttpPost("models")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 201)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> CreateModelConfiguration([FromBody][Required] UpdateModelConfigurationRequest request)
        {
            try
            {
                var response = await _configService.CreateModelConfigurationAsync(request);
                _logger.LogInformation("Model configuration created for {ModelType} by {User}", request.ModelType, User.GetUserId());
                return CreatedAtAction(nameof(GetActiveModelConfiguration), new { modelType = request.ModelType }, response);
            }
            catch (ArgumentException ex) { return BadRequest(new ErrorResponse("invalid_argument", ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse("invalid_operation", ex.Message)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model configuration");
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Update an existing model configuration
        /// </summary>
        /// <param name="id">ID of the model configuration to update</param>
        /// <param name="request">Updated configuration details</param>
        /// <returns>Updated model configuration</returns>
        [HttpPut("models/{id}")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> UpdateModelConfiguration(int id, [FromBody][Required] UpdateModelConfigurationRequest request)
        {
            try
            {
                var response = await _configService.UpdateModelConfigurationAsync(id, request);
                _logger.LogInformation("Model configuration {Id} updated by {User}", id, User.GetUserId());
                return Ok(response);
            }
            catch (KeyNotFoundException) { return NotFound(new ErrorResponse("not_found", $"Model configuration with ID '{id}' not found.")); }
            catch (ArgumentException ex) { return BadRequest(new ErrorResponse("invalid_argument", ex.Message)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model configuration {Id}", id);
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Delete a model configuration
        /// </summary>
        /// <param name="id">ID of the model configuration to delete</param>
        /// <returns>Success response</returns>
        [HttpDelete("models/{id}")]
        [ProducesResponseType(typeof(ActionSuccessResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> DeleteModelConfiguration(int id)
        {
            try
            {
                var deleted = await _configService.DeleteModelConfigurationAsync(id);
                if (!deleted)
                {
                    return NotFound(new ErrorResponse("not_found", $"Model configuration with ID '{id}' not found."));
                }
                _logger.LogInformation("Model configuration {Id} deleted by {User}", id, User.GetUserId());
                return Ok(new ActionSuccessResponse { Success = true, Message = $"Model configuration {id} deleted successfully" });
            }
            catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse("invalid_operation", ex.Message)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model configuration {Id}", id);
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Activate a model configuration
        /// </summary>
        /// <param name="id">ID of the model configuration to activate</param>
        /// <returns>Success response</returns>
        [HttpPost("models/{id}/activate")]
        [ProducesResponseType(typeof(ActionSuccessResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> ActivateModelConfiguration(int id)
        {
            try
            {
                await _configService.ActivateModelConfigurationAsync(id);
                _logger.LogInformation("Model configuration {Id} activated by {User}", id, User.GetUserId());
                return Ok(new ActionSuccessResponse { Success = true, Message = $"Model configuration {id} activated successfully" });
            }
            catch (KeyNotFoundException) { return NotFound(new ErrorResponse("not_found", $"Model configuration with ID '{id}' not found.")); }
            catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse("invalid_operation", ex.Message)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating model configuration {Id}", id);
                return BadRequest(new ErrorResponse("config_error", ex.Message));
            }
        }

        /// <summary>
        /// Test model connection
        /// </summary>
        /// <param name="id">ID of the model configuration to test</param>
        /// <returns>Test connection result</returns>
        [HttpPost("models/{id}/test-connection")]
        [ProducesResponseType(typeof(TestConnectionResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> TestModelConnection(int id)
        {
            try
            {
                var success = await _configService.TestModelConnectionAsync(id);
                return Ok(new TestConnectionResponse
                {
                    Success = success,
                    Message = success ? "Connection successful" : "Connection failed",
                    TestedAt = DateTime.UtcNow
                });
            }
            catch (KeyNotFoundException) { return NotFound(new ErrorResponse("not_found", $"Model configuration with ID '{id}' not found.")); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing model connection {Id}", id);
                return BadRequest(new ErrorResponse("connection_error", ex.Message));
            }
        }

        /// <summary>
        /// Get all model providers
        /// </summary>
        /// <param name="activeOnly">If true, returns only active providers</param>
        /// <returns>List of model providers</returns>
        [HttpGet("providers")]
        [ProducesResponseType(typeof(ModelProviderListResponse), 200)]
        [ProducesErrorResponseType(typeof(ErrorResponse))]
        public async Task<IActionResult> GetModelProviders([FromQuery] bool activeOnly = false)
        {
            try
            {
                var providers = await _configService.GetModelProvidersAsync(activeOnly);
                var response = new ModelProviderListResponse
                {
                    Success = true,
                    Providers = providers,
                    Count = providers.Count,
                    ActiveOnly = activeOnly
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model providers");
                return BadRequest(new ErrorResponse("provider_error", ex.Message));
            }
        }
    }
} 