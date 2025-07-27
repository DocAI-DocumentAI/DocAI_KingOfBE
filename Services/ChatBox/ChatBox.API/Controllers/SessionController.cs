using System.Security.Claims;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Services.Interfaces;
using ChatBox.Infrastructure.Paginate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ITokenValidationService _tokenValidationService;
        private readonly ILogger<ChatController> _logger;
        public SessionController(
           IChatService chatService,
           ITokenValidationService tokenValidationService,
           ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _tokenValidationService = tokenValidationService;
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
        [HttpPost("sessions")]
        public async Task<ActionResult<AdvancedSessionResponse>> CreateSession([FromBody] CreateSessionRequest request)
        {
            try
            {
                var userId = GetUserId();
                var ipAddress = GetIpAddress();
                var userAgent = GetUserAgent();

                var response = await _chatService.CreateSessionAsync(userId, request, ipAddress, userAgent);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<AdvancedSessionResponse>> GetSession(Guid sessionId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GetSessionAsync(userId, sessionId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("sessions")]
        public async Task<ActionResult<IPaginate<SessionSummaryResponse>>> GetSessions([FromQuery] GetSessionsRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GetSessionsAsync(userId, request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("sessions/{sessionId}")]
        public async Task<ActionResult<bool>> DeleteSession(Guid sessionId, [FromQuery] string reason = "user_request")
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.DeleteSessionAsync(userId, sessionId, reason);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpPost("search")]
        public async Task<ActionResult> SearchConversations([FromBody] SearchRequest request)
        {
            try
            {
                var userId = GetUserId();
                var results = await _chatService.SearchConversationsAsync(userId, request);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching conversations");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
