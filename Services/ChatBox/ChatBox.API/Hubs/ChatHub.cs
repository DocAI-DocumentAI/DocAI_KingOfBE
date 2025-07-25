using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatBox.API.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        // User joins a chat session
        public async Task JoinSession(string sessionId)
        {
            var userId = GetUserId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

            _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
        }

        // User leaves a chat session
        public async Task LeaveSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
            _logger.LogInformation("User left session {SessionId}", sessionId);
        }

        // Send message with real-time response
        public async Task SendMessage(string sessionId, string message)
        {
            try
            {
                var userId = GetUserId();
                var ipAddress = GetClientIpAddress();
                var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"];

                _logger.LogInformation("Received message from user {UserId} in session {SessionId}", userId, sessionId);

                // Send to ChatService for processing
                var request = new SendMessageRequest
                {
                    Message = message,
                    SessionId = Guid.Parse(sessionId)
                };

                var response = await _chatService.SendMessageAsync(userId, request, ipAddress, userAgent);

                if (response.Success)
                {
                    // Send response back to the session group
                    await Clients.Group($"Session_{sessionId}").SendAsync("ReceiveMessage", new
                    {
                        MessageId = response.MessageId,
                        SessionId = response.SessionId,
                        UserMessage = message,
                        BotResponse = response.Response,
                        Sources = response.Sources,
                        SuggestedQuestions = response.SuggestedQuestions,
                        Timestamp = response.Timestamp,
                        TokensUsed = response.TokensUsed
                    });
                }
                else
                {
                    // Send error back to user
                    await Clients.Caller.SendAsync("ReceiveError", new
                    {
                        Error = response.Message,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message in SignalR hub");
                await Clients.Caller.SendAsync("ReceiveError", new
                {
                    Error = "Có lỗi xảy ra khi xử lý tin nhắn",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // Start streaming response
        public async Task StartStreaming(string sessionId, string message)
        {
            try
            {
                var userId = GetUserId();
                var connectionId = Context.ConnectionId;

                var request = new StreamChatRequest
                {
                    Message = message,
                    SessionId = Guid.Parse(sessionId)
                };

                var streamingResponse = await _chatService.StartStreamingAsync(userId, request, connectionId);

                if (streamingResponse.Success)
                {
                    await Clients.Caller.SendAsync("StreamingStarted", new
                    {
                        StreamId = streamingResponse.StreamId,
                        SessionId = streamingResponse.SessionId,
                        Message = "Streaming started"
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("StreamingError", new
                    {
                        Error = streamingResponse.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting streaming");
                await Clients.Caller.SendAsync("StreamingError", new
                {
                    Error = "Không thể bắt đầu streaming"
                });
            }
        }

        // Connection events
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            _logger.LogInformation("User {UserId} connected to SignalR", userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = GetUserId();
            _logger.LogInformation("User {UserId} disconnected from SignalR", userId);
            await base.OnDisconnectedAsync(exception);
        }

        // Helper methods
        private Guid GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst("sub")?.Value ??
                             Context.User?.FindFirst("user_id")?.Value ??
                             Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
                return userId;

            throw new UnauthorizedAccessException("User ID not found in token");
        }

        private string GetClientIpAddress()
        {
            var httpContext = Context.GetHttpContext();
            return httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
