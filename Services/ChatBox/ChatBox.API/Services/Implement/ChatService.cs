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
        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IPreferenceService preferenceService,
            IManualDocumentSearchService manualDocumentSearchService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _preferenceService = preferenceService;
            _manualDocumentSearchService = manualDocumentSearchService;
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
        {
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

            Console.WriteLine($"🔍 [CHAT] Checking if should search documents for: {request.Message}");

            string documentAnswer = null;
            if (_manualDocumentSearchService.ShouldSearchDocuments(request.Message))
            {
                Console.WriteLine($"🔍 [CHAT] Triggering manual document search...");
                documentAnswer = await _manualDocumentSearchService.SearchAndAnswerAsync(request.Message, userId);

                if (!string.IsNullOrEmpty(documentAnswer))
                {
                    Console.WriteLine($"✅ [CHAT] Document search returned {documentAnswer.Length} characters");
                }
                else
                {
                    Console.WriteLine($"❌ [CHAT] Document search returned no results");
                }
            }
            else
            {
                Console.WriteLine($"🔍 [CHAT] No document search needed for this message");
            }

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

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);

            var chatHistory = await BuildChatHistoryAsync(session.Id);
            if (!string.IsNullOrEmpty(documentAnswer))
            {
                Console.WriteLine($"✅ [CHAT] Injecting document context into system prompt");
                chatHistory = InjectDocumentContext(chatHistory, documentAnswer);
            }
            var aiResponse = await _semanticKernelService.GetChatResponseAsync(session.ModelName, chatHistory);

            if (string.IsNullOrEmpty(aiResponse))
            {
                aiResponse = MessageConstant.AI.ResponseGenerationFailed;
            }

            var aiMessage = new ChatMessage
            {
                Content = aiResponse,
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(aiResponse),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            // ✅ GENERATE TITLE for first message - FEATURE PRESERVED
            if (isFirstMessage && (string.IsNullOrEmpty(session.Title) ||
                session.Title == ChatConstants.DefaultSessionTitle))
            {
                try
                {
                    session.Title = await _semanticKernelService.GenerateTitleAsync(request.Message);
                }
                catch
                {
                    session.Title = ChatConstants.DefaultSessionTitle;
                }
            }

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();

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
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);
            string documentAnswer = null;
            if (_manualDocumentSearchService.ShouldSearchDocuments(request.Message))
            {
                documentAnswer = await _manualDocumentSearchService.SearchAndAnswerAsync(request.Message, userId);
            }
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;

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

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
            await _unitOfWork.CommitAsync();

            var chatHistory = await BuildChatHistoryAsync(session.Id);
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

            return _mapper.Map<SessionResponse>(session);
        }

        public async Task<SessionDetailResponse> GetSessionAsync(string sessionId, string userId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                 .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId,
                     include: q => q.Include(a => a.Messages).Include(a => a.Preferences));

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            var response = _mapper.Map<SessionDetailResponse>(session);
            response.Messages = response.Messages.OrderBy(m => m.Timestamp).ToList();

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
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == newModelName && c.IsActive);

            if (config == null)
                throw new ArgumentException(string.Format(MessageConstant.Admin.ModelNotFound, newModelName));

            if (session.ModelName == newModelName)
                return true;

            session.ModelName = newModelName;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();

            return true;
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

            if (totalTokens > ChatConstants.MaxContextTokens * ChatConstants.ContextWarningThreshold)
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
                return newSession;
            }

            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            if (!string.IsNullOrEmpty(modelName) && session.ModelName != modelName)
            {
                session.ModelName = modelName;
                session.UpdatedAt = DateTime.UtcNow;
                session.UpdatedBy = userId;
                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                await _unitOfWork.CommitAsync();
            }

            return session;
        }

        private async Task<string> GetValidModelNameAsync(string requestedModelName)
        {
            if (string.IsNullOrEmpty(requestedModelName))
            {
                var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.IsActive);

                return defaultConfig?.ModelName ?? ChatConstants.DefaultModelName;
            }

            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == requestedModelName && c.IsActive);

            return config?.ModelName ?? await GetValidModelNameAsync(null);
        }
        private async Task<ChatHistory> BuildChatHistoryAsync(string sessionId)
        {
            var messages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId,
                    orderBy: q => q.OrderBy(m => m.CreatedAt));

            var chatHistory = new ChatHistory();
            var systemPrompt = await BuildSystemPromptAsync(sessionId);
            chatHistory.AddSystemMessage(systemPrompt);

            //  MISTRAL OPTIMIZATION: Limit recent messages based on config
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

            // ✅ CHECK TOKEN LIMIT and reduce if necessary
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
        private async Task<string> BuildSystemPromptAsync(string sessionId)
        {
            //var aiConfig = await GetCurrentAIConfigurationAsync();
            //var baseSystemPrompt = aiConfig?.SystemPrompt ?? _configuration["ChatService:SystemPrompt"];

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

        private async Task<string> GetDefaultModelNameAsync()
        {

            var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return defaultConfig?.ModelName ?? ChatConstants.DefaultModelName;
        }
        private async Task<string> GetCurrentModelNameAsync(string sessionId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
            
            return session?.ModelName ?? await GetDefaultModelNameAsync();
        }
        private async Task<AIConfiguration?> GetCurrentAIConfigurationAsync(string modelName)
        {
            return await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName && c.IsActive);
        }
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

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            if (session != null)
            {
                session.LastActiveAt = DateTime.UtcNow;
                session.UpdatedBy = userId;

                //  TITLE GENERATION - FEATURE PRESERVED
                if (isFirstMessage && (string.IsNullOrEmpty(session.Title) ||
                    session.Title == ChatConstants.DefaultSessionTitle))
                {
                    try
                    {
                        session.Title = await _semanticKernelService.GenerateTitleAsync(firstMessage);
                    }
                    catch
                    {
                        session.Title = ChatConstants.DefaultSessionTitle;
                    }
                }

                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            }

            await _unitOfWork.CommitAsync();
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
                Console.WriteLine($"✅ [CONTEXT] Enhanced system prompt with document context");
            }

            // Copy other messages
            foreach (var message in originalHistory.Where(m => m.Role != AuthorRole.System))
            {
                enhancedHistory.Add(message);
            }

            return enhancedHistory;
        }
    }
} 
