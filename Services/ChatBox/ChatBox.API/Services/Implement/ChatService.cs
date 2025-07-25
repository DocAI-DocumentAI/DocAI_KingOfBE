using AutoMapper;
using ChatBox.API.Hubs;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Enum;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Paginate;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security;
using System.Text;
using System.Text.Json;

namespace ChatBox.API.Services.Implement
{
    public class ChatService : IChatService
    {
        private readonly IConversationOrchestrationService _orchestrationService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IAuditService _auditService;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ISecurityService _securityService;
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<ChatService> _logger;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IMapper _mapper;

        public ChatService(
           IConversationOrchestrationService orchestrationService,
           IAnalyticsService analyticsService,
           IAuditService auditService,
           IRateLimitingService rateLimitingService,
           ISecurityService securityService,
           IUnitOfWork<ChatBoxDbContext> unitOfWork,
           ILogger<ChatService> logger,
           IHubContext<ChatHub> hubContext,
           IMapper mapper)
        {
            _orchestrationService = orchestrationService;
            _analyticsService = analyticsService;
            _auditService = auditService;
            _rateLimitingService = rateLimitingService;
            _securityService = securityService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _hubContext = hubContext;
            _mapper = mapper;
        }
        public async Task<SendMessageResponse> SendMessageAsync(Guid userId, SendMessageRequest request, string ipAddress, string userAgent)
        {
            try
            {
                _logger.LogInformation("Processing message for user {UserId}", userId);

                // 1. Rate limiting check
                var isWithinLimit = await _rateLimitingService.IsWithinLimitAsync(userId, "send_message");
                if (!isWithinLimit)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = "Rate limit exceeded. Please wait before sending another message.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // 2. Record request for rate limiting
                await _rateLimitingService.RecordRequestAsync(userId, "send_message");

                // 3. Security analysis
                var securityResult = await _securityService.AnalyzeContentAsync(request.Message, userId, ipAddress);
                if (securityResult.HasSecurityIssues)
                {
                    await _auditService.LogSecurityEventAsync(userId, "SecurityViolation",
                        $"Message blocked due to security issues: {string.Join(", ", securityResult.DetectedIssues)}",
                        "high", ipAddress);

                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = "Your message contains content that violates our security policy.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // 4. Process message through orchestration service
                var processRequest = _mapper.Map<ProcessMessageRequest>(request);
                processRequest.UserId = userId;
                processRequest.IpAddress = ipAddress;
                processRequest.UserAgent = userAgent;

                var orchestrationResult = await _orchestrationService.ProcessMessageAsync(processRequest);

                // 5. Map to response using AutoMapper
                var response = _mapper.Map<SendMessageResponse>(orchestrationResult);
                response.SuggestedQuestions = request.IncludeSuggestions ? orchestrationResult.SuggestedQuestions : new List<string>();

                // 6. Log audit trail
                await _auditService.LogAsync(userId, "SendMessage", "ChatMessage", orchestrationResult.MessageId.ToString(),
                    null, new { Request = request, Response = response }, ipAddress, userAgent);

                _logger.LogInformation("Message processed successfully for user {UserId}, MessageId: {MessageId}",
                    userId, orchestrationResult.MessageId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for user {UserId}", userId);

                await _auditService.LogSecurityEventAsync(userId, "SendMessageError",
                    $"Error processing message: {ex.Message}", "medium", ipAddress);

                return new SendMessageResponse
                {
                    Success = false,
                    Message = "An error occurred while processing your message. Please try again.",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        public async Task<StreamingResponse> StartStreamingAsync(Guid userId, StreamChatRequest request, string connectionId)
        {
            try
            {
                _logger.LogInformation("Starting streaming for user {UserId}, ConnectionId: {ConnectionId}", userId, connectionId);

                // 1. Rate limiting check
                var isWithinLimit = await _rateLimitingService.IsWithinLimitAsync(userId, "start_streaming");
                if (!isWithinLimit)
                {
                    return new StreamingResponse
                    {
                        Success = false,
                        Message = "Rate limit exceeded for streaming.",
                        StartedAt = DateTime.UtcNow
                    };
                }

                await _rateLimitingService.RecordRequestAsync(userId, "start_streaming");

                // 2. Create or get session
                var session = await GetOrCreateSessionForStreamingAsync(userId, request.SessionId);

                // 3. Generate stream ID
                var streamId = Guid.NewGuid();

                // 4. Start background streaming process
                _ = Task.Run(async () => await ProcessStreamingAsync(userId, request, connectionId, streamId, session.Id));

                // 5. Return immediate response
                return new StreamingResponse
                {
                    Success = true,
                    Message = "Streaming started successfully",
                    StreamId = streamId,
                    SessionId = session.Id,
                    ConnectionId = connectionId,
                    StartedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting streaming for user {UserId}", userId);
                return new StreamingResponse
                {
                    Success = false,
                    Message = "Failed to start streaming",
                    StartedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<bool> CancelStreamingAsync(Guid userId, Guid messageId)
        {
            try
            {
                _logger.LogInformation("Cancelling streaming for user {UserId}, MessageId: {MessageId}", userId, messageId);

                // Send cancellation signal through SignalR
                await _hubContext.Clients.User(userId.ToString()).SendAsync("StreamingCancelled", messageId);

                await _auditService.LogAsync(userId, "CancelStreaming", "ChatMessage", messageId.ToString());

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling streaming for user {UserId}, MessageId: {MessageId}", userId, messageId);
                return false;
            }
        }

        public async Task<AdvancedMessageResponse> GetMessageAsync(Guid userId, Guid messageId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var message = await messageRepo.SingleOrDefaultAsync(predicate:
                    m => m.Id == messageId && m.UserId == userId,
                    include: null);

                if (message == null)
                {
                    return null;
                }

                var feedback = await GetMessageFeedbackAsync(messageId);

                // Map using AutoMapper and add additional properties
                var response = _mapper.Map<AdvancedMessageResponse>(message);
                response.Sources = ParseDocumentReferences(message.SourceDocuments);
                response.Metadata = ParseMetadata(message.Metadata);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId} for user {UserId}", messageId, userId);
                return null;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid userId, Guid messageId, string reason = "user_request")
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var message = await messageRepo.SingleOrDefaultAsync(predicate:
                    m => m.Id == messageId && m.UserId == userId);

                if (message == null)
                {
                    return false;
                }

                // Soft delete - mark as deleted instead of removing
                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;
                message.DeletionReason = reason;

                messageRepo.UpdateAsync(message);
                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(userId, "DeleteMessage", "ChatMessage", messageId.ToString(),
                    message, null, null, null);

                _logger.LogInformation("Message {MessageId} deleted for user {UserId}, Reason: {Reason}",
                    messageId, userId, reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                return false;
            }
        }

        public async Task<bool> AddFeedbackAsync(Guid userId, FeedbackRequest request)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var feedbackRepo = _unitOfWork.GetRepository<MessageFeedback>();

                // Verify message belongs to user
                var message = await messageRepo.SingleOrDefaultAsync(predicate:
                    m => m.Id == request.MessageId && m.UserId == userId);

                if (message == null)
                {
                    return false;
                }

                // Check if feedback already exists
                var existingFeedback = await feedbackRepo.SingleOrDefaultAsync(predicate:
                    f => f.MessageId == request.MessageId && f.UserId == userId);

                if (existingFeedback != null)
                {
                    // Update existing feedback
                    existingFeedback.Rating = request.Rating;
                    existingFeedback.Comment = request.Comment;
                    existingFeedback.FeedbackType = request.FeedbackType;
                    existingFeedback.UpdatedAt = DateTime.UtcNow;
                    feedbackRepo.UpdateAsync(existingFeedback);
                }
                else
                {
                    // Create new feedback
                    var feedback = new MessageFeedback
                    {
                        Id = Guid.NewGuid(),
                        MessageId = request.MessageId,
                        UserId = userId,
                        Rating = request.Rating,
                        Comment = request.Comment,
                        FeedbackType = request.FeedbackType,
                        CreatedAt = DateTime.UtcNow
                    };

                    await feedbackRepo.InsertAsync(feedback);
                }

                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(userId, "AddFeedback", "MessageFeedback", request.MessageId.ToString(),
                    null, request);

                _logger.LogInformation("Feedback added for message {MessageId} by user {UserId}",
                    request.MessageId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding feedback for message {MessageId} by user {UserId}",
                    request.MessageId, userId);
                return false;
            }
        }
        public async Task<AdvancedSessionResponse> CreateSessionAsync(Guid userId, CreateSessionRequest request, string ipAddress, string userAgent)
        {
            try
            {
                _logger.LogInformation("Creating session for user {UserId}", userId);

                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

                // Map request to domain model using AutoMapper
                var session = _mapper.Map<ChatSession>(request);
                session.Id = Guid.NewGuid();
                session.UserId = userId;
                session.Title = string.IsNullOrWhiteSpace(request.Title) ? "New Conversation" : request.Title;

                await sessionRepo.InsertAsync(session);
                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(userId, "CreateSession", "ChatSession", session.Id.ToString(),
                    null, request, ipAddress, userAgent);

                var statistics = new SessionStatistics
                {
                    TotalMessages = 0,
                    TotalTokensUsed = 0,
                    AverageResponseTime = TimeSpan.Zero,
                    AverageRating = 0,
                    TopTopics = new List<string>()
                };

                // Map session to response using AutoMapper
                var response = _mapper.Map<AdvancedSessionResponse>(session);
                response.Statistics = statistics;
                response.Metadata = new Dictionary<string, object>
                {
                    { "SessionType", session.SessionType },
                    { "InitialContext", request.InitialContext }
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error create session for message ");
                return null;
            }
        }
        public async Task<AdvancedSessionResponse> GetSessionAsync(Guid userId, Guid sessionId)
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
                var session = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                {
                    return null;
                }

                var statistics = await CalculateSessionStatisticsAsync(sessionId);

                // Map session to response using AutoMapper
                var response = _mapper.Map<AdvancedSessionResponse>(session);
                response.Statistics = statistics;
                response.Metadata = new Dictionary<string, object>
                {
                    { "SessionType", session.SessionType ?? "general" },
                    { "InitialContext", ParseMetadata(session.InitialContext) }
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session {SessionId} for user {UserId}", sessionId, userId);
                return null;
            }
        }
        public async Task<IPaginate<SessionSummaryResponse>> GetSessionsAsync(Guid userId, GetSessionsRequest request)
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

                var predicate = BuildSessionFilterPredicate(userId, request);

                var sessions = await sessionRepo.GetPagingListAsync(
                    selector: s => new SessionSummaryResponse
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Status = s.Status,
                        MessageCount = s.MessageCount,
                        LastActivityAt = s.LastActivityAt,
                        LastMessage = GetLastMessagePreview(s.Id),
                        Duration = s.LastActivityAt - s.CreatedAt
                    },
                    filter: null,
                    predicate: predicate,
                    orderBy: GetSessionOrderBy(request.SortBy, request.IsAscending),
                    include: null,
                    page: request.Page,
                    size: request.Size);

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions for user {UserId}", userId);
                return new Paginate<SessionSummaryResponse>(new List<SessionSummaryResponse>(), 0, request.Page, request.Size);
            }
        }
        public async Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, string reason = "user_request")
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();

                var session = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                {
                    return false;
                }

                // Soft delete session
                session.IsDeleted = true;
                session.DeletedAt = DateTime.UtcNow;
                session.DeletionReason = reason;
                session.Status = SessionStatus.Archived;

                // Soft delete all messages in session
                var messages = await messageRepo.GetListAsync(predicate:
                    m => m.SessionId == sessionId && m.UserId == userId);

                foreach (var message in messages)
                {
                    message.IsDeleted = true;
                    message.DeletedAt = DateTime.UtcNow;
                    message.DeletionReason = reason;
                }

                sessionRepo.UpdateAsync(session);
                messageRepo.UpdateRange(messages);
                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(userId, "DeleteSession", "ChatSession", sessionId.ToString(),
                    session, null, null, null);

                _logger.LogInformation("Session {SessionId} deleted for user {UserId}, Reason: {Reason}",
                    sessionId, userId, reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId} for user {UserId}", sessionId, userId);
                return false;
            }
        }

