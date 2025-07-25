using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationOrchestrationService _conversationService;
        private readonly ILogger<ConversationController> _logger;

        public ConversationController(
            IConversationOrchestrationService conversationService, 
            ILogger<ConversationController> logger)
        {
            _conversationService = conversationService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string GetUserAgent()
        {
            return HttpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
        }

        /// <summary>
        /// Process a message through the conversation orchestration pipeline
        /// </summary>
        [HttpPost("process")]
        public async Task<ActionResult<ConversationResponse>> ProcessMessage([FromBody] ProcessMessageRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Enrich request with context information
                request.UserId = GetUserId();
                request.IpAddress = GetIpAddress();
                request.UserAgent = GetUserAgent();

                var response = await _conversationService.ProcessMessageAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message through conversation orchestration");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Execute RAG (Retrieval-Augmented Generation) workflow
        /// </summary>
        [HttpPost("rag")]
        public async Task<ActionResult<RAGResponse>> ExecuteRAGWorkflow([FromBody] RAGRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Set user context
                request.UserId = GetUserId();

                var response = await _conversationService.ExecuteRAGWorkflowAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing RAG workflow");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }

    // Helper request classes
    public class UpdateContextRequest
    {
        public Dictionary<string, object> Context { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? Topic { get; set; }
        public string? Intent { get; set; }
    }

    public class ExportConversationRequest
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string Format { get; set; } = "json"; // json, csv, pdf
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeAnalytics { get; set; } = false;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
