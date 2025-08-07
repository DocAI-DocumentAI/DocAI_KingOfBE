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
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IPreferenceService preferenceService,
            IManualDocumentSearchService manualDocumentSearchService,
            ILogger<ChatService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _preferenceService = preferenceService;
            _manualDocumentSearchService = manualDocumentSearchService;
            _logger = logger;
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
        {
            await ValidateMessageStrictAsync(request.Message);

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);

            // Validate model switching
            if (!string.IsNullOrEmpty(request.ModelName) &&
                !string.IsNullOrEmpty(session.Id) &&
                session.ModelName != request.ModelName)
            {
                throw new InvalidOperationException(
                    $"Không thể thay đổi model trong session đã có conversation. " +
                    $"Session hiện tại sử dụng {session.ModelName}. " +
                    $"Để sử dụng {request.ModelName}, vui lòng tạo session mới.");
            }

            // Validate session model is still active
            var isSessionModelActive = await IsModelActiveAsync(session.ModelName);
            if (!isSessionModelActive)
            {
                throw new InvalidOperationException(
                    $"Model '{session.ModelName}' đã bị tắt bởi admin. " +
                    $"Session này không thể tiếp tục. Vui lòng tạo session mới với model khác.");
            }

            // Check if first message (for title generation)
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

            _logger.LogInformation("Processing chat message for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // Document search if applicable
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

            // Build chat history and add temporary user message
            var chatHistory = await BuildChatHistoryAsync(session.Id);
            chatHistory.AddUserMessage(request.Message);

            // Inject document context if available
            if (!string.IsNullOrEmpty(documentAnswer))
            {
                _logger.LogInformation("Injecting document context into chat history");
                chatHistory = InjectDocumentContext(chatHistory, documentAnswer);
            }

            // Get AI response
            var aiResponse = await _semanticKernelService.GetChatResponseAsync(session.ModelName, chatHistory);

            // Validate AI response
            if (string.IsNullOrEmpty(aiResponse))
            {
                _logger.LogError("AI service returned empty response for session {SessionId}", session.Id);
                throw new InvalidOperationException(MessageConstant.AI.ResponseGenerationFailed);
            }

            _logger.LogInformation("AI response generated successfully, length: {Length}", aiResponse.Length);

            // Create and save messages
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(request.Message, session.ModelName),
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
                TokenCount = _tokenCountService.CountTokens(aiResponse, session.ModelName),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow.AddMilliseconds(1), // Ensure order
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save both messages
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            // Update session
            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            // Generate title for first message
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
                    if (string.IsNullOrEmpty(session.Title))
                    {
                        session.Title = ChatConstants.DefaultSessionTitle;
                    }
                }
            }

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();

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

        public async Task<IAsyncEnumerable<string>> SendMessageStreamAsync(ChatRequest request, string userId)
        {
            await ValidateMessageStrictAsync(request.Message);

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);

            // Model validation (same as non-stream)
            if (!string.IsNullOrEmpty(request.ModelName) &&
                !string.IsNullOrEmpty(session.Id) &&
                session.ModelName != request.ModelName)
            {
                throw new InvalidOperationException(
                    $"Không thể thay đổi model trong session đã có conversation. " +
                    $"Session hiện tại sử dụng {session.ModelName}. " +
                    $"Để sử dụng {request.ModelName}, vui lòng tạo session mới.");
            }

            var isSessionModelActive = await IsModelActiveAsync(session.ModelName);
            if (!isSessionModelActive)
            {
                throw new InvalidOperationException(
                    $"Model '{session.ModelName}' đã bị tắt bởi admin. " +
                    $"Session này không thể tiếp tục. Vui lòng tạo session mới với model khác.");
            }

            // Check first message
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

            _logger.LogInformation("Processing streaming chat for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // Document search
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

            // Build chat history
            var chatHistory = await BuildChatHistoryAsync(session.Id);
            chatHistory.AddUserMessage(request.Message);

            if (!string.IsNullOrEmpty(documentAnswer))
            {
                chatHistory = InjectDocumentContext(chatHistory, documentAnswer);
            }

            var responseStream = await _semanticKernelService.GetChatResponseStreamAsync(session.ModelName, chatHistory);

            return WrapStreamWithSave(responseStream, session.Id, userId, request.Message, isFirstMessage, session.ModelName);
        }

        public async Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string userId)
        {
            var modelName = request.ModelName;

            // Validate requested model
            if (!string.IsNullOrEmpty(modelName))
            {
                var isValidModel = await IsModelActiveAsync(modelName);
                if (!isValidModel)
                {
                    var availableModels = await GetAvailableModelNamesAsync();
                    _logger.LogWarning("Model validation failed. Requested: '{ModelName}', Available: [{AvailableModels}]",
                        modelName, string.Join(", ", availableModels));

                    throw new ArgumentException(
                        $"Model '{modelName}' không khả dụng. " +
                        $"Models có sẵn: {string.Join(", ", availableModels)}");
                }
            }
            else
            {
                // Use default active model
                modelName = await GetDefaultActiveModelAsync();
            }

            var session = new ChatSession
            {
                Title = string.IsNullOrEmpty(request.Title) ? ChatConstants.DefaultSessionTitle : request.Title,
                UserId = userId,
                ModelName = modelName,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatSession>().InsertAsync(session);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created new session {SessionId} for user {UserId} with model {ModelName}",
                session.Id, userId, modelName);

            return _mapper.Map<SessionResponse>(session);
        }

        public async Task<SessionDetailResponse> GetSessionAsync(string sessionId, string userId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(
                    predicate: s => s.Id == sessionId && s.UserId == userId,
                    include: q => q.Include(a => a.Messages).Include(a => a.Preferences));

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            var response = _mapper.Map<SessionDetailResponse>(session);
            response.Messages = response.Messages.OrderBy(m => m.Timestamp).ToList();
            response.IsModelActive = await IsModelActiveAsync(session.ModelName);

            return response;
        }

        public async Task<List<SessionResponse>> GetUserSessionsAsync(string userId)
        {
            var sessions = await _unitOfWork.GetRepository<ChatSession>()
                .GetListAsync(
                    predicate: s => s.UserId == userId && s.IsActive,
                    orderBy: q => q.OrderByDescending(s => s.LastActiveAt),
                    include: query => query.Include(s => s.Messages));

            var responses = _mapper.Map<List<SessionResponse>>(sessions);

            // Add additional info for each session
            foreach (var response in responses)
            {
                var session = sessions.First(s => s.Id == response.Id);
                response.MessageCount = session.Messages.Count;
                response.IsModelActive = await IsModelActiveAsync(session.ModelName);
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

            var warningThreshold = (int)(ChatConstants.MaxTokenLimit * ChatConstants.TokenWarningThreshold);
            if (tokenCount > warningThreshold)
            {
                return ApiResponse<object>.Ok(null,
                    string.Format(MessageConstant.Chat.TokenWarning, tokenCount, ChatConstants.MaxTokenLimit));
            }

            return ApiResponse<object>.Ok(null, MessageConstant.Chat.MessageValid);
        }

        public async Task<List<AvailableModelResponse>> GetAvailableModelsAsync()
        {
            var activeConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(
                    predicate: c => c.IsActive,
                    orderBy: q => q.OrderBy(c => c.DisplayName));

            if (!activeConfigs.Any())
            {
                _logger.LogError("No active models found in database");
                throw new InvalidOperationException("Không có model nào được kích hoạt. Vui lòng liên hệ quản trị viên.");
            }

            var defaultModel = activeConfigs.First();

            var result = activeConfigs.Select(c => new AvailableModelResponse
            {
                ModelName = c.ModelName,
                DisplayName = c.DisplayName,
                MaxTokens = c.MaxTokens,
                IsDefault = c.Id == defaultModel.Id,
                IsFree = c.IsFree,
                Temperature = c.Temperature,
                TopP = c.TopP
            }).ToList();

            return result;
        }

        public async Task<bool> SwitchSessionModelAsync(string sessionId, string newModelName, string userId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            // Check if session has messages
            var hasMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId);

            if (hasMessages.Any())
            {
                throw new InvalidOperationException(
                    "Không thể thay đổi model trong session đã có conversation. " +
                    "Vui lòng tạo session mới để sử dụng model khác.");
            }

            // Validate new model
            var isValidModel = await IsModelActiveAsync(newModelName);
            if (!isValidModel)
            {
                var availableModels = await GetAvailableModelNamesAsync();
                throw new ArgumentException(
                    $"Model '{newModelName}' không khả dụng. " +
                    $"Models có sẵn: {string.Join(", ", availableModels)}");
            }

            if (session.ModelName == newModelName)
                return true; // Already using this model

            session.ModelName = newModelName;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Switched model for empty session {SessionId} to {ModelName}", sessionId, newModelName);
            return true;
        }

        #region Private Helper Methods

        private async Task ValidateMessageStrictAsync(string message)
        {
            var validation = await ValidateMessageAsync(message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }
        }

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                // Creating new session
                var validModelName = await GetValidActiveModelAsync(modelName);

                var newSession = new ChatSession
                {
                    Title = ChatConstants.DefaultSessionTitle,
                    UserId = userId,
                    ModelName = validModelName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    LastActiveAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ChatSession>().InsertAsync(newSession);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Created new session {SessionId} for user {UserId} with model {ModelName}",
                    newSession.Id, userId, validModelName);
                return newSession;
            }

            // Get existing session
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            return session;
        }

        private async Task<string> GetValidActiveModelAsync(string requestedModelName)
        {
            if (!string.IsNullOrEmpty(requestedModelName))
            {
                var isValid = await IsModelActiveAsync(requestedModelName);
                if (isValid)
                    return requestedModelName;

                var availableModels = await GetAvailableModelNamesAsync();
                throw new ArgumentException(
                    $"Model '{requestedModelName}' không khả dụng. " +
                    $"Models có sẵn: {string.Join(", ", availableModels)}");
            }

            return await GetDefaultActiveModelAsync();
        }

        private async Task<bool> IsModelActiveAsync(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return false;

            var normalizedModelName = NormalizeModelName(modelName);

            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName && c.IsActive);

            return config != null;
        }

        private async Task<string> GetDefaultActiveModelAsync()
        {
            var activeConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(predicate: c => c.IsActive, orderBy: q => q.OrderBy(c => c.DisplayName));

            if (!activeConfigs.Any())
                throw new InvalidOperationException("Không có model nào được kích hoạt. Vui lòng liên hệ quản trị viên.");

            return activeConfigs.First().ModelName;
        }

        private async Task<List<string>> GetAvailableModelNamesAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.Select(m => m.ModelName).ToList();
        }

        private async Task<ChatHistory> BuildChatHistoryAsync(string sessionId)
        {
            var messages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(
                    predicate: m => m.SessionId == sessionId,
                    orderBy: q => q.OrderBy(m => m.CreatedAt));

            return await BuildChatHistoryFromMessages(sessionId, messages.ToList());
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

            var currentModelName = await GetCurrentModelNameAsync(sessionId);
            if (!_tokenCountService.IsContextWithinLimit(chatHistory, currentModelName))
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

        private async Task<string> BuildSystemPromptAsync(string sessionId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            var baseSystemPrompt = ChatConstants.SystemPrompt;

            if (session != null)
            {
                var normalizedModelName = NormalizeModelName(session.ModelName);

                var aiConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName && c.IsActive);

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

        private async Task<string> GetDefaultModelNameAsync()
        {
            try
            {
                var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.IsActive);

                return defaultConfig?.ModelName ?? ChatConstants.DefaultModelName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get default model name, using fallback");
                return ChatConstants.DefaultModelName;
            }
        }

        private async IAsyncEnumerable<string> WrapStreamWithSave(
            IAsyncEnumerable<string> stream,
            string sessionId,
            string userId,
            string userMessageContent,
            bool isFirstMessage,
            string modelName)
        {
            var fullResponse = new StringBuilder();

            await foreach (var token in stream)
            {
                fullResponse.Append(token);
                yield return token;
            }

            try
            {
                var userMessage = new ChatMessage
                {
                    Content = userMessageContent,
                    Role = MessageRole.User,
                    TokenCount = _tokenCountService.CountTokens(userMessageContent, modelName),
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-1),
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var aiMessage = new ChatMessage
                {
                    Content = fullResponse.ToString(),
                    Role = MessageRole.Assistant,
                    TokenCount = _tokenCountService.CountTokens(fullResponse.ToString(), modelName),
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

                    if (isFirstMessage && (string.IsNullOrEmpty(session.Title) ||
                        session.Title == ChatConstants.DefaultSessionTitle))
                    {
                        try
                        {
                            var newTitle = await _semanticKernelService.GenerateTitleAsync(userMessageContent);
                            if (!string.IsNullOrEmpty(newTitle))
                            {
                                session.Title = newTitle;
                                _logger.LogInformation("Generated title for streaming session {SessionId}: {Title}", sessionId, newTitle);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Title generation failed for streaming session {SessionId}, keeping default", sessionId);
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
            }
        }

        private ChatHistory InjectDocumentContext(ChatHistory originalHistory, string documentContext)
        {
            var enhancedHistory = new ChatHistory();

            var originalSystemMessage = originalHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (originalSystemMessage != null)
            {
                var enhancedSystemPrompt = originalSystemMessage.Content +
                    $"\n\n=== THÔNG TIN TÀI LIỆU LIÊN QUAN ===\n{documentContext}\n=== HẾT THÔNG TIN TÀI LIỆU ===\n\n" +
                    "Hãy sử dụng thông tin tài liệu trên để trả lời câu hỏi của người dùng.";

                enhancedHistory.AddSystemMessage(enhancedSystemPrompt);
                _logger.LogDebug("Enhanced system prompt with document context for session");
            }

            foreach (var message in originalHistory.Where(m => m.Role != AuthorRole.System))
            {
                enhancedHistory.Add(message);
            }

            return enhancedHistory;
        }

        private string NormalizeModelName(string modelName) =>
            Uri.UnescapeDataString(modelName ?? string.Empty).Trim().ToLowerInvariant();

        #endregion
    }
}
