using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response;
using ChatBox.Infrastructure.Paginate;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
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

        // Core Messaging
        [HttpPost("messages")]
        public async Task<ActionResult<SendMessageResponse>> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = GetUserId();
                var ipAddress = GetIpAddress();
                var userAgent = GetUserAgent();

                var response = await _chatService.SendMessageAsync(userId, request, ipAddress, userAgent);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("streaming")]
        public async Task<ActionResult<StreamingResponse>> StartStreaming([FromBody] StreamChatRequest request)
        {
            try
            {
                var userId = GetUserId();
                var connectionId = HttpContext.Connection.Id ?? Guid.NewGuid().ToString();

                var response = await _chatService.StartStreamingAsync(userId, request, connectionId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting streaming");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("streaming/{messageId}/cancel")]
        public async Task<ActionResult<bool>> CancelStreaming(Guid messageId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.CancelStreamingAsync(userId, messageId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling streaming for message {MessageId}", messageId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Message Management
        [HttpGet("messages/{messageId}")]
        public async Task<ActionResult<AdvancedMessageResponse>> GetMessage(Guid messageId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GetMessageAsync(userId, messageId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", messageId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<ActionResult<bool>> DeleteMessage(Guid messageId, [FromQuery] string reason = "user_request")
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.DeleteMessageAsync(userId, messageId, reason);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("feedback")]
        public async Task<ActionResult<bool>> AddFeedback([FromBody] FeedbackRequest request)
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.AddFeedbackAsync(userId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding feedback");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Session Management
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

        // Advanced Features
        [HttpPost("sessions/{sessionId}/summary")]
        public async Task<ActionResult<ConversationSummaryResponse>> GenerateSummary(Guid sessionId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GenerateSummaryAsync(userId, sessionId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }
}
