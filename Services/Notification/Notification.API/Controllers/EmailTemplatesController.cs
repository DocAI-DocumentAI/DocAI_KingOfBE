using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Infrastructure.Filter;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/email-templates")]
    [Authorize(Roles = "Admin")]
    // [Authorize(Roles = "Admin")] // Chỉ Admin mới có quyền quản lý template
    public class EmailTemplatesController : ControllerBase
    {
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<EmailTemplatesController> _logger;

        public EmailTemplatesController(IEmailTemplateService emailTemplateService, ILogger<EmailTemplatesController> logger)
        {
            _emailTemplateService = emailTemplateService ?? throw new ArgumentNullException(nameof(emailTemplateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEmailTemplate([FromBody] EmailTemplateRequest request)
        {
            var response = await _emailTemplateService.CreateEmailTemplateAsync(request);
            return CreatedAtAction(nameof(GetEmailTemplateById), new { id = response.Id }, response);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmailTemplateById(Guid id)
        {
            var template = await _emailTemplateService.GetEmailTemplateByIdAsync(id);
            return template != null ? Ok(template) : NotFound();
        }

        [HttpGet]
        [ProducesResponseType(typeof(IPaginate<EmailTemplateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IPaginate<EmailTemplateResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(IPaginate<EmailTemplateResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllEmailTemplates(int page = 1, int size = 30,
        [FromQuery] EmailTemplateFilter? filter = null, string? sortBy = null, bool isAsc = true)
        {
            var templates = await _emailTemplateService.GetAllEmailTemplatesAsync(page, size, filter, sortBy, isAsc);
            return Ok(templates);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEmailTemplate(Guid id, [FromBody] EmailTemplateRequest request)
        {
            var response = await _emailTemplateService.UpdateEmailTemplateAsync(id, request);
            return Ok(response);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEmailTemplate(Guid id)
        {
            var result = await _emailTemplateService.DeleteEmailTemplateAsync(id);
            return result ? NoContent() : NotFound();
        }

        [HttpPost("preview")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PreviewEmailTemplate([FromBody] PreviewEmailTemplateRequest request)
        {
            var renderedHtml = await _emailTemplateService.PreviewEmailTemplateAsync(request);
            return Ok(renderedHtml);
        }
    }
}
