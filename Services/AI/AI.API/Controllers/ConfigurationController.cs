using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AI.API.Constants;

namespace AI.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route(ApiEndPointConstant.API_PREFIX + "/admin/configuration")]
    public class ConfigurationController : BaseApiController
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<ConfigurationController> _logger;

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
        [HttpGet("system")]
        [ProducesResponseType(typeof(Dictionary<string, string>), 200)]
        public async Task<IActionResult> GetSystemConfigurations([FromQuery] string category = null)
        {
            try
            {
                var configs = await _configService.GetAllConfigurationsAsync(category);

                return Ok(new
                {
                    success = true,
                    configurations = configs,
                    category,
                    count = configs.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system configurations");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get a specific configuration value
        /// </summary>
        [HttpGet("system/{key}")]
        [ProducesResponseType(typeof(ConfigurationResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetConfiguration(string key)
        {
            try
            {
                var value = await _configService.GetConfigurationAsync<string>(key, null);

                if (value == null)
                {
                    return HandleNotFound("Configuration", key);
                }

                return Ok(new ConfigurationResponse
                {
                    Success = true,
                    Key = key,
                    Value = value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration {Key}", key);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Create or update a system configuration
        /// </summary>
        [HttpPost("system")]
        [ProducesResponseType(typeof(ConfigurationResponse), 200)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> SetConfiguration([FromBody][Required] UpdateConfigurationRequest request)
        {
            try
            {
                var response = await _configService.SetConfigurationAsync(request);

                _logger.LogInformation("Configuration {Key} updated by {User}",
                    request.Key, User.Identity?.Name);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Delete a system configuration
        /// </summary>
        [HttpDelete("system/{key}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> DeleteConfiguration(string key)
        {
            try
            {
                var deleted = await _configService.DeleteConfigurationAsync(key);

                if (!deleted)
                {
                    return HandleNotFound("Configuration", key);
                }

                _logger.LogInformation("Configuration {Key} deleted by {User}",
                    key, User.Identity?.Name);

                return Ok(new
                {
                    success = true,
                    message = $"Configuration '{key}' deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting configuration {Key}", key);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get all model configurations
        /// </summary>
        [HttpGet("models")]
        [ProducesResponseType(typeof(List<ModelConfigurationResponse>), 200)]
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
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get active model configuration by type
        /// </summary>
        [HttpGet("models/{modelType}/active")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetActiveModelConfiguration(string modelType)
        {
            try
            {
                var config = await _configService.GetActiveModelConfigurationAsync(modelType);

                if (config == null)
                {
                    return HandleNotFound("Active model configuration", modelType);
                }

                return Ok(config);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active model configuration");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Create a new model configuration
        /// </summary>
        [HttpPost("models")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 201)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> CreateModelConfiguration([FromBody][Required] UpdateModelConfigurationRequest request)
        {
            try
            {
                var response = await _configService.CreateModelConfigurationAsync(request);

                _logger.LogInformation("Model configuration created for {ModelType} by {User}",
                    request.ModelType, User.Identity?.Name);

                return CreatedAtAction(
                    nameof(GetActiveModelConfiguration),
                    new { modelType = request.ModelType },
                    response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model configuration");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Update an existing model configuration
        /// </summary>
        [HttpPut("models/{id}")]
        [ProducesResponseType(typeof(ModelConfigurationResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> UpdateModelConfiguration(int id, [FromBody][Required] UpdateModelConfigurationRequest request)
        {
            try
            {
                var response = await _configService.UpdateModelConfigurationAsync(id, request);

                _logger.LogInformation("Model configuration {Id} updated by {User}",
                    id, User.Identity?.Name);

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return HandleNotFound("Model configuration", id);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model configuration {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Delete a model configuration
        /// </summary>
        [HttpDelete("models/{id}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> DeleteModelConfiguration(int id)
        {
            try
            {
                var deleted = await _configService.DeleteModelConfigurationAsync(id);

                if (!deleted)
                {
                    return HandleNotFound("Model configuration", id);
                }

                _logger.LogInformation("Model configuration {Id} deleted by {User}",
                    id, User.Identity?.Name);

                return Ok(new
                {
                    success = true,
                    message = $"Model configuration {id} deleted successfully"
                });
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model configuration {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Activate a model configuration
        /// </summary>
        [HttpPost("models/{id}/activate")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> ActivateModelConfiguration(int id)
        {
            try
            {
                var activated = await _configService.ActivateModelConfigurationAsync(id);

                if (!activated)
                {
                    return HandleBadRequest("Failed to activate model configuration");
                }

                _logger.LogInformation("Model configuration {Id} activated by {User}",
                    id, User.Identity?.Name);

                return Ok(new
                {
                    success = true,
                    message = $"Model configuration {id} activated successfully"
                });
            }
            catch (KeyNotFoundException)
            {
                return HandleNotFound("Model configuration", id);
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating model configuration {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Test model connection
        /// </summary>
        [HttpPost("models/{id}/test")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> TestModelConnection(int id)
        {
            try
            {
                var success = await _configService.TestModelConnectionAsync(id);

                return Ok(new
                {
                    success,
                    message = success ? "Connection successful" : "Connection failed",
                    testedAt = DateTime.UtcNow
                });
            }
            catch (KeyNotFoundException)
            {
                return HandleNotFound("Model configuration", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing model connection {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get all model providers
        /// </summary>
        [HttpGet("providers")]
        [ProducesResponseType(typeof(List<ModelProviderResponse>), 200)]
        public async Task<IActionResult> GetModelProviders([FromQuery] bool activeOnly = false)
        {
            try
            {
                var providers = await _configService.GetModelProvidersAsync(activeOnly);

                return Ok(new
                {
                    success = true,
                    providers,
                    count = providers.Count,
                    activeOnly
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model providers");
                return HandleError(ex);
            }
        }
    }
}
