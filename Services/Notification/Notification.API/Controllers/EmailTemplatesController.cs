using Microsoft.AspNetCore.Mvc;
using Notification.Api.Constants;
using Notification.API.Attributes;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Infrastructure.Filter;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Controllers
{
    /// <summary>
    /// API quản lý Email Templates - tạo, sửa, xóa các mẫu email thông báo
    /// </summary>
    [ApiController]
    [Route(ApiEndpointConstant.ApiEndpoint)]
    public class EmailTemplateController : ControllerBase
    {
        private readonly IEmailTemplateService _templateService;
        private readonly ILogger<EmailTemplateController> _logger;

        public EmailTemplateController(
            IEmailTemplateService templateService,
            ILogger<EmailTemplateController> logger)
        {
            _templateService = templateService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("User ID not found in token");
        }

        /// <summary>
        /// Lấy danh sách tất cả email templates
        /// </summary>
        [HttpGet(ApiEndpointConstant.EmailTemplate.GetAll)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(IPaginate<EmailTemplateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllTemplatesAsync(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? sortBy = "CreateAt",
            [FromQuery] bool isAsc = false)
        {
            try
            {
                var templates = await _templateService.GetAllEmailTemplatesAsync(page, size, sortBy, isAsc);
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get email templates");
                return Problem("Failed to retrieve email templates");
            }
        }

        /// <summary>
        /// Lấy email template theo ID
        /// </summary>
        [HttpGet(ApiEndpointConstant.EmailTemplate.GetById)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetTemplateByIdAsync(Guid id)
        {
            try
            {
                var template = await _templateService.GetEmailTemplateByIdAsync(id);
                if (template == null)
                    return NotFound(MessageConstant.EmailTemplate.NotFound);

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get email template {TemplateId}", id);
                return Problem("Failed to retrieve email template");
            }
        }

        /// <summary>
        /// Lấy email template theo tên
        /// </summary>
        [HttpGet(ApiEndpointConstant.EmailTemplate.GetByName)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager, Roles.Editor })]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetTemplateByNameAsync(string templateName)
        {
            try
            {
                var template = await _templateService.GetEmailTemplateByNameAsync(templateName);
                if (template == null)
                    return NotFound(MessageConstant.EmailTemplate.NotFound);

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get email template {TemplateName}", templateName);
                return Problem("Failed to retrieve email template");
            }
        }

        /// <summary>
        /// Tạo email template mới
        /// </summary>
        [HttpPost(ApiEndpointConstant.EmailTemplate.Create)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateTemplateAsync([FromBody] EmailTemplateRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _templateService.CreateEmailTemplateAsync(request);

                _logger.LogInformation("Email template created: {TemplateName} by {UserId}",
                    request.TemplateName, userId);

                return Created($"{ApiEndpointConstant.EmailTemplate.GetById.Replace("{id:guid}", response.Id.ToString())}", response);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogWarning("Invalid email template request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create email template");
                return Problem("Failed to create email template");
            }
        }

        /// <summary>
        /// Cập nhật email template
        /// </summary>
        [HttpPut(ApiEndpointConstant.EmailTemplate.Update)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateTemplateAsync(Guid id, [FromBody] EmailTemplateRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _templateService.UpdateEmailTemplateAsync(id, request);

                _logger.LogInformation("Email template updated: {TemplateId} by {UserId}", id, userId);
                return Ok(response);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogWarning("Invalid update request for template {TemplateId}: {Error}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update email template {TemplateId}", id);
                return Problem("Failed to update email template");
            }
        }

        /// <summary>
        /// Xóa email template
        /// </summary>
        [HttpDelete(ApiEndpointConstant.EmailTemplate.Delete)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteTemplateAsync(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var success = await _templateService.DeleteEmailTemplateAsync(id);

                if (!success)
                    return NotFound(MessageConstant.EmailTemplate.NotFound);

                _logger.LogInformation("Email template deleted: {TemplateId} by {UserId}", id, userId);
                return Ok(MessageConstant.EmailTemplate.DeleteSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete email template {TemplateId}", id);
                return Problem("Failed to delete email template");
            }
        }
    }
}
