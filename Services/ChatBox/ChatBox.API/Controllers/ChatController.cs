using Azure;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("send")]
        public async Task<ActionResult<ApiResponse<ChatResponse>>> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _chatService.SendMessageAsync(request, userId);
                return Ok(ApiResponse<ChatResponse>.Ok(response));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<ChatResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ChatResponse>.Fail("Đã xảy ra lỗi khi xử lý tin nhắn."));
            }
        }

        [HttpPost("send-stream")]
        public async Task<IActionResult> SendMessageStream([FromBody] ChatRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var responseStream = await _chatService.SendMessageStreamAsync(request, userId);

                Response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                Response.Headers.Add("Cache-Control", "no-cache");
                Response.Headers.Add("Connection", "keep-alive");

                await foreach (var token in responseStream)
                {
                    await Response.WriteAsync(token);
                    await Response.Body.FlushAsync();
                }

                return new EmptyResult();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail("Đã xảy ra lỗi khi xử lý tin nhắn."));
            }
        }

        [HttpPost("sessions")]
        public async Task<ActionResult<ApiResponse<SessionResponse>>> CreateSession([FromBody] CreateSessionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _chatService.CreateSessionAsync(request, userId);
                return Ok(ApiResponse<SessionResponse>.Ok(response, "Tạo phiên chat thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<SessionResponse>.Fail("Đã xảy ra lỗi khi tạo phiên chat."));
            }
        }

        [HttpGet("sessions")]
        public async Task<ActionResult<ApiResponse<List<SessionResponse>>>> GetUserSessions()
        {
            try
            {
                var userId = GetCurrentUserId();
                var sessions = await _chatService.GetUserSessionsAsync(userId);
                return Ok(ApiResponse<List<SessionResponse>>.Ok(sessions));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<SessionResponse>>.Fail("Đã xảy ra lỗi khi lấy danh sách phiên chat."));
            }
        }

        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<ApiResponse<SessionDetailResponse>>> GetSession(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var session = await _chatService.GetSessionAsync(sessionId, userId);
                return Ok(ApiResponse<SessionDetailResponse>.Ok(session));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<SessionDetailResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<SessionDetailResponse>.Fail("Đã xảy ra lỗi khi lấy thông tin phiên chat."));
            }
        }

        [HttpDelete("sessions/{sessionId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteSession(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.DeleteSessionAsync(sessionId, userId);

                if (result)
                    return Ok(ApiResponse<bool>.Ok(true, "Xóa phiên chat thành công."));
                else
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy phiên chat."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi xóa phiên chat."));
            }
        }

        [HttpPost("suggest-title")]
        public async Task<ActionResult<ApiResponse<string>>> SuggestTitle([FromBody] string message)
        {
            try
            {
                var title = await _chatService.SuggestTitleAsync(message);
                return Ok(ApiResponse<string>.Ok(title));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail("Đã xảy ra lỗi khi tạo tiêu đề."));
            }
        }
        [HttpPost("validate")]
        public async Task<ActionResult<ApiResponse<object>>> ValidateMessage([FromBody] string message)
        {
            try
            {
                var result = await _chatService.ValidateMessageAsync(message);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi kiểm tra tin nhắn."));
            }
        }
        private string GetCurrentUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        }
    }
}

