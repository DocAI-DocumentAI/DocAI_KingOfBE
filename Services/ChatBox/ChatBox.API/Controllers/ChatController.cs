using ChatBox.API.Services.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Attributes;

namespace ChatBox.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;
        public ChatController(ILogger<ChatController> logger, IChatService chatService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        private (string UserId, List<string> UserRoles, List<string> UserPermissions) GetUserContext()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("User ID claim not found in JWT token for authenticated user. This indicates a misconfiguration in JWT claims.");
                throw new UnauthorizedAccessException("User is authenticated but user ID claim is missing.");
            }

            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var userPermissions = User.FindAll("permissions").Select(c => c.Value).ToList();

            return (userId, userRoles, userPermissions);
        }

        [HttpPost("start")]
        [CusTomAuthorize]
        [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartNewConversation([FromBody] ChatRequestPayload request)
        {
            var (userId, userRoles, userPermissions) = GetUserContext(); 
            if (string.IsNullOrEmpty(request.Question))
            {
                _logger.LogWarning("StartNewConversation request received with empty question for user {UserId}.", userId);
                return BadRequest("Initial question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Starting new conversation for user {UserId} with roles {Roles} and question: {Question}", userId, string.Join(",", userRoles), request.Question);
                // Truyền roles xuống service
                var response = await _chatService.StartNewConversationAsync(userId,userRoles, request); 
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
        [CusTomAuthorize]
        [ProducesResponseType(typeof(List<ConversationSummaryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConversations()
        {
            var (userId, userRoles, userPermissions) = GetUserContext(); 
            try
            {
                _logger.LogInformation("Retrieving conversations for user {UserId} with roles {Roles}.", userId, string.Join(",", userRoles));
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

        [HttpGet("conversations/{conversationId}/history")]
        [CusTomAuthorize]
        [ProducesResponseType(typeof(List<MessageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConversationHistory(string conversationId)
        {
            var (userId, userRoles, userPermissions) = GetUserContext(); 
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("Conversation ID cannot be empty.");
            }
            try
            {
                _logger.LogInformation("Retrieving history for conversation {ConversationId} for user {UserId} with roles {Roles}.", conversationId, userId, string.Join(",", userRoles));
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
            catch (InvalidOperationException ex)
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
        [CusTomAuthorize]
        [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ContinueChat(string conversationId, [FromBody] ChatRequestPayload request)
        {
            var (userId, userRoles, userPermissions) = GetUserContext(); // REVIEW POINT: Lấy userId, roles, permissions
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Conversation ID and Question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Continuing chat in conversation {ConversationId} for user {UserId} with question: {Question}", conversationId, userId, request.Question);
                var response = await _chatService.ContinueChatAsync(conversationId, request.Question, userId,userRoles);
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
        [CusTomAuthorize]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StreamContinueChat(string conversationId, [FromBody] ChatRequestPayload request)
        {
            var (userId, userRoles, userPermissions) = GetUserContext();
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Conversation ID and Question cannot be empty.");
            }

            try
            {
                _logger.LogInformation("Streaming chat requested for conversation {ConversationId} for user {UserId} with roles {Roles} and question: {Question}", conversationId, userId, string.Join(",", userRoles), request.Question);
                return Ok(_chatService.StreamContinueChatAsync(conversationId, request.Question, userId, userRoles)); 
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
        [CusTomAuthorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteConversation(string conversationId)
        {
            var (userId, userRoles, userPermissions) = GetUserContext();
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("Conversation ID cannot be empty.");
            }

            try
            {
                var isDeleted = await _chatService.DeleteConversationAsync(conversationId, userId);
                if (!isDeleted)
                {
                    _logger.LogWarning("Conversation {ConversationId} not found or unauthorized for deletion by user {UserId}.", conversationId, userId);
                    return NotFound($"Conversation {conversationId} not found or you are not authorized to delete it.");
                }
                _logger.LogInformation("Conversation {ConversationId} deleted successfully by user {UserId}.", conversationId, userId);
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
