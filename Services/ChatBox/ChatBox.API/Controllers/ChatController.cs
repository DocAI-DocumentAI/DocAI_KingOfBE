using ChatBox.API.Services.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using Microsoft.AspNetCore.Authorization;

namespace ChatBox.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        // Constructor injection với null checks
        public ChatController(ILogger<ChatController> logger, IChatService chatService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        // Phương thức hỗ trợ để lấy UserId từ JWT
        // Đảm bảo JWT Authentication được cấu hình ở lớp cao hơn (API Gateway hoặc Auth Service)
        private string GetUserIdFromJwt()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; // Hoặc ClaimTypes.Name, tùy theo cách bạn lưu trữ User ID
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("User ID not found in JWT token claims.");
                // Tùy theo chính sách: ném AuthenticationException hoặc trả về Unauthorized
                throw new UnauthorizedAccessException("User is not authenticated or user ID claim is missing.");
            }
            return userId;
        }

        // REVIEW POINT: Endpoint để bắt đầu một cuộc hội thoại mới
        [HttpPost("start")]
        [Authorize] // Yêu cầu xác thực
        [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Unauthorized if JWT missing/invalid
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartNewConversation([FromBody] ChatRequestPayload request)
        {
            var userId = GetUserIdFromJwt(); // Lấy User ID từ JWT
            if (string.IsNullOrEmpty(request.Question))
            {
                _logger.LogWarning("StartNewConversation request received with empty question for user {UserId}.", userId);
                return BadRequest("Initial question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Starting new conversation for user {UserId} with question: {Question}", userId, request.Question);
                var response = await _chatService.StartNewConversationAsync(userId, request);
                return CreatedAtAction(nameof(StartNewConversation), new { conversationId = response.Id }, response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for StartNewConversation.");
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new conversation for user {UserId} with question: {Question}", userId, request.Question);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to start conversation");
            }
        }

        // REVIEW POINT: Endpoint để lấy danh sách các cuộc hội thoại của người dùng
        [HttpGet("conversations")]
        [Authorize]
        [ProducesResponseType(typeof(List<ConversationSummaryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConversations()
        {
            var userId = GetUserIdFromJwt();
            try
            {
                _logger.LogInformation("Retrieving conversations for user {UserId}.", userId);
                var conversations = await _chatService.GetUserConversationsAsync(userId);
                return Ok(conversations);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for GetConversations.");
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversations for user {UserId}.", userId);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to retrieve conversations");
            }
        }

        // REVIEW POINT: Endpoint để lấy lịch sử tin nhắn của một cuộc hội thoại cụ thể
        [HttpGet("conversations/{conversationId}/history")]
        [Authorize]
        [ProducesResponseType(typeof(List<MessageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConversationHistory(string conversationId)
        {
            var userId = GetUserIdFromJwt();
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("Conversation ID cannot be empty.");
            }
            try
            {
                _logger.LogInformation("Retrieving history for conversation {ConversationId} for user {UserId}.", conversationId, userId);
                var history = await _chatService.GetConversationHistoryAsync(conversationId, userId);
                if (!history.Any())
                {
                    _logger.LogWarning("Conversation {ConversationId} not found or has no history for user {UserId}.", conversationId, userId);
                    return NotFound($"Conversation {conversationId} not found or has no history.");
                }
                return Ok(history);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for GetConversationHistory for conversation {ConversationId}.", conversationId);
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (InvalidOperationException ex) // Bắt lỗi nếu conversation không tồn tại hoặc không được ủy quyền
            {
                _logger.LogWarning(ex, "Conversation {ConversationId} not found or unauthorized for user {UserId}.", conversationId, userId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting history for conversation {ConversationId} for user {UserId}.", conversationId, userId);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to retrieve conversation history");
            }
        }

        // REVIEW POINT: Endpoint để tiếp tục chat (non-streaming)
        [HttpPost("conversations/{conversationId}/chat")]
        [Authorize]
        [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ContinueChat(string conversationId, [FromBody] ChatRequestPayload request)
        {
            var userId = GetUserIdFromJwt();
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Conversation ID and Question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Continuing chat in conversation {ConversationId} for user {UserId} with question: {Question}", conversationId, userId, request.Question);
                var response = await _chatService.ContinueChatAsync(conversationId, userId, request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for ContinueChat for conversation {ConversationId}.", conversationId);
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Conversation {ConversationId} not found or unauthorized for user {UserId}.", conversationId, userId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error continuing chat in conversation {ConversationId} for user {UserId}.", conversationId, userId);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to continue chat");
            }
        }

        // REVIEW POINT: Endpoint để tiếp tục chat (streaming)
        [HttpPost("conversations/{conversationId}/stream-chat")]
        [Authorize]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StreamContinueChat(string conversationId, [FromBody] ChatRequestPayload request)
        {
            var userId = GetUserIdFromJwt();
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Conversation ID and Question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Streaming chat requested for conversation {ConversationId} for user {UserId} with question: {Question}", conversationId, userId, request.Question);
                return Ok(_chatService.StreamContinueChatAsync(conversationId, userId, request));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for StreamContinueChat for conversation {ConversationId}.", conversationId);
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Conversation {ConversationId} not found or unauthorized for user {UserId}.", conversationId, userId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming chat in conversation {ConversationId} for user {UserId}.", conversationId, userId);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to stream chat");
            }
        }

        // REVIEW POINT: Endpoint để xóa cuộc hội thoại
        [HttpDelete("conversations/{conversationId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)] // 204 No Content cho xóa thành công
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteConversation(string conversationId)
        {
            var userId = GetUserIdFromJwt();
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("Conversation ID cannot be empty.");
            }

            try
            {
                var isDeleted = await _chatService.DeleteConversationAsync(conversationId, userId);
                if (!isDeleted)
                {
                    // Nếu service trả về false, có nghĩa là không tìm thấy hoặc không được phép xóa
                    _logger.LogWarning("Conversation {ConversationId} not found or unauthorized for deletion by user {UserId}.", conversationId, userId);
                    return NotFound($"Conversation {conversationId} not found or you are not authorized to delete it.");
                }
                _logger.LogInformation("Conversation {ConversationId} deleted successfully for user {UserId}.", conversationId, userId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access attempt for DeleteConversation for conversation {ConversationId}.", conversationId);
                return Unauthorized("Authentication failed or user ID is missing.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId} for user {UserId}.", conversationId, userId);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to delete conversation");
            }
        }
    }
}