        public async Task<ConversationSummaryResponse> GenerateSummaryAsync(Guid userId, Guid sessionId)
        {
            try
            {
                _logger.LogInformation("Generating summary for session {SessionId}, user {UserId}", sessionId, userId);

                // Verify session ownership
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
                var session = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                {
                    return null;
                }

                // Get all messages in session
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var messages = await messageRepo.GetListAsync(predicate:
                    m => m.SessionId == sessionId && !m.IsDeleted,
                    orderBy: m => m.OrderBy(x => x.CreatedAt));

                if (messages.Count == 0)
                {
                    return new ConversationSummaryResponse
                    {
                        SessionId = sessionId,
                        Summary = "No messages found in this conversation.",
                        KeyTopics = new List<string>(),
                        ActionItems = new List<string>(),
                        MessageCount = 0,
                        TotalDuration = TimeSpan.Zero,
                        GeneratedAt = DateTime.UtcNow
                    };
                }

                // Build conversation text for summarization
                var conversationText = BuildConversationTextForSummary(messages);

                // Generate summary using AI service (through orchestration)
                var summary = await GenerateAISummaryAsync(conversationText);

                // Extract key topics and action items
                var keyTopics = ExtractKeyTopicsFromMessages(messages);
                var actionItems = ExtractActionItemsFromMessages(messages);

                // Calculate duration
                var duration = messages.Last().CreatedAt - messages.First().CreatedAt;

                return new ConversationSummaryResponse
                {
                    SessionId = sessionId,
                    Summary = summary,
                    KeyTopics = keyTopics,
                    ActionItems = actionItems,
                    MessageCount = messages.Count,
                    TotalDuration = duration,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary for session {SessionId}", sessionId);
                return null;
            }
        }
        public async Task<IPaginate<SearchResult>> SearchConversationsAsync(Guid userId, SearchRequest request)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();

                var predicate = BuildSearchPredicate(userId, request);

                var searchResults = await messageRepo.GetPagingListAsync(
                    selector: m => new SearchResult
                    {
                        MessageId = m.Id,
                        SessionId = m.SessionId,
                        Content = m.Content,
                        Response = m.AiResponse,
                        CreatedAt = m.CreatedAt,
                        RelevanceScore = CalculateRelevanceScore(m.Content, m.AiResponse, request.Query),
                        MatchContext = GenerateMatchContext(m.Content, m.AiResponse, request.Query)
                    },
                    filter: null,
                    predicate: predicate,
                    orderBy: m => m.OrderByDescending(x => x.CreatedAt),
                    include: null,
                    page: request.Page,
                    size: request.Size);

                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching conversations for user {UserId}", userId);
                return new Paginate<SearchResult>(new List<SearchResult>(), 0, request.Page, request.Size);
            }
        }

        public async Task<List<AlertResponse>> GetUserAlertsAsync(Guid userId)
        {
            try
            {
                var alertRepo = _unitOfWork.GetRepository<UserAlert>();
                var alerts = await alertRepo.GetListAsync(predicate:
                    a => a.UserId == userId && !a.IsRead,
                    orderBy: a => a.OrderByDescending(x => x.CreatedAt));

                return alerts.Select(a => new AlertResponse
                {
                    Id = a.Id,
                    Type = a.Type,
                    Title = a.Title,
                    Message = a.Message,
                    Severity = a.Severity,
                    CreatedAt = a.CreatedAt,
                    IsRead = a.IsRead,
                    Data = ParseMetadata(a.Data)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts for user {UserId}", userId);
                return new List<AlertResponse>();
            }
        }

        // Private helper methods
        private async Task<ChatSession> GetOrCreateSessionForStreamingAsync(Guid userId, Guid? sessionId)
        {
            var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

            if (sessionId.HasValue)
            {
                var existingSession = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId.Value && s.UserId == userId && s.Status == SessionStatus.Active);

                if (existingSession != null)
                {
                    return existingSession;
                }
            }

            // Create new session for streaming
            var newSession = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Streaming Conversation",
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                MessageCount = 0,
                SessionType = "streaming"
            };

            await sessionRepo.InsertAsync(newSession);
            await _unitOfWork.CommitAsync();

            return newSession;
        }

        private async Task ProcessStreamingAsync(Guid userId, StreamChatRequest request, string connectionId, Guid streamId, Guid sessionId)
        {
            try
            {
                _logger.LogInformation("Processing streaming for user {UserId}, StreamId: {StreamId}", userId, streamId);

                // Send streaming start notification
                await _hubContext.Clients.Client(connectionId).SendAsync("StreamingStarted", streamId);

                // Process through orchestration service
                var processRequest = new ProcessMessageRequest
                {
                    UserId = userId,
                    Message = request.Message,
                    SessionId = sessionId,
                    Context = request.Context
                };

                var result = await _orchestrationService.ProcessMessageAsync(processRequest);

                if (result.Success)
                {
                    // Send streaming chunks (simulate streaming response)
                    var responseChunks = SplitResponseIntoChunks(result.Response);

                    foreach (var chunk in responseChunks)
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("StreamingChunk", new
                        {
                            StreamId = streamId,
                            Chunk = chunk,
                            IsComplete = false
                        });

                        await Task.Delay(100); // Simulate streaming delay
                    }

                    // Send completion
                    await _hubContext.Clients.Client(connectionId).SendAsync("StreamingComplete", new
                    {
                        StreamId = streamId,
                        MessageId = result.MessageId,
                        SessionId = result.SessionId,
                        Sources = result.DocumentReferences,
                        SuggestedQuestions = result.SuggestedQuestions,
                        TokensUsed = result.TokensUsed
                    });
                }
                else
                {
                    // Send error
                    await _hubContext.Clients.Client(connectionId).SendAsync("StreamingError", new
                    {
                        StreamId = streamId,
                        Error = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming process for user {UserId}, StreamId: {StreamId}", userId, streamId);

                await _hubContext.Clients.Client(connectionId).SendAsync("StreamingError", new
                {
                    StreamId = streamId,
                    Error = "An error occurred during streaming"
                });
            }
        }

        private async Task<FeedbackInfo> GetMessageFeedbackAsync(Guid messageId)
        {
            try
            {
                var feedbackRepo = _unitOfWork.GetRepository<MessageFeedback>();
                var feedback = await feedbackRepo.SingleOrDefaultAsync(predicate: f => f.MessageId == messageId);

                if (feedback == null)
                {
                    return null;
                }

                return new FeedbackInfo
                {
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    FeedbackDate = feedback.CreatedAt,
                    FeedbackType = feedback.FeedbackType
                };
            }
            catch
            {
                return null;
            }
        }

        private List<DocumentReference> ParseDocumentReferences(string sourceDocuments)
        {
            if (string.IsNullOrEmpty(sourceDocuments))
            {
                return new List<DocumentReference>();
            }

            try
            {
                var documentIds = sourceDocuments.Split(',');
                return documentIds.Select(id => new DocumentReference
                {
                    DocumentId = id.Trim(),
                    Title = $"Document {id.Trim()}",
                    Url = $"/documents/{id.Trim()}"
                }).ToList();
            }
            catch
            {
                return new List<DocumentReference>();
            }
        }

        private Dictionary<string, object> ParseMetadata(string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(metadata) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private async Task<SessionStatistics> CalculateSessionStatisticsAsync(Guid sessionId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var feedbackRepo = _unitOfWork.GetRepository<MessageFeedback>();

                var messages = await messageRepo.GetListAsync(predicate: m => m.SessionId == sessionId && !m.IsDeleted);
                var feedbacks = await feedbackRepo.GetListAsync(predicate:  f => messages.Select(m => m.Id).Contains(f.MessageId));

                var totalTokens = messages.Sum(m => m.TokensUsed);
                var averageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;

                // Extract top topics (simplified)
                var topTopics = ExtractTopTopicsFromMessages(messages);

                return new SessionStatistics
                {
                    TotalMessages = messages.Count,
                    TotalTokensUsed = totalTokens,
                    AverageResponseTime = TimeSpan.FromSeconds(2), // Placeholder - would need to track actual response times
                    AverageRating = averageRating,
                    TopTopics = topTopics
                };
            }
            catch
            {
                return new SessionStatistics
                {
                    TotalMessages = 0,
                    TotalTokensUsed = 0,
                    AverageResponseTime = TimeSpan.Zero,
                    AverageRating = 0,
                    TopTopics = new List<string>()
                };
            }
        }

        private List<string> ExtractTopTopicsFromMessages(ICollection<ChatMessage> messages)
        {
            // Simplified topic extraction - in real implementation, use NLP
            var commonWords = new[] { "policy", "procedure", "document", "process", "requirement", "guideline" };
            var topics = new List<string>();

            foreach (var word in commonWords)
            {
                if (messages.Any(m => m.Content.ToLower().Contains(word) || m.AiResponse?.ToLower().Contains(word) == true))
                {
                    topics.Add(word);
                }
            }

            return topics.Take(5).ToList();
        }

        private System.Linq.Expressions.Expression<Func<ChatSession, bool>> BuildSessionFilterPredicate(Guid userId, GetSessionsRequest request)
        {
            var predicate = System.Linq.Expressions.Expression.Parameter(typeof(ChatSession), "s");
            var condition = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(predicate, nameof(ChatSession.UserId)),
                System.Linq.Expressions.Expression.Constant(userId));

            // Add IsDeleted check
            var isDeletedCheck = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(predicate, nameof(ChatSession.IsDeleted)),
                System.Linq.Expressions.Expression.Constant(false));

            condition = System.Linq.Expressions.Expression.AndAlso(condition, isDeletedCheck);

            // Add date filters if specified
            if (request.FromDate.HasValue)
            {
                var fromDateCheck = System.Linq.Expressions.Expression.GreaterThanOrEqual(
                    System.Linq.Expressions.Expression.Property(predicate, nameof(ChatSession.CreatedAt)),
                    System.Linq.Expressions.Expression.Constant(request.FromDate.Value));
                condition = System.Linq.Expressions.Expression.AndAlso(condition, fromDateCheck);
            }

            if (request.ToDate.HasValue)
            {
                var toDateCheck = System.Linq.Expressions.Expression.LessThanOrEqual(
                    System.Linq.Expressions.Expression.Property(predicate, nameof(ChatSession.CreatedAt)),
                    System.Linq.Expressions.Expression.Constant(request.ToDate.Value));
                condition = System.Linq.Expressions.Expression.AndAlso(condition, toDateCheck);
            }

            // Add status filter if specified
            if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<SessionStatus>(request.Status, out var status))
            {
                var statusCheck = System.Linq.Expressions.Expression.Equal(
                    System.Linq.Expressions.Expression.Property(predicate, nameof(ChatSession.Status)),
                    System.Linq.Expressions.Expression.Constant(status));
                condition = System.Linq.Expressions.Expression.AndAlso(condition, statusCheck);
            }

            return System.Linq.Expressions.Expression.Lambda<Func<ChatSession, bool>>(condition, predicate);
        }

        private Func<IQueryable<ChatSession>, IOrderedQueryable<ChatSession>> GetSessionOrderBy(string sortBy, bool isAscending)
        {
            return sortBy?.ToLower() switch
            {
                "title" => q => isAscending ? q.OrderBy(s => s.Title) : q.OrderByDescending(s => s.Title),
                "createdat" => q => isAscending ? q.OrderBy(s => s.CreatedAt) : q.OrderByDescending(s => s.CreatedAt),
                "messagecount" => q => isAscending ? q.OrderBy(s => s.MessageCount) : q.OrderByDescending(s => s.MessageCount),
                _ => q => isAscending ? q.OrderBy(s => s.LastActivityAt) : q.OrderByDescending(s => s.LastActivityAt)
            };
        }

        private string GetLastMessagePreview(Guid sessionId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var lastMessage = messageRepo.SingleOrDefaultAsync(predicate:
                    m => m.SessionId == sessionId && !m.IsDeleted,
                    orderBy: m => m.OrderByDescending(x => x.CreatedAt)).Result;

                if (lastMessage == null)
                {
                    return "No messages";
                }

                var preview = lastMessage.Content.Length > 100 ?
                    lastMessage.Content.Substring(0, 100) + "..." :
                    lastMessage.Content;

                return preview;
            }
            catch
            {
                return "No messages";
            }
        }

        private string BuildConversationTextForSummary(ICollection<ChatMessage> messages)
        {
            var conversationBuilder = new StringBuilder();

            foreach (var message in messages)
            {
                conversationBuilder.AppendLine($"User: {message.Content}");
                if (!string.IsNullOrEmpty(message.AiResponse))
                {
                    conversationBuilder.AppendLine($"Assistant: {message.AiResponse}");
                }
                conversationBuilder.AppendLine();
            }

            return conversationBuilder.ToString();
        }

        private async Task<string> GenerateAISummaryAsync(string conversationText)
        {
            try
            {
                // This would typically call the AI service for summarization
                // For now, return a basic summary
                var lines = conversationText.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                var messageCount = lines.Count(l => l.StartsWith("User:"));

                return $"This conversation contained {messageCount} user messages covering various topics. " +
                       "The user asked questions about company policies and procedures, and received detailed responses.";
            }
            catch
            {
                return "Unable to generate summary at this time.";
            }
        }

        private List<string> ExtractKeyTopicsFromMessages(ICollection<ChatMessage> messages)
        {
            var topics = new HashSet<string>();
            var keywordTopicMap = new Dictionary<string, string>
            {
                { "policy", "Company Policies" },
                { "procedure", "Procedures" },
                { "hr", "Human Resources" },
                { "it", "IT Support" },
                { "finance", "Finance" },
                { "legal", "Legal" },
                { "training", "Training" },
                { "benefits", "Benefits" }
            };

            foreach (var message in messages)
            {
                var content = (message.Content + " " + message.AiResponse).ToLower();
                foreach (var kvp in keywordTopicMap)
                {
                    if (content.Contains(kvp.Key))
                    {
                        topics.Add(kvp.Value);
                    }
                }
            }

            return topics.Take(5).ToList();
        }

        private List<string> ExtractActionItemsFromMessages(ICollection<ChatMessage> messages)
        {
            var actionItems = new List<string>();
            var actionKeywords = new[] { "need to", "should", "must", "required", "contact", "submit", "apply" };

            foreach (var message in messages)
            {
                var responses = new[] { message.Content, message.AiResponse }.Where(r => !string.IsNullOrEmpty(r));

                foreach (var response in responses)
                {
                    var sentences = response.Split('.', '!', '?');
                    foreach (var sentence in sentences)
                    {
                        if (actionKeywords.Any(keyword => sentence.ToLower().Contains(keyword)))
                        {
                            var actionItem = sentence.Trim();
                            if (actionItem.Length > 10 && actionItem.Length < 200)
                            {
                                actionItems.Add(actionItem);
                            }
                        }
                    }
                }
            }

            return actionItems.Distinct().Take(5).ToList();
        }

        private System.Linq.Expressions.Expression<Func<ChatMessage, bool>> BuildSearchPredicate(Guid userId, SearchRequest request)
        {
            var predicate = System.Linq.Expressions.Expression.Parameter(typeof(ChatMessage), "m");

            // Base conditions: user ownership and not deleted
            var userCondition = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.UserId)),
                System.Linq.Expressions.Expression.Constant(userId));

            var notDeletedCondition = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.IsDeleted)),
                System.Linq.Expressions.Expression.Constant(false));

            var condition = System.Linq.Expressions.Expression.AndAlso(userCondition, notDeletedCondition);

            // Search in content or AI response
            var contentProperty = System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.Content));
            var responseProperty = System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.AiResponse));
            var queryConstant = System.Linq.Expressions.Expression.Constant(request.Query.ToLower());

            var contentContains = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Call(contentProperty, typeof(string).GetMethod("ToLower", Type.EmptyTypes)),
                typeof(string).GetMethod("Contains", new[] { typeof(string) }),
                queryConstant);

            var responseContains = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Call(responseProperty, typeof(string).GetMethod("ToLower", Type.EmptyTypes)),
                typeof(string).GetMethod("Contains", new[] { typeof(string) }),
                queryConstant);

            var searchCondition = System.Linq.Expressions.Expression.OrElse(contentContains, responseContains);
            condition = System.Linq.Expressions.Expression.AndAlso(condition, searchCondition);

            // Add date filters
            if (request.FromDate.HasValue)
            {
                var fromDateCheck = System.Linq.Expressions.Expression.GreaterThanOrEqual(
                    System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.CreatedAt)),
                    System.Linq.Expressions.Expression.Constant(request.FromDate.Value));
                condition = System.Linq.Expressions.Expression.AndAlso(condition, fromDateCheck);
            }

            if (request.ToDate.HasValue)
            {
                var toDateCheck = System.Linq.Expressions.Expression.LessThanOrEqual(
                    System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.CreatedAt)),
                    System.Linq.Expressions.Expression.Constant(request.ToDate.Value));
                condition = System.Linq.Expressions.Expression.AndAlso(condition, toDateCheck);
            }

            // Add session filter
            if (request.SessionIds.Any())
            {
                var sessionProperty = System.Linq.Expressions.Expression.Property(predicate, nameof(ChatMessage.SessionId));
                var sessionIds = System.Linq.Expressions.Expression.Constant(request.SessionIds);
                var sessionContains = System.Linq.Expressions.Expression.Call(
                    sessionIds,
                    typeof(List<Guid>).GetMethod("Contains"),
                    sessionProperty);
                condition = System.Linq.Expressions.Expression.AndAlso(condition, sessionContains);
            }

            return System.Linq.Expressions.Expression.Lambda<Func<ChatMessage, bool>>(condition, predicate);
        }

        private double CalculateRelevanceScore(string content, string response, string query)
        {
            // Simple relevance scoring - in production, use more sophisticated scoring
            var queryLower = query.ToLower();
            var contentLower = (content + " " + response).ToLower();

            var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matchCount = queryWords.Count(word => contentLower.Contains(word));

            return (double)matchCount / queryWords.Length;
        }

        private string GenerateMatchContext(string content, string response, string query)
        {
            // Generate context showing where the match occurred
            var queryLower = query.ToLower();
            var fullText = content + " " + response;

            var index = fullText.ToLower().IndexOf(queryLower);
            if (index >= 0)
            {
                var start = Math.Max(0, index - 50);
                var length = Math.Min(100, fullText.Length - start);
                var context = fullText.Substring(start, length);

                if (start > 0) context = "..." + context;
                if (start + length < fullText.Length) context = context + "...";

                return context;
            }

            return content.Length > 100 ? content.Substring(0, 100) + "..." : content;
        }

        private List<string> SplitResponseIntoChunks(string response)
        {
            const int chunkSize = 50;
            var chunks = new List<string>();

            for (int i = 0; i < response.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, response.Length - i);
                chunks.Add(response.Substring(i, length));
            }

            return chunks;
        }
    }
}