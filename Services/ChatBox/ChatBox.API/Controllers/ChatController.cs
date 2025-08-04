using Azure;
using ChatBox.API.Attributes;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
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
                var responseStream = await _chatService.SendMessageStreamAsync(request, userId);

                Response.StatusCode = 200;
                Response.ContentType = "text/event-stream"; 
                Response.Headers["Cache-Control"] = "no-cache";

                await using var writer = new StreamWriter(Response.Body);

                await foreach (var chunk in responseStream)
                {
                    await writer.WriteAsync(chunk);
                        await writer.FlushAsync(); 
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid stream request: {Error}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start message stream");
            }
        }

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

        [HttpPost(ApiEndPointConstant.Chat.ValidateMessage)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ValidateMessageAsync([FromBody] string message)
        {
            var validation = await _chatService.ValidateMessageAsync(message);
            return Ok(validation);
        }

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

