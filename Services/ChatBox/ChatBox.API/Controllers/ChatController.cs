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
    [Route(ApiEndPointConstant.ApiEndpoint)]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
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

                _logger.LogInformation("Message sent successfully for user {UserId}, session {SessionId}, model {ModelName}",
                    userId, response.SessionId, response.ModelUsed);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid message request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("thay đổi model") || ex.Message.Contains("đã bị tắt"))
            {
                // Modern flow violations - clear error messages
                _logger.LogWarning("Model operation not allowed: {Error}", ex.Message);
                return BadRequest(new
                {
                    error = ex.Message,
                    code = "MODEL_SWITCH_NOT_ALLOWED",
                    suggestion = "Vui lòng tạo session mới để sử dụng model khác."
                });
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
                var validation = await _chatService.ValidateMessageAsync(request.Message);

                if (!validation.Success)
                {
                    Response.StatusCode = 400;
                    await Response.WriteAsync($"Error: {validation.Message}");
                    return;
                }

                var responseStream = await _chatService.SendMessageStreamAsync(request, userId);

                Response.StatusCode = 200;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";
                Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

                _logger.LogInformation("Starting streaming response for user {UserId}", userId);

                await using var writer = new StreamWriter(Response.Body, leaveOpen: true);
                await foreach (var chunk in responseStream)
                {
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        _logger.LogInformation("Streaming cancelled by client");
                        break;
                    }

                    _logger.LogDebug("Sending chunk: {Chunk}", chunk);
                    await writer.WriteAsync(chunk);
                    await writer.FlushAsync();
                    await Response.Body.FlushAsync(); // Force flush the response body
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("thay đổi model"))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming failed");
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
                return Created($"{ApiEndPointConstant.ApiEndpoint}/{ApiEndPointConstant.Chat.GetSession.Replace("{sessionId}", response.Id)}", response);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("không khả dụng"))
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không có model nào"))
            {
                return StatusCode(503, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session");
                return Problem(MessageConstant.Chat.CreateSessionFailed);
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
                return NotFound(MessageConstant.Chat.SessionNotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
                return Problem(MessageConstant.Chat.GetSessionFailed);
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
                return Problem(MessageConstant.Chat.GetSessionsFailed);
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
                    return NotFound(MessageConstant.Chat.SessionNotFound);

                return Ok(MessageConstant.Chat.SessionDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session");
                return Problem(MessageConstant.Chat.DeleteSessionFailed);
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
                    return NotFound(MessageConstant.Chat.SessionNotFound);

                return Ok(MessageConstant.Chat.ModelSwitched);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("đã có conversation"))
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch model");
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("không có model nào"))
            {
                return StatusCode(503, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available models");
                return Problem(MessageConstant.Chat.GetModelsFailed);
            }
        }
    }
}

