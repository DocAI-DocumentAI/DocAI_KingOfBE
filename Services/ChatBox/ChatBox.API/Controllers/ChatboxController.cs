using Azure;
using ChatBox.API.Attributes;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.Claims;
using System.Text;

namespace ChatBox.API.Controllers
{
    /// <summary>
    /// API chat chính - gửi tin nhắn và quản lý session
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ChatboxController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatboxController> _logger;

        public ChatboxController(IChatService chatService, ILogger<ChatboxController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("User ID not found in token");
        }
        private static bool IsEndOfSentence(string chunk)
        {
            return chunk.Contains('.') || chunk.Contains('!') || chunk.Contains('?') ||
                   chunk.Contains('\n') || chunk.Contains('。') || chunk.Contains('！') || chunk.Contains('？');
        }
        /// <summary>
        /// Gửi tin nhắn và nhận phản hồi AI (có tích hợp RAG tự động)
        /// </summary>
        [HttpPost(ApiEndPointConstant.Chat.SendMessage)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendMessageAsync([FromBody] ChatRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.SendMessageAsync(request, userId);

                _logger.LogInformation("Message sent successfully for user {UserId}, session {SessionId}",
                    userId, response.SessionId);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid message request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message");
                return Problem(MessageConstant.Chat.SendFailed);
            }
        }
        /// <summary>
        /// Gửi tin nhắn và nhận phản hồi AI theo dạng streaming real-time
        /// </summary>
        [HttpPost(ApiEndPointConstant.Chat.SendMessageStream)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task SendMessageStreamAsync([FromBody] ChatRequest request)
        {

            try
            {
                var userId = GetUserId();

                // 🔧 FIXED: Validate trước khi stream để tránh lãng phí
                var validation = await _chatService.ValidateMessageAsync(request.Message);
                if (!validation.Success)
                {
                    Response.StatusCode = 400;
                    await Response.WriteAsync($"Error: {validation.Message}");
                    return;
                }

                var responseStream = await _chatService.SendMessageStreamAsync(request, userId);

                Response.StatusCode = 200;
                Response.ContentType = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                // 🔧 FIXED: Simple buffer để giảm số lần flush
                var buffer = new StringBuilder();
                var tokenCount = 0;
                const int bufferThreshold = 50; // Buffer 50 tokens trước khi flush

                await using var writer = new StreamWriter(Response.Body);

                await foreach (var chunk in responseStream)
                {
                    // 🔧 FIXED: Check client disconnect
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        _logger.LogInformation("Client disconnected during streaming");
                        break;
                    }

                    buffer.Append(chunk);
                    tokenCount++;

                    // 🔧 FIXED: Flush buffer khi đủ tokens hoặc gặp dấu câu
                    if (tokenCount >= bufferThreshold || IsEndOfSentence(chunk))
                    {
                        await writer.WriteAsync(buffer.ToString());
                        await writer.FlushAsync();

                        buffer.Clear();
                        tokenCount = 0;
                    }
                }

                if (buffer.Length > 0)
                {
                    await writer.WriteAsync(buffer.ToString());
                    await writer.FlushAsync();
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid stream request: {Error}", ex.Message);
                Response.StatusCode = 400;
                await Response.WriteAsync($"Error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming cancelled by client");
                // Không cần làm gì thêm
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start message stream");
                Response.StatusCode = 500;
                await Response.WriteAsync($"Error: {MessageConstant.Chat.SendFailed}");
            }
        }
        /// <summary>
        /// Tạo session chat mới
        /// </summary>
        [HttpPost(ApiEndPointConstant.Chat.CreateSession)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateSessionAsync([FromBody] CreateSessionRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.CreateSessionAsync(request, userId);

                _logger.LogInformation("Session created successfully: {SessionId}", response.Id);

                return Created($"{ApiEndPointConstant.Chat.GetSession.Replace("{sessionId}", response.Id)}", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session");
                return Problem("Tạo phiên chat thất bại");
            }
        }
        /// <summary>
        /// Lấy thông tin chi tiết session và lịch sử chat
        /// </summary>
        [HttpGet(ApiEndPointConstant.Chat.GetSession)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(SessionDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSessionAsync(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GetSessionAsync(sessionId, userId);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Session not found: {SessionId}", sessionId);
                return NotFound(MessageConstant.Chat.SessionNotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
                return Problem("Lấy thông tin phiên chat thất bại");
            }
        }
        /// <summary>
        /// Lấy danh sách tất cả session của user
        /// </summary>
        [HttpGet(ApiEndPointConstant.Chat.GetUserSessions)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(List<SessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUserSessionsAsync()
        {
            try
            {
                var userId = GetUserId();
                var response = await _chatService.GetUserSessionsAsync(userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user sessions");
                return Problem("Lấy danh sách phiên chat thất bại");
            }
        }
        /// <summary>
        /// Xóa session (soft delete)
        /// </summary>
        [HttpDelete(ApiEndPointConstant.Chat.DeleteSession)]
        [CustomAuthorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteSessionAsync(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.DeleteSessionAsync(sessionId, userId);

                if (!result)
                {
                    return NotFound(MessageConstant.Chat.SessionNotFound);
                }

                _logger.LogInformation("Session deleted: {SessionId}", sessionId);
                return Ok(MessageConstant.Chat.SessionDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
                return Problem("Xóa phiên chat thất bại");
            }
        }
        /// <summary>
        /// Chuyển đổi model cho session - DISABLED (phải tạo session mới)
        /// </summary>
        [HttpPatch(ApiEndPointConstant.Chat.SwitchModel)]
        [CustomAuthorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SwitchSessionModelAsync(string sessionId, string newModelName)
        {
            try
            {
                var userId = GetUserId();
                var result = await _chatService.SwitchSessionModelAsync(sessionId, newModelName, userId);

                if (!result)
                {
                    return NotFound(MessageConstant.Chat.SessionNotFound);
                }

                _logger.LogInformation("Model switched for session {SessionId} to {ModelName}",
                    sessionId, newModelName);

                return Ok(MessageConstant.Chat.ModelSwitched);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch model for session {SessionId}", sessionId);
                return Problem(MessageConstant.Chat.ModelSwitchFailed);
            }
        }
        /// <summary>
        /// Validate tin nhắn trước khi gửi (độ dài, tokens)
        /// </summary>
        [HttpPost(ApiEndPointConstant.Chat.ValidateMessage)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ValidateMessageAsync([FromBody] string message)
        {
            var validation = await _chatService.ValidateMessageAsync(message);
            return Ok(validation);
        }
        /// <summary>
        /// Lấy danh sách các model AI khả dụng
        /// </summary>
        [HttpGet(ApiEndPointConstant.Chat.AvailableModels)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(List<AvailableModelResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableModelsAsync()
        {
            try
            {
                var models = await _chatService.GetAvailableModelsAsync();
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available models");
                return Problem("Lấy danh sách model thất bại");
            }
        }
    }
}

