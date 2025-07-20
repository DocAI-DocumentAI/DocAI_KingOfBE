using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace AI.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route(ApiEndPointConstant.API_PREFIX + "/admin/templates")]
    public class PromptTemplateController : BaseApiController
    {
        private readonly IPromptTemplateService _templateService;
        private readonly ILogger<PromptTemplateController> _logger;

        public PromptTemplateController(
            IPromptTemplateService templateService,
            ILogger<PromptTemplateController> logger)
        {
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all prompt templates
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<PromptTemplateSummary>), 200)]
        public async Task<IActionResult> GetAllTemplates(
            [FromQuery] string category = null,
            [FromQuery] bool activeOnly = true)
        {
            try
            {
                var templates = await _templateService.GetAllTemplatesAsync(category, activeOnly);

                return Ok(new
                {
                    success = true,
                    templates,
                    count = templates.Count,
                    filters = new { category, activeOnly }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting templates");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get template by name
        /// </summary>
        [HttpGet("by-name/{name}")]
        [ProducesResponseType(typeof(PromptTemplateResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetTemplateByName(string name)
        {
            try
            {
                var template = await _templateService.GetTemplateAsync(name);

                if (template == null || !template.Success)
                {
                    return HandleNotFound("Template", name);
                }

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template {Name}", name);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get template by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PromptTemplateResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetTemplateById(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null || !template.Success)
                {
                    return HandleNotFound("Template", id);
                }

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Create a new prompt template
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(PromptTemplateResponse), 201)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> CreateTemplate([FromBody][Required] CreatePromptTemplateRequest request)
        {
            try
            {
                var response = await _templateService.CreateTemplateAsync(request);

                if (!response.Success)
                {
                    return HandleBadRequest(response.Message);
                }

                _logger.LogInformation("Template {Name} created by {User}",
                    request.Name, User.Identity?.Name);

                return CreatedAtAction(
                    nameof(GetTemplateById),
                    new { id = response.Id },
                    response);
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating template");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Update an existing prompt template
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(PromptTemplateResponse), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> UpdateTemplate(int id, [FromBody][Required] UpdatePromptTemplateRequest request)
        {
            try
            {
                var response = await _templateService.UpdateTemplateAsync(id, request);

                if (!response.Success)
                {
                    if (response.Message?.Contains("not found") == true)
                    {
                        return HandleNotFound("Template", id);
                    }
                    return HandleBadRequest(response.Message);
                }

                _logger.LogInformation("Template {Id} updated by {User}",
                    id, User.Identity?.Name);

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return HandleNotFound("Template", id);
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating template {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Delete a prompt template
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            try
            {
                var deleted = await _templateService.DeleteTemplateAsync(id);

                if (!deleted)
                {
                    return HandleNotFound("Template", id);
                }

                _logger.LogInformation("Template {Id} deleted by {User}",
                    id, User.Identity?.Name);

                return Ok(new
                {
                    success = true,
                    message = $"Template {id} deleted successfully"
                });
            }
            catch (InvalidOperationException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting template {Id}", id);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Test render a template with variables
        /// </summary>
        [HttpPost("test-render")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> TestRender([FromBody][Required] TestRenderRequest request)
        {
            try
            {
                var rendered = await _templateService.RenderTemplateAsync(
                    request.TemplateName,
                    request.Variables ?? new Dictionary<string, string>());

                return Ok(new
                {
                    success = true,
                    templateName = request.TemplateName,
                    rendered,
                    variableCount = request.Variables?.Count ?? 0
                });
            }
            catch (KeyNotFoundException ex)
            {
                return HandleNotFound("Template", request.TemplateName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering template {Name}", request.TemplateName);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Validate template syntax
        /// </summary>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> ValidateTemplate([FromBody][Required] ValidateTemplateRequest request)
        {
            try
            {
                var isValid = await _templateService.ValidateTemplateAsync(
                    request.Template,
                    request.Variables ?? new Dictionary<string, string>());

                return Ok(new
                {
                    success = true,
                    isValid,
                    template = request.Template,
                    variableCount = request.Variables?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating template");
                return HandleError(ex);
            }
        }
    }

    public class TestRenderRequest
    {
        [Required]
        public string TemplateName { get; set; }
        public Dictionary<string, string> Variables { get; set; }
    }

    public class ValidateTemplateRequest
    {
        [Required]
        public string Template { get; set; }
        public Dictionary<string, string> Variables { get; set; }
    }
}
