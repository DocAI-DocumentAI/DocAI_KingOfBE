using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IAIConfigurationService _configService;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(
            IAIConfigurationService configService,
            ILogger<ConfigurationController> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Get all system configurations
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ConfigurationResponse>>> GetAllConfigurations([FromQuery] string? category = null)
        {
            try
            {
                var configurations = await _configService.GetAllConfigurationsAsync(category);
                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configurations");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get configuration by key
        /// </summary>
        [HttpGet("{key}")]
        public async Task<ActionResult<string>> GetConfiguration(string key)
        {
            try
            {
                var value = await _configService.GetConfigurationAsync(key);
                if (value == null)
                {
                    return NotFound(new { message = "Configuration not found" });
                }
                return Ok(new { key, value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for key {Key}", key);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Set configuration value
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> SetConfiguration([FromBody] CreateConfigurationRequest request)
        {
            try
            {
                await _configService.SetConfigurationAsync(request.Key, request.Value, request.Category, request.Description);
                return Ok(new { message = "Configuration set successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting configuration");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get active AI model configuration
        /// [DEPRECATED] Use /api/ai-model-config endpoints instead
        /// </summary>
        [HttpGet("ai-model/active")]
        [Obsolete("This endpoint is deprecated. Use /api/ai-model-config endpoints instead.")]
        public async Task<ActionResult<AIModelConfigResponse>> GetActiveAIModelConfig()
        {
            return BadRequest(new {
                error = "This endpoint is deprecated",
                message = "Please use /api/ai-model-config endpoints instead",
                newEndpoint = "/api/ai-model-config"
            });
        }

        /// <summary>
        /// Set AI model configuration
        /// [DEPRECATED] Use /api/ai-model-config endpoints instead
        /// </summary>
        [HttpPost("ai-model")]
        [Obsolete("This endpoint is deprecated. Use /api/ai-model-config endpoints instead.")]
        public async Task<ActionResult<AIModelConfigResponse>> SetAIModelConfig([FromBody] SetAIModelConfigRequest request)
        {
            return BadRequest(new {
                error = "This endpoint is deprecated",
                message = "Please use /api/ai-model-config endpoints instead",
                newEndpoint = "/api/ai-model-config/{id}"
            });
        }

        /// <summary>
        /// Test AI model configuration
        /// [DEPRECATED] Use /api/ai-model-config endpoints instead
        /// </summary>
        [HttpPost("ai-model/test")]
        [Obsolete("This endpoint is deprecated. Use /api/ai-model-config endpoints instead.")]
        public async Task<ActionResult> TestAIModelConfig([FromBody] TestConnectionRequest request)
        {
            return BadRequest(new {
                error = "This endpoint is deprecated",
                message = "Please use /api/ai-model-config endpoints instead",
                newEndpoint = "/api/ai-model-config/{id}/test"
            });
        }
    }
}
