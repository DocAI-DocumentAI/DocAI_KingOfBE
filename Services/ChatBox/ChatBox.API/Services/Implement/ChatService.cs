using AutoMapper;
using ChatBox.API.Hubs;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
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
        private readonly IAiServiceClient _aiServiceClient;
        private readonly IConversationOrchestrationService _orchestrationService;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ISecurityService _securityService;
        private readonly IContentModerationService _contentModerationService;
        private readonly ITokenValidationService _tokenValidationService;
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<ChatService> _logger;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public ChatService(
            IAiServiceClient aiServiceClient,
            IConversationOrchestrationService orchestrationService,
            IRateLimitingService rateLimitingService,
            ISecurityService securityService,
            IContentModerationService contentModerationService,
            ITokenValidationService tokenValidationService,
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            ILogger<ChatService> logger,
            IHubContext<ChatHub> hubContext,
            IMapper mapper,
            IConfiguration configuration)
        {
            _aiServiceClient = aiServiceClient;
            _orchestrationService = orchestrationService;
            _rateLimitingService = rateLimitingService;
            _securityService = securityService;
            _contentModerationService = contentModerationService;
            _tokenValidationService = tokenValidationService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _hubContext = hubContext;
            _mapper = mapper;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<SendMessageResponse> SendMessageAsync(Guid userId, SendMessageRequest request, string ipAddress, string userAgent)
        {
            try
            {
                _logger.LogInformation("Processing message for user {UserId}", userId);


                // 1. Kiểm tra rate limiting
                var isWithinLimit = await _rateLimitingService.IsWithinLimitAsync(userId, "send_message");
                if (!isWithinLimit)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:RateLimitExceeded"] ?? "Bạn đã gửi quá nhiều tin nhắn. Vui lòng chờ một chút trước khi gửi tiếp.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // 2. Kiểm tra độ dài và token limit của tin nhắn
                var maxMessageLength = _configuration.GetValue<int>("ChatService:MaxMessageLength", 5000);
                if (request.Message.Length > maxMessageLength)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:MessageTooLong"] ?? $"Tin nhắn quá dài. Vui lòng nhập ít hơn {maxMessageLength} ký tự.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // 3. Kiểm tra token limit
                var maxTokens = _configuration.GetValue<int>("ChatService:MaxInputTokens", 3000);
                var isWithinTokenLimit = await _tokenValidationService.IsWithinTokenLimitAsync(request.Message, maxTokens);
                if (!isWithinTokenLimit)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:TokenLimitExceeded"] ?? "Nội dung quá dài để xử lý. Vui lòng chia nhỏ câu hỏi của bạn.",
                        Timestamp = DateTime.UtcNow
                    };
                }
                //var maxInputTokens = _configuration.GetValue<int>("ChatService:MaxInputTokens", 3000);
                //var tokenCount = await _aiServiceClient.CountTokensAsync(request.Message, request.AIModelId);
                //if (tokenCount > maxInputTokens)
                //{
                //    return new SendMessageResponse
                //    {
                //        Success = false,
                //        Message = _configuration["ChatService:Messages:TokenLimitExceeded"] ?? "Nội dung quá dài để xử lý. Vui lòng chia nhỏ câu hỏi của bạn.",
                //        Timestamp = DateTime.UtcNow,
                //        Metadata = new Dictionary<string, object>
                //{
                //    { "TokenCount", tokenCount },
                //    { "MaxTokens", maxInputTokens }
                //}
                //    };
                //}
                // 4. Kiểm tra nội dung không phù hợp
                var moderationResult = await _contentModerationService.ModerateContentAsync(request.Message, userId);
                if (!moderationResult.IsApproved)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:ContentViolation"] ?? "Nội dung của bạn vi phạm chính sách. Vui lòng nhập câu hỏi khác.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                var securityResult = await _securityService.AnalyzeContentAsync(request.Message, userId, ipAddress);
                if (securityResult.HasSecurityIssues && securityResult.RiskScore > _configuration.GetValue<double>("ChatService:SecurityRiskThreshold", 0.7))
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:SecurityIssue"] ?? "Nội dung có vấn đề bảo mật. Vui lòng thử lại.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // 6. Ghi nhận request để rate limiting
                await _rateLimitingService.RecordRequestAsync(userId, "send_message");


                // 7. Lấy hoặc tạo session
                var session = await GetOrCreateSessionAsync(userId, request.SessionId, request.AIModelId, request.Temperature, request.MaxTokens);


               // 8.Xử lý qua orchestration service
                var processRequest = new ProcessMessageRequest
                {
                    UserId = userId,
                    Message = request.Message,
                    SessionId = session.Id,
                    Context = request.Context,
                    AIModelId = request.AIModelId ?? session.AIModelId,
                    Temperature = request.Temperature ?? session.Temperature,
                    MaxTokens = request.MaxTokens ?? session.MaxTokens,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                var orchestrationResult = await _orchestrationService.ProcessMessageAsync(processRequest);

                if (!orchestrationResult.Success)
                {
                    return new SendMessageResponse
                    {
                        Success = false,
                        Message = orchestrationResult.Message ?? _configuration["ChatService:Messages:ProcessingError"] ?? "Có lỗi xảy ra khi xử lý tin nhắn. Vui lòng thử lại.",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Map to response
                var response = _mapper.Map<SendMessageResponse>(orchestrationResult);
                response.SuggestedQuestions = request.IncludeSuggestions ? orchestrationResult.SuggestedQuestions : new List<string>();

                _logger.LogInformation("Message processed successfully for user {UserId}, MessageId: {MessageId}",
                    userId, orchestrationResult.MessageId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for user {UserId}", userId);

                return new SendMessageResponse
                {
                    Success = false,
                    Message = _configuration["ChatService:ErrorMessages:ProcessingError"] ?? "An error occurred while processing your message.",
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
                        Message = _configuration["ChatService:Messages:StreamingRateLimit"] ?? "Quá nhiều yêu cầu streaming. Vui lòng chờ.",
                        StartedAt = DateTime.UtcNow
                    };
                }
                var moderationResult = await _contentModerationService.ModerateContentAsync(request.Message, userId);
                if (!moderationResult.IsApproved)
                {
                    return new StreamingResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:ContentViolation"] ?? "Nội dung vi phạm chính sách.",
                        StartedAt = DateTime.UtcNow
                    };
                }

                var maxTokens = _configuration.GetValue<int>("ChatService:MaxInputTokens", 3000);
                var isWithinTokenLimit = await _tokenValidationService.IsWithinTokenLimitAsync(request.Message, maxTokens);
                if (!isWithinTokenLimit)
                {
                    return new StreamingResponse
                    {
                        Success = false,
                        Message = _configuration["ChatService:Messages:TokenLimitExceeded"] ?? "Nội dung quá dài. Vui lòng rút ngắn.",
                        StartedAt = DateTime.UtcNow
                    };
                }

                await _rateLimitingService.RecordRequestAsync(userId, "start_streaming");

                // 2. Create or get session
                var session = await GetOrCreateSessionAsync(userId, request.SessionId, request.AIModelId, request.Temperature, request.MaxTokens);

                // 3. Generate stream ID
                var streamId = Guid.NewGuid();

                // 4. Start background streaming process
                _ = Task.Run(async () => await ProcessEnhancedStreamingAsync(userId, request, connectionId, streamId, session.Id));

                // 5. Return immediate response
                return new StreamingResponse
                {
                    Success = true,
                    Message = _configuration["ChatService:Messages:StreamingStarted"] ?? "Bắt đầu streaming thành công",
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
                    Message = _configuration["ChatService:Messages:StreamingError"] ?? "Không thể bắt đầu streaming",
                    StartedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<bool> CancelStreamingAsync(Guid userId, Guid messageId)
        {
            try
            {
                _logger.LogInformation("Cancelling streaming for user {UserId}, MessageId: {MessageId}", userId, messageId);

                await _hubContext.Clients.User(userId.ToString()).SendAsync("StreamingCancelled", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling streaming for user {UserId}, MessageId: {MessageId}", userId, messageId);
                return false;
            }
        }

        public async Task<MessageResponse> GetMessageAsync(Guid userId, Guid messageId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var message = await messageRepo.SingleOrDefaultAsync(predicate:
                    m => m.Id == messageId && m.UserId == userId && !m.IsDeleted);

                if (message == null)
                {
                    return null;
                }

                var response = _mapper.Map<MessageResponse>(message);
                response.Sources = ParseDocumentReferences(message.SourceDocuments);
                response.Metadata = ParseMetadata(message.Metadata);
                response.Feedback = await GetMessageFeedbackAsync(messageId);

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

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                return false;
            }
        }


        public async Task<SessionResponse> CreateSessionAsync(Guid userId, CreateSessionRequest request, string ipAddress, string userAgent)
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

                var session = _mapper.Map<ChatSession>(request);
                session.Id = Guid.NewGuid();
                session.UserId = userId;

                if (!string.IsNullOrWhiteSpace(request.InitialContext))
                {
                    session.Title = await GenerateSessionTitleAsync(request.InitialContext);
                }
                else
                {
                    session.Title = string.IsNullOrWhiteSpace(request.Title) ?
                        _configuration["ChatService:DefaultSessionTitle"] ?? "Cuộc trò chuyện mới" : request.Title;
                }

                session.Status = SessionStatus.Active;
                session.CreatedAt = DateTime.UtcNow;
                session.LastActivityAt = DateTime.UtcNow;
                session.MessageCount = 0;
                session.AIModelId = request.AIModelId ?? _configuration["ChatService:DefaultAIModel"];
                session.Temperature = request.Temperature;
                session.MaxTokens = request.MaxTokens;

                await sessionRepo.InsertAsync(session);
                await _unitOfWork.CommitAsync();

                return _mapper.Map<SessionResponse>(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session for user {UserId}", userId);
                return null;
            }
        }
        private async Task<string> GenerateSessionTitleAsync(string initialContext)
        {
            try
            {
                var titleRequest = new TitleSuggestionRequest
                {
                    Content = initialContext,
                    MaxLength = _configuration.GetValue<int>("ChatService:MaxSessionTitleLength", 50),
                    Language = _configuration["ChatService:DefaultPreferences:Language"] ?? "vi",
                    Style = "concise"
                };

                var generatedTitle = await _aiServiceClient.SuggestTitleAsync(titleRequest);
                return string.IsNullOrEmpty(generatedTitle) ?
                    _configuration["ChatService:DefaultSessionTitle"] ?? "Cuộc trò chuyện mới" :
                    generatedTitle;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating session title, using default");
                return _configuration["ChatService:DefaultSessionTitle"] ?? "Cuộc trò chuyện mới";
            }
        }

        public async Task<SessionResponse> GetSessionAsync(Guid userId, Guid sessionId)
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
                var session = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId && s.UserId == userId && !s.IsDeleted);

                return session != null ? _mapper.Map<SessionResponse>(session) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session {SessionId} for user {UserId}", sessionId, userId);
                return null;
            }
        }
        public async Task<IPaginate<SessionResponse>> GetSessionsAsync(Guid userId, GetSessionsRequest request)
        {
            try
            {
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

                var predicate = BuildSessionFilterPredicate(userId, request);
                var orderBy = GetSessionOrderBy(request.SortBy, request.IsAscending);

                var sessions = await sessionRepo.GetPagingListAsync(
                    selector: s => _mapper.Map<SessionResponse>(s),
                    filter: null,
                    predicate: predicate,
                    orderBy: orderBy,
                    include: null,
                    page: request.Page,
                    size: request.Size);

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions for user {UserId}", userId);
                return new Paginate<SessionResponse>(new List<SessionResponse>(), 0, request.Page, request.Size);
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

                session.IsDeleted = true;
                session.DeletedAt = DateTime.UtcNow;
                session.DeletionReason = reason;
                session.Status = SessionStatus.Archived;

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

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId} for user {UserId}", sessionId, userId);
                return false;
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
        public async Task<bool> SubmitMessageFeedbackAsync(Guid userId, Guid messageId, MessageFeedbackRequest request)
        {
            try
            {
                var feedbackRepo = _unitOfWork.GetRepository<MessageFeedback>();

                var existingFeedback = await feedbackRepo.SingleOrDefaultAsync(predicate:
                    f => f.MessageId == messageId && f.UserId == userId);

                if (existingFeedback != null)
                {
                    existingFeedback.Rating = request.Rating;
                    existingFeedback.Comment = request.Comment;
                    existingFeedback.FeedbackType = request.FeedbackType;
                    existingFeedback.CreatedAt = DateTime.UtcNow;

                    feedbackRepo.UpdateAsync(existingFeedback);
                }
                else
                {
                    var feedback = _mapper.Map<MessageFeedback>(request);
                    feedback.Id = Guid.NewGuid();
                    feedback.MessageId = messageId;
                    feedback.UserId = userId;
                    feedback.CreatedAt = DateTime.UtcNow;

                    await feedbackRepo.InsertAsync(feedback);
                }

                await _unitOfWork.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback for message {MessageId}", messageId);
                return false;
            }
        }
        // Private helper methods
        private async Task<ChatSession> GetOrCreateSessionAsync(Guid userId, Guid? sessionId, string aiModelId, double? temperature, int? maxTokens)
        {
            var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

            if (sessionId.HasValue)
            {
                var existingSession = await sessionRepo.SingleOrDefaultAsync(predicate:
                    s => s.Id == sessionId.Value && s.UserId == userId && s.Status == SessionStatus.Active);

                if (existingSession != null)
                {
                    existingSession.LastActivityAt = DateTime.UtcNow;
                    sessionRepo.UpdateAsync(existingSession);
                    await _unitOfWork.CommitAsync();
                    return existingSession;
                }
            }

            var newSession = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = _configuration["ChatService:DefaultSessionTitle"] ?? "Cuộc trò chuyện mới",
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                MessageCount = 0,
                AIModelId = aiModelId ?? _configuration["ChatService:DefaultAIModel"] ?? "gpt-3.5-turbo",
                Temperature = temperature ?? _configuration.GetValue<double>("ChatService:DefaultTemperature", 0.7),
                MaxTokens = maxTokens ?? _configuration.GetValue<int>("ChatService:DefaultMaxTokens", 2000)
            };

            await sessionRepo.InsertAsync(newSession);
            await _unitOfWork.CommitAsync();

            return newSession;
        }

        private async Task ProcessEnhancedStreamingAsync(Guid userId, StreamChatRequest request, string connectionId, Guid streamId, Guid sessionId)
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("StreamingStarted", streamId);

                var processRequest = new ProcessMessageRequest
                {
                    UserId = userId,
                    Message = request.Message,
                    SessionId = sessionId,
                    Context = request.Context,
                    AIModelId = request.AIModelId,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens
                };

                var result = await _orchestrationService.ProcessMessageAsync(processRequest);

                if (result.Success)
                {
                    // Enhanced streaming với metadata
                    var responseChunks = SplitResponseIntoChunks(result.Response);

                    foreach (var (chunk, index) in responseChunks.Select((chunk, index) => (chunk, index)))
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("StreamingChunk", new
                        {
                            StreamId = streamId,
                            Chunk = chunk,
                            ChunkIndex = index,
                            IsComplete = false,
                            Metadata = new
                            {
                                TokensUsed = result.TokensUsed,
                                AIModelUsed = result.AIModelUsed,
                                ProcessingTime = result.ProcessingTime.TotalMilliseconds
                            }
                        });

                        await Task.Delay(_configuration.GetValue<int>("ChatService:StreamingDelayMs", 100));
                    }

                    // Enhanced completion với full metadata
                    await _hubContext.Clients.Client(connectionId).SendAsync("StreamingComplete", new
                    {
                        StreamId = streamId,
                        MessageId = result.MessageId,
                        SessionId = result.SessionId,
                        Sources = result.DocumentReferences,
                        SuggestedQuestions = result.SuggestedQuestions,
                        TokensUsed = result.TokensUsed,
                        AIModelUsed = result.AIModelUsed,
                        ProcessingTime = result.ProcessingTime,
                        Metadata = result.Metadata
                    });
                }
                else
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("StreamingError", new
                    {
                        StreamId = streamId,
                        Error = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced streaming process for user {UserId}, StreamId: {StreamId}", userId, streamId);

                await _hubContext.Clients.Client(connectionId).SendAsync("StreamingError", new
                {
                    StreamId = streamId,
                    Error = _configuration["ChatService:Messages:StreamingError"] ?? "Lỗi trong quá trình streaming"
                });
            }
        }
        private List<string> SplitResponseIntoChunks(string response)
        {
            var chunkSize = _configuration.GetValue<int>("ChatService:StreamingChunkSize", 50);
            var chunks = new List<string>();

            for (int i = 0; i < response.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, response.Length - i);
                chunks.Add(response.Substring(i, length));
            }

            return chunks;
        }
        private async Task<MessageFeedBackResponse> GetMessageFeedbackAsync(Guid messageId)
        {
            try
            {
                var feedbackRepo = _unitOfWork.GetRepository<MessageFeedback>();
                var feedback = await feedbackRepo.SingleOrDefaultAsync(predicate: f => f.MessageId == messageId);

                return feedback != null ? new MessageFeedBackResponse
                {
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    FeedbackType = feedback.FeedbackType,
                    FeedbackDate = feedback.CreatedAt
                } : null;
            }
            catch
            {
                return null;
            }
        }
        private List<DocumentReference> ParseDocumentReferences(string sourceDocuments)
        {
            if (string.IsNullOrEmpty(sourceDocuments))
                return new List<DocumentReference>();

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
                return new Dictionary<string, object>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(metadata, _jsonOptions) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
        private System.Linq.Expressions.Expression<Func<ChatSession, bool>> BuildSessionFilterPredicate(Guid userId, GetSessionsRequest request)
        {
            return s => s.UserId == userId && !s.IsDeleted &&
                       (!request.FromDate.HasValue || s.CreatedAt >= request.FromDate.Value) &&
                       (!request.ToDate.HasValue || s.CreatedAt <= request.ToDate.Value) &&
                       (string.IsNullOrEmpty(request.Status) || s.Status.ToString() == request.Status);
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

        private System.Linq.Expressions.Expression<Func<ChatMessage, bool>> BuildSearchPredicate(Guid userId, SearchRequest request)
        {
            var searchLower = request.Query.ToLower();
            return m => m.UserId == userId && !m.IsDeleted &&
                       (m.Content.ToLower().Contains(searchLower) || m.AiResponse.ToLower().Contains(searchLower)) &&
                       (!request.FromDate.HasValue || m.CreatedAt >= request.FromDate.Value) &&
                       (!request.ToDate.HasValue || m.CreatedAt <= request.ToDate.Value) &&
                       (!request.SessionIds.Any() || request.SessionIds.Contains(m.SessionId));
        }

        private double CalculateRelevanceScore(string content, string response, string query)
        {
            var queryLower = query.ToLower();
            var contentLower = (content + " " + response).ToLower();
            var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matchCount = queryWords.Count(word => contentLower.Contains(word));
            return (double)matchCount / queryWords.Length;
        }

        private string GenerateMatchContext(string content, string response, string query)
        {
            var queryLower = query.ToLower();
            var fullText = content + " " + response;
            var index = fullText.ToLower().IndexOf(queryLower);

            if (index >= 0)
            {
                var contextSize = _configuration.GetValue<int>("ChatService:SearchContextSize", 100);
                var start = Math.Max(0, index - 50);
                var length = Math.Min(contextSize, fullText.Length - start);
                var context = fullText.Substring(start, length);

                if (start > 0) context = "..." + context;
                if (start + length < fullText.Length) context = context + "...";

                return context;
            }

            return content.Length > 100 ? content.Substring(0, 100) + "..." : content;
        }
    }
}