using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IChatSessionService _chatSessionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatCompletionService chatCompletionService,
        IChatSessionService chatSessionService,
        IEmbeddingService embeddingService,
        ILogger<ChatController> logger)
    {
        _chatCompletionService = chatCompletionService;
        _chatSessionService = chatSessionService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    [HttpPost("completion")]
    public async Task<ActionResult<ChatCompletionResponse>> GetCompletionAsync([FromBody] ChatCompletionRequest request)
    {
        try
        {
            Guid sessionId;

            // Create or use existing session
            if (string.IsNullOrEmpty(request.SessionId))
            {
                // Extract user ID from claims or use a default
                var userId = User.Identity.IsAuthenticated
                    ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    : "anonymous";

                var session1 = await _chatSessionService.CreateSessionAsync(userId);
                sessionId = session1.Id;
            }
            else
            {
                if (!Guid.TryParse(request.SessionId, out sessionId))
                {
                    return BadRequest("Invalid session ID format");
                }
            }

            // Get or create session
            ChatSession session;
            try
            {
                session = await _chatSessionService.GetSessionAsync(sessionId);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Session {sessionId} not found");
            }

            // Generate embedding for the message
            var embedding = await _embeddingService.GetEmbeddingsAsync(request.Message);

            // Add user message to session
            await _chatSessionService.AddMessageAsync(sessionId, "user", request.Message, embedding);

            // Get history from session
            var messages = await _chatSessionService.GetSessionMessagesAsync(sessionId);
            var messageHistory = messages.Select(m => (m.Role, m.Content)).ToList();

            // Get completion from LLM
            var completionResult = await _chatCompletionService.GetCompletionAsync(
                sessionId.ToString(),
                messageHistory,
                request.Settings
            );

            // Add assistant response to session
            var responseEmbedding = await _embeddingService.GetEmbeddingsAsync(completionResult);
            await _chatSessionService.AddMessageAsync(sessionId, "assistant", completionResult, responseEmbedding);

            return Ok(new ChatCompletionResponse
            {
                SessionId = sessionId.ToString(),
                Message = completionResult,
                Role = "assistant",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat completion request");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<SessionsResponse>> GetSessionsAsync()
    {
        try
        {
            // Extract user ID from claims or use a default
            var userId = User.Identity.IsAuthenticated
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : "anonymous";

            var sessions = await _chatSessionService.GetUserSessionsAsync(userId);

            return Ok(new SessionsResponse
            {
                Sessions = sessions.Select(s => new SessionInfo
                {
                    Id = s.Id.ToString(),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    LastMessage = s.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user sessions");
            return StatusCode(500, "An error occurred while retrieving your sessions");
        }
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetSessionMessagesAsync(string sessionId)
    {
        try
        {
            if (!Guid.TryParse(sessionId, out var id))
            {
                return BadRequest("Invalid session ID format");
            }

            var messages = await _chatSessionService.GetSessionMessagesAsync(id);
            return Ok(messages);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Session {sessionId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session messages");
            return StatusCode(500, "An error occurred while retrieving session messages");
        }
    }
}