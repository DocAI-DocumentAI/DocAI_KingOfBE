using AI.API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using AI.API.Payload.Request;
using AI.API.Atributte;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AIModelConfigController : ControllerBase
    {
        private readonly IAIModelConfigService _modelConfigService;
        private readonly ILogger<AIModelConfigController> _logger;

        public AIModelConfigController(
            IAIModelConfigService modelConfigService,
            ILogger<AIModelConfigController> logger)
        {
            _modelConfigService = modelConfigService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllModels()
        {
            try
            {
                var models = await _modelConfigService.GetAllModelsAsync();
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AI models");
                return BadRequest($"Failed to get models: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetModel(int id)
        {
            try
            {
                var model = await _modelConfigService.GetModelByIdAsync(id);
                if (model == null)
                    return NotFound($"Model with ID {id} not found");

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model {ModelId}", id);
                return BadRequest($"Failed to get model: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateModel(int id, [FromBody] UpdateAIModelConfigRequest request)
        {
            try
            {
                var success = await _modelConfigService.UpdateModelAsync(id, request);
                if (!success)
                    return NotFound($"Model with ID {id} not found");

                return Ok(new { message = "Model updated successfully", modelId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update model {ModelId}", id);
                return BadRequest($"Failed to update model: {ex.Message}");
            }
        }

        [HttpPost("{id}/test")]
        public async Task<IActionResult> TestModel(int id)
        {
            try
            {
                var result = await _modelConfigService.TestModelAsync(id);
                return Ok(new
                {
                    success = result.Success,
                    responseTimeMs = result.ResponseTimeMs,
                    error = result.Error ?? "",
                    response = result.Response ?? "",
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test model {ModelId}", id);
                return BadRequest($"Failed to test model: {ex.Message}");
            }
        }

        [HttpPost("{id}/activate")]
        public async Task<IActionResult> ActivateModel(int id)
        {
            try
            {
                var success = await _modelConfigService.ActivateModelAsync(id);
                if (!success)
                    return NotFound($"Model with ID {id} not found");

                return Ok(new { message = "Model activated successfully", modelId = id });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate model {ModelId}", id);
                return BadRequest($"Failed to activate model: {ex.Message}");
            }
        }

        [HttpGet("providers")]
        public async Task<IActionResult> GetSupportedProviders()
        {
            try
            {
                var providers = await _modelConfigService.GetSupportedProvidersAsync();
                return Ok(providers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get supported providers");
                return BadRequest($"Failed to get providers: {ex.Message}");
            }
        }
    }

    public class UpdateModelRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? Endpoint { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiVersion { get; set; }
        public string? Description { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
