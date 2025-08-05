using System.Text;
using System.Text.Json;
using AutoMapper;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Enum;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBox.API.Services.Implement
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ISemanticKernelService _semanticKernelService;
        private readonly ITokenCountService _tokenCountService;
        private readonly IPreferenceService _preferenceService;
        private readonly IManualDocumentSearchService _manualDocumentSearchService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ChatService> _logger;

        private readonly TimeSpan _chatHistoryCacheDuration = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _sessionCacheDuration = TimeSpan.FromMinutes(30);
        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IPreferenceService preferenceService,
            IManualDocumentSearchService manualDocumentSearchService,
            ICacheService cacheService,
            ILogger<ChatService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _preferenceService = preferenceService;
            _manualDocumentSearchService = manualDocumentSearchService;
            _cacheService = cacheService;
            _logger = logger;
        }

        // 🔧 FIXED: SendMessageAsync - AI response trước khi save
        public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
        {
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);

            if (!string.IsNullOrEmpty(request.ModelName) &&
                !string.IsNullOrEmpty(session.Id) &&
                session.ModelName != request.ModelName)
            {
                throw new InvalidOperationException(
                    $"Không thể thay đổi model trong phiên chat hiện tại. " +
                    $"Model hiện tại: {session.ModelName}. " +
                    $"Để sử dụng model {request.ModelName}, vui lòng tạo phiên chat mới.");
            }

            // 🔧 Check first message (chấp nhận race condition)
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

            _logger.LogInformation("Processing chat message for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // 🔧 Document search
            string documentAnswer = null;
            if (_manualDocumentSearchService.ShouldSearchDocuments(request.Message))
            {
                _logger.LogInformation("Triggering document search for message");
                try
                {
                    documentAnswer = await _manualDocumentSearchService.SearchAndAnswerAsync(request.Message, userId);
                    if (!string.IsNullOrEmpty(documentAnswer))
                    {
                        _logger.LogInformation("Document search returned {Length} characters", documentAnswer.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Document search failed, continuing without document context");
                }
            }

            // 🔧 FIXED: Build chat history và add temp user message
            var chatHistory = await BuildChatHistoryAsync(session.Id);
            chatHistory.AddUserMessage(request.Message); // Add temporary user message for AI context

            if (!string.IsNullOrEmpty(documentAnswer))
            {
                _logger.LogInformation("Injecting document context into chat history");
                chatHistory = InjectDocumentContext(chatHistory, documentAnswer);
            }

            // 🔧 FIXED: Get AI response TRƯỚC khi save user message
            var aiResponse = await _semanticKernelService.GetChatResponseAsync(session.ModelName, chatHistory);

            // 🔧 FIXED: Nếu AI fail → throw exception (như chatbot hiện tại)
            if (string.IsNullOrEmpty(aiResponse))
            {
                _logger.LogError("AI service returned empty response for session {SessionId}", session.Id);
                throw new InvalidOperationException(MessageConstant.AI.ResponseGenerationFailed);
            }

            _logger.LogInformation("AI response generated successfully, length: {Length}", aiResponse.Length);

            // 🔧 FIXED: CHỈ KHI AI response thành công mới save messages
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(request.Message),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var aiMessage = new ChatMessage
            {
                Content = aiResponse,
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(aiResponse),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow.AddMilliseconds(1), // Ensure order
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save both messages together
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            // Update session
            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            // 🔧 FIXED: Title generation - keep default nếu fail
            if (isFirstMessage && (string.IsNullOrEmpty(session.Title) || session.Title == ChatConstants.DefaultSessionTitle))
            {
                try
                {
                    var newTitle = await _semanticKernelService.GenerateTitleAsync(request.Message);
                    if (!string.IsNullOrEmpty(newTitle))
                    {
                        session.Title = newTitle;
                        _logger.LogInformation("Generated title for session {SessionId}: {Title}", session.Id, newTitle);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Title generation failed for session {SessionId}, keeping default", session.Id);
                    // Keep default title - không thay đổi gì
                    if (string.IsNullOrEmpty(session.Title))
                    {
                        session.Title = ChatConstants.DefaultSessionTitle;
                    }
                }
            }

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync(); // Commit tất cả cùng lúc

            _logger.LogInformation("Chat message processed successfully for session {SessionId}", session.Id);

            return new ChatResponse
            {
                SessionId = session.Id,
                Message = aiResponse,
                Role = MessageRole.Assistant,
                TokenCount = aiMessage.TokenCount,
                Timestamp = aiMessage.Timestamp,
                ModelUsed = session.ModelName
            };
        }

        // 🔧 FIXED: SendMessageStreamAsync - đồng nhất logic với non-stream
        public async Task<IAsyncEnumerable<string>> SendMessageStreamAsync(ChatRequest request, string userId)
        {
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);

            if (!string.IsNullOrEmpty(request.ModelName) &&
             !string.IsNullOrEmpty(session.Id) &&
             session.ModelName != request.ModelName)
            {
                throw new InvalidOperationException(
                    $"Không thể thay đổi model trong phiên chat hiện tại. " +
                    $"Model hiện tại: {session.ModelName}. " +
                    $"Để sử dụng model {request.ModelName}, vui lòng tạo phiên chat mới.");
            }
            // 🔧 Check first message (giống như non-stream)
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

            _logger.LogInformation("Processing streaming chat for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // Document search (giống như non-stream)
            string documentAnswer = null;
            if (_manualDocumentSearchService.ShouldSearchDocuments(request.Message))
            {
                try
                {
                    documentAnswer = await _manualDocumentSearchService.SearchAndAnswerAsync(request.Message, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Document search failed in streaming mode");
                }
            }

            // 🔧 Build chat history với temp user message (giống non-stream)
            var chatHistory = await BuildChatHistoryAsync(session.Id);
            chatHistory.AddUserMessage(request.Message); // Add temporary

            if (!string.IsNullOrEmpty(documentAnswer))
            {
                chatHistory = InjectDocumentContext(chatHistory, documentAnswer);
            }

            var responseStream = await _semanticKernelService.GetChatResponseStreamAsync(session.ModelName, chatHistory);

            return WrapStreamWithSave(responseStream, session.Id, userId, request.Message, isFirstMessage);
        }

        public async Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string userId)
        {
            var session = new ChatSession
            {
                Title = string.IsNullOrEmpty(request.Title) ?
                   ChatConstants.DefaultSessionTitle : request.Title,
                UserId = userId,
                ModelName = request.ModelName ?? await GetDefaultModelNameAsync(),
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatSession>().InsertAsync(session);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created new session {SessionId} for user {UserId}", session.Id, userId);

            return _mapper.Map<SessionResponse>(session);
        }

        public async Task<SessionDetailResponse> GetSessionAsync(string sessionId, string userId)
        {
            var sessionCacheKey = $"session_detail_{sessionId}";
            var cachedSession = await _cacheService.GetAsync<SessionDetailResponse>(sessionCacheKey);

            if (cachedSession != null)
            {
                // Verify cache freshness
                var lastMessageCacheKey = $"last_message_time_{sessionId}";
                var cachedLastMessageTime = await _cacheService.GetDateTimeAsync(lastMessageCacheKey);
                var actualLastMessageTime = await GetLastMessageTimeFromDB(sessionId);

                if (cachedLastMessageTime >= actualLastMessageTime)
                {
                    _logger.LogDebug("Cache HIT: Session detail for {SessionId}", sessionId);
                    return cachedSession;
                }
                else
                {
                    _logger.LogDebug("Cache STALE: Session detail for {SessionId}", sessionId);
                }
            }

            // Cache miss hoặc stale → query DB
            var session = await _unitOfWork.GetRepository<ChatSession>()
                 .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId,
                     include: q => q.Include(a => a.Messages).Include(a => a.Preferences));

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            var response = _mapper.Map<SessionDetailResponse>(session);
            response.Messages = response.Messages.OrderBy(m => m.Timestamp).ToList();

            // Cache session detail
            try
            {
                await _cacheService.SetAsync(sessionCacheKey, response, _sessionCacheDuration);
                _logger.LogDebug("Cached session detail for {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache session detail for {SessionId}", sessionId);
            }

            return response;
        }

        public async Task<List<SessionResponse>> GetUserSessionsAsync(string userId)
        {
            var sessions = await _unitOfWork.GetRepository<ChatSession>()
                 .GetListAsync(predicate: s => s.UserId == userId && s.IsActive,
                     orderBy: q => q.OrderByDescending(s => s.LastActiveAt),
                     include: query => query.Include(s => s.Messages));

            var responses = _mapper.Map<List<SessionResponse>>(sessions);

            foreach (var response in responses)
            {
                var session = sessions.First(s => s.Id == response.Id);
                response.MessageCount = session.Messages.Count;
            }

            return responses;
        }

        public async Task<bool> DeleteSessionAsync(string sessionId, string userId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                   .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return false;

            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;
            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();

            // Invalidate caches for deleted session
            await InvalidateChatCaches(sessionId);

            _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);

            return true;
        }

        public async Task<ApiResponse<object>> ValidateMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return ApiResponse<object>.Fail(MessageConstant.Chat.EmptyMessage);
            }

            if (message.Length > ChatConstants.MaxMessageLength)
            {
                return ApiResponse<object>.Fail(
                    string.Format(MessageConstant.Chat.MessageTooLong, ChatConstants.MaxMessageLength));
            }

            var tokenCount = _tokenCountService.CountTokens(message);

            if (tokenCount > ChatConstants.MaxTokenLimit)
            {
                return ApiResponse<object>.Fail(
                    string.Format(MessageConstant.Chat.TokenLimitExceeded, tokenCount, ChatConstants.MaxTokenLimit));
            }

            if (tokenCount > ChatConstants.MaxTokenLimit * ChatConstants.TokenWarningThreshold)
            {
                return ApiResponse<object>.Ok(null,
                    string.Format(MessageConstant.Chat.TokenWarning, tokenCount, ChatConstants.MaxTokenLimit));
            }

            return ApiResponse<object>.Ok(null, MessageConstant.Chat.MessageValid);
        }

        public async Task<List<AvailableModelResponse>> GetAvailableModelsAsync()
        {
            var configs = await _unitOfWork.GetRepository<AIConfiguration>()
                  .GetListAsync(predicate: c => c.IsActive,
                      orderBy: q => q.OrderBy(c => c.DisplayName));

            var defaultModel = configs.FirstOrDefault(c => c.IsActive);

            return configs.Select(c => new AvailableModelResponse
            {
                ModelName = c.ModelName,
                DisplayName = c.DisplayName,
                MaxTokens = c.MaxTokens,
                IsDefault = c.Id == defaultModel?.Id,
                IsFree = c.IsFree,
                Temperature = c.Temperature,
                TopP = c.TopP
            }).ToList();
        }

        public async Task<bool> SwitchSessionModelAsync(string sessionId, string newModelName, string userId)
        {
            //var session = await _unitOfWork.GetRepository<ChatSession>()
            //    .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            //if (session == null)
            //    throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            //var config = await _unitOfWork.GetRepository<AIConfiguration>()
            //    .SingleOrDefaultAsync(predicate: c => c.ModelName == newModelName && c.IsActive);

            //if (config == null)
            //    throw new ArgumentException(string.Format(MessageConstant.Admin.ModelNotFound, newModelName));

            //if (session.ModelName == newModelName)
            //    return true;

            //session.ModelName = newModelName;
            //session.UpdatedAt = DateTime.UtcNow;
            //session.UpdatedBy = userId;

            //_unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            //await _unitOfWork.CommitAsync();

            //_logger.LogInformation("Switched model for session {SessionId} to {ModelName}", sessionId, newModelName);

            //return true;
            throw new InvalidOperationException(
                "Không thể thay đổi model trong phiên chat hiện tại. " +
                "Vui lòng tạo phiên chat mới để sử dụng model khác.");
        }

        public async Task<ApiResponse<object>> ValidateConversationContextAsync(string sessionId, string newMessage)
        {
            var messageValidation = await ValidateMessageAsync(newMessage);
            if (!messageValidation.Success)
                return messageValidation;

            var allMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId,
                    orderBy: q => q.OrderByDescending(x => x.Timestamp));

            var messages = allMessages.Take(ChatConstants.ContextValidationMessageCount).ToList();
            var totalTokens = messages.Sum(m => m.TokenCount) + _tokenCountService.CountTokens(newMessage);

            if (totalTokens > ChatConstants.MaxContextTokens)
            {
                return ApiResponse<object>.Fail(
                    string.Format(MessageConstant.Chat.ContextTooLong, totalTokens));
            }

            if (totalTokens > ChatConstants.MaxContextTokens * ChatConstants.TokenWarningThreshold)
            {
                return ApiResponse<object>.Ok(new
                {
                    totalTokens,
                    ChatConstants.MaxContextTokens,
                    messageTokens = _tokenCountService.CountTokens(newMessage),
                    warningLevel = "high"
                }, string.Format(MessageConstant.Chat.ContextWarning, totalTokens, ChatConstants.MaxContextTokens));
            }

            return ApiResponse<object>.Ok(new
            {
                totalTokens,
                ChatConstants.MaxContextTokens,
                messageTokens = _tokenCountService.CountTokens(newMessage),
                warningLevel = "safe"
            });
        }

        #region Private Helper Methods

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                var newSession = new ChatSession
                {
                    Title = ChatConstants.DefaultSessionTitle,
                    UserId = userId,
                    ModelName = await GetValidModelNameAsync(modelName),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    LastActiveAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ChatSession>().InsertAsync(newSession);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Created new session {SessionId} for user {UserId}", newSession.Id, userId);
                return newSession;
            }

            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            //if (!string.IsNullOrEmpty(modelName) && session.ModelName != modelName)
            //{
            //    session.ModelName = modelName;
            //    session.UpdatedAt = DateTime.UtcNow;
            //    session.UpdatedBy = userId;
            //    _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            //    await _unitOfWork.CommitAsync();
            //}
            if (!string.IsNullOrEmpty(modelName) && session.ModelName != modelName)
            {
                _logger.LogWarning("User {UserId} attempted to switch model in session {SessionId}: {CurrentModel} → {RequestedModel}",
                    userId, sessionId, session.ModelName, modelName);
            }

            return session;
        }

        private async Task<ChatHistory> BuildChatHistoryAsync(string sessionId)
        {
            var historyCacheKey = $"chat_history_built_{sessionId}";
            var messagesCacheKey = $"chat_messages_{sessionId}";
            var lastMessageCacheKey = $"last_message_time_{sessionId}";

            // Check cache freshness
            var cachedLastMessageTime = await _cacheService.GetDateTimeAsync(lastMessageCacheKey);
            var actualLastMessageTime = await GetLastMessageTimeFromDB(sessionId);

            bool isCacheStale = cachedLastMessageTime == null ||
                               cachedLastMessageTime < actualLastMessageTime;

            if (isCacheStale)
            {
                _logger.LogDebug("Cache STALE for session {SessionId} - invalidating", sessionId);
                await InvalidateChatCaches(sessionId);
            }

            // Try get built history from cache
            var cachedHistory = await _cacheService.GetAsync<ChatHistoryCache>(historyCacheKey);
            if (cachedHistory != null && !isCacheStale)
            {
                _logger.LogDebug("Cache HIT: Chat history for session {SessionId}", sessionId);
                return DeserializeChatHistory(cachedHistory);
            }

            // Cache miss hoặc stale → rebuild
            _logger.LogDebug("Cache MISS: Rebuilding chat history for session {SessionId}", sessionId);

            var messages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId,
                    orderBy: q => q.OrderBy(m => m.CreatedAt));

            var chatHistory = await BuildChatHistoryFromMessages(sessionId, messages.ToList());

            // Cache everything
            var historyCache = SerializeChatHistory(chatHistory);
            var lastMessageTime = messages.LastOrDefault()?.CreatedAt ?? DateTime.UtcNow;

            var cacheTasks = new[]
            {
                _cacheService.SetAsync(messagesCacheKey, messages, _chatHistoryCacheDuration),
                _cacheService.SetAsync(historyCacheKey, historyCache, _chatHistoryCacheDuration),
                _cacheService.SetAsync(lastMessageCacheKey, lastMessageTime.ToString("O"), _chatHistoryCacheDuration)
            };

            try
            {
                await Task.WhenAll(cacheTasks);
                _logger.LogDebug("Cached chat history for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache chat history for session {SessionId}", sessionId);
            }

            return chatHistory;
        }
        private async Task<ChatHistory> BuildChatHistoryFromMessages(string sessionId, List<ChatMessage> messages)
        {
            var chatHistory = new ChatHistory();
            var systemPrompt = await BuildSystemPromptAsync(sessionId);
            chatHistory.AddSystemMessage(systemPrompt);

            var recentMessages = messages.TakeLast(ChatConstants.MaxHistoryMessages).ToList();

            foreach (var message in recentMessages)
            {
                switch (message.Role)
                {
                    case MessageRole.User:
                        chatHistory.AddUserMessage(message.Content);
                        break;
                    case MessageRole.Assistant:
                        chatHistory.AddAssistantMessage(message.Content);
                        break;
                }
            }

            if (!_tokenCountService.IsContextWithinLimit(chatHistory, await GetCurrentModelNameAsync(sessionId)))
            {
                var reducedMessages = recentMessages.TakeLast(ChatConstants.MinHistoryMessages).ToList();
                var reducedHistory = new ChatHistory();
                reducedHistory.AddSystemMessage(systemPrompt);

                foreach (var message in reducedMessages)
                {
                    switch (message.Role)
                    {
                        case MessageRole.User:
                            reducedHistory.AddUserMessage(message.Content);
                            break;
                        case MessageRole.Assistant:
                            reducedHistory.AddAssistantMessage(message.Content);
                            break;
                    }
                }
                return reducedHistory;
            }

            return chatHistory;
        }
        private async Task<DateTime?> GetLastMessageTimeFromDB(string sessionId)
        {
            try
            {
                var lastMessage = await _unitOfWork.GetRepository<ChatMessage>()
                    .SingleOrDefaultAsync(
                        predicate: m => m.SessionId == sessionId,
                        orderBy: q => q.OrderByDescending(m => m.CreatedAt));

                return lastMessage?.CreatedAt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get last message time for session {SessionId}", sessionId);
                return null; // Force cache refresh
            }
        }
        private async Task UpdateCacheWithNewMessages(string sessionId, ChatMessage[] newMessages)
        {
            try
            {
                var messagesCacheKey = $"chat_messages_{sessionId}";
                var historyCacheKey = $"chat_history_built_{sessionId}";
                var lastMessageCacheKey = $"last_message_time_{sessionId}";

                // Get existing cached messages
                var cachedMessages = await _cacheService.GetAsync<List<ChatMessage>>(messagesCacheKey);

                if (cachedMessages != null)
                {
                    // Append new messages to cache
                    cachedMessages.AddRange(newMessages);

                    // Rebuild history with updated messages
                    var updatedHistory = await BuildChatHistoryFromMessages(sessionId, cachedMessages);
                    var historyCache = SerializeChatHistory(updatedHistory);
                    var lastMessageTime = newMessages.Max(m => m.CreatedAt);

                    // Update all caches atomically
                    var updateTasks = new[]
                    {
                        _cacheService.SetAsync(messagesCacheKey, cachedMessages, _chatHistoryCacheDuration),
                        _cacheService.SetAsync(historyCacheKey, historyCache, _chatHistoryCacheDuration),
                        _cacheService.SetAsync(lastMessageCacheKey, lastMessageTime.ToString("O"), _chatHistoryCacheDuration) // ISO format
                    };

                    await Task.WhenAll(updateTasks);

                    _logger.LogDebug("Successfully updated cache for session {SessionId} with {Count} new messages",
                        sessionId, newMessages.Length);
                }
                else
                {
                    // Cache miss → invalidate to force fresh rebuild next time
                    _logger.LogDebug("Cache miss during update, invalidating for session {SessionId}", sessionId);
                    await InvalidateChatCaches(sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cache, invalidating for session {SessionId}", sessionId);
                // Fallback: invalidate cache to ensure consistency
                await InvalidateChatCaches(sessionId);
            }
        }
        private async Task InvalidateChatCaches(string sessionId)
        {
            var cacheKeys = new[]
            {
                $"chat_history_built_{sessionId}",
                $"chat_messages_{sessionId}",
                $"last_message_time_{sessionId}",
                $"session_detail_{sessionId}"
            };

            var invalidationTasks = cacheKeys.Select(key => _cacheService.RemoveAsync(key));

            try
            {
                await Task.WhenAll(invalidationTasks);
                _logger.LogDebug("Invalidated all caches for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate some caches for session {SessionId}", sessionId);
            }
        }
        private ChatHistoryCache SerializeChatHistory(ChatHistory history)
        {
            return new ChatHistoryCache
            {
                Messages = history.Select(msg => new CachedChatMessage
                {
                    Role = msg.Role.ToString(),
                    Content = msg.Content ?? string.Empty
                }).ToList(),
                CachedAt = DateTime.UtcNow
            };
        }

        private ChatHistory DeserializeChatHistory(ChatHistoryCache cache)
        {
            var history = new ChatHistory();
            foreach (var msg in cache.Messages)
            {
                var role = Enum.Parse<AuthorRole>(msg.Role);
                history.Add(new ChatMessageContent(role, msg.Content));
            }
            return history;
        }
        private async Task<string> GetValidModelNameAsync(string requestedModelName)
        {
            if (string.IsNullOrEmpty(requestedModelName))
            {
                var cachedDefault = await _cacheService.GetAsync<string>("default_active_model");
                if (!string.IsNullOrEmpty(cachedDefault))
                {
                    return cachedDefault;
                }

                var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.IsActive);

                var modelName = defaultConfig?.ModelName ?? ChatConstants.DefaultModelName;
                await _cacheService.SetAsync("default_active_model", modelName, TimeSpan.FromMinutes(10));
                return modelName;
            }

            var cacheKey = $"model_valid_{requestedModelName}";
            var cachedValid = await _cacheService.GetAsync<string>(cacheKey);
            if (!string.IsNullOrEmpty(cachedValid))
            {
                return cachedValid;
            }

            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == requestedModelName && c.IsActive);

            var validModelName = config?.ModelName ?? await GetValidModelNameAsync(null);
            await _cacheService.SetAsync(cacheKey, validModelName, TimeSpan.FromMinutes(5));
            return validModelName;
        }
        private async Task<string> BuildSystemPromptAsync(string sessionId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            var baseSystemPrompt = ChatConstants.SystemPrompt;

            if (session != null)
            {
                var aiConfig = await _unitOfWork.GetRepository<AIConfiguration>()
               .SingleOrDefaultAsync(predicate: c => c.ModelName == session.ModelName && c.IsActive);

                if (aiConfig != null && !string.IsNullOrEmpty(aiConfig.SystemPrompt))
                {
                    baseSystemPrompt = aiConfig.SystemPrompt;
                }

                var preferences = await _preferenceService.GetEffectivePreferencesAsync(sessionId, session.UserId);

                if (!string.IsNullOrEmpty(preferences.UserName))
                {
                    baseSystemPrompt += $" {string.Format(ChatConstants.UserNamePromptTemplate, preferences.UserName)}";
                }

                if (preferences.ChatbotCharacteristics.Any())
                {
                    var characteristics = preferences.ChatbotCharacteristics
                             .Take(ChatConstants.MaxCharacteristics)
                             .Select(c => ChatbotCharacteristics.GetDisplayName(c))
                             .Where(name => !string.IsNullOrEmpty(name));

                    if (characteristics.Any())
                    {
                        baseSystemPrompt += $" {string.Format(ChatConstants.CharacteristicsPromptTemplate, string.Join(", ", characteristics))}";
                    }
                }

                if (!string.IsNullOrEmpty(preferences.AdditionalInfo))
                {
                    var additionalInfo = preferences.AdditionalInfo.Length > ChatConstants.MaxAdditionalInfoLength
                        ? preferences.AdditionalInfo.Substring(0, ChatConstants.MaxAdditionalInfoLength) + "..."
                        : preferences.AdditionalInfo;

                    baseSystemPrompt += $" {string.Format(ChatConstants.AdditionalInfoPromptTemplate, additionalInfo)}";
                }
            }

            baseSystemPrompt += $" {ChatConstants.DocumentSearchPromptAddition}";

            return baseSystemPrompt;
        }
        private async Task<string> GetCurrentModelNameAsync(string sessionId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            return session?.ModelName ?? await GetDefaultModelNameAsync();
        }

        // 🔧 FIXED: WrapStreamWithSave - save messages together như non-stream
        private async IAsyncEnumerable<string> WrapStreamWithSave(
            IAsyncEnumerable<string> stream,
            string sessionId,
            string userId,
            string firstMessage,
            bool isFirstMessage)
        {
            var fullResponse = new StringBuilder();

            await foreach (var token in stream)
            {
                fullResponse.Append(token);
                yield return token;
            }

            try
            {
                // 🔧 FIXED: Save user message và AI message cùng lúc (như non-stream)
                var userMessage = new ChatMessage
                {
                    Content = firstMessage,
                    Role = MessageRole.User,
                    TokenCount = _tokenCountService.CountTokens(firstMessage),
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-1), // Slightly earlier than AI message
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var aiMessage = new ChatMessage
                {
                    Content = fullResponse.ToString(),
                    Role = MessageRole.Assistant,
                    TokenCount = _tokenCountService.CountTokens(fullResponse.ToString()),
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
                await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

                var session = await _unitOfWork.GetRepository<ChatSession>()
                    .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

                if (session != null)
                {
                    session.LastActiveAt = DateTime.UtcNow;
                    session.UpdatedBy = userId;

                    // 🔧 Title generation cho stream (giống non-stream)
                    if (isFirstMessage && (string.IsNullOrEmpty(session.Title) ||
                        session.Title == ChatConstants.DefaultSessionTitle))
                    {
                        try
                        {
                            var newTitle = await _semanticKernelService.GenerateTitleAsync(firstMessage);
                            if (!string.IsNullOrEmpty(newTitle))
                            {
                                session.Title = newTitle;
                                _logger.LogInformation("Generated title for streaming session {SessionId}: {Title}", sessionId, newTitle);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Title generation failed for streaming session {SessionId}, keeping default", sessionId);
                            // Keep default title
                            if (string.IsNullOrEmpty(session.Title))
                            {
                                session.Title = ChatConstants.DefaultSessionTitle;
                            }
                        }
                    }

                    _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                }

                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Streaming chat completed and saved for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save streaming chat data for session {SessionId}", sessionId);
                // Don't throw here as stream is already completed
            }
        }

        private ChatHistory InjectDocumentContext(ChatHistory originalHistory, string documentContext)
        {
            var enhancedHistory = new ChatHistory();

            // Copy system message với context
            var originalSystemMessage = originalHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (originalSystemMessage != null)
            {
                var enhancedSystemPrompt = originalSystemMessage.Content +
                    $"\n\n=== THÔNG TIN TÀI LIỆU LIÊN QUAN ===\n{documentContext}\n=== HẾT THÔNG TIN TÀI LIỆU ===\n\n" +
                    "Hãy sử dụng thông tin tài liệu trên để trả lời câu hỏi của người dùng.";

                enhancedHistory.AddSystemMessage(enhancedSystemPrompt);
                _logger.LogDebug("Enhanced system prompt with document context for session");
            }

            // Copy other messages
            foreach (var message in originalHistory.Where(m => m.Role != AuthorRole.System))
            {
                enhancedHistory.Add(message);
            }

            return enhancedHistory;
        }
        private async Task<string> GetDefaultModelNameAsync()
        {
            try
            {
                // Check cache first
                var cached = await _cacheService.GetAsync<string>("default_active_model");
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogDebug("Cache HIT: Default model name");
                    return cached;
                }

                // Query database for active model
                var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.IsActive);

                var modelName = defaultConfig?.ModelName ?? ChatConstants.DefaultModelName;

                // Cache the result
                try
                {
                    await _cacheService.SetAsync("default_active_model", modelName, TimeSpan.FromMinutes(10));
                    _logger.LogDebug("Cached default model name: {ModelName}", modelName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache default model name");
                }

                return modelName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get default model name, using fallback");
                return ChatConstants.DefaultModelName;
            }
        }
        #endregion
    }
}
