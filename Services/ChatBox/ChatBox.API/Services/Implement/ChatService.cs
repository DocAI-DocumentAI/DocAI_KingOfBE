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
        private readonly IConfiguration _configuration;
        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
             IConfiguration configuration,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IPreferenceService preferenceService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _configuration = configuration;
            _preferenceService = preferenceService;
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
        {
            // Validate message
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            // Get or create session
            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
       .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var messageCount = userMessages.Count;
            var isFirstMessage = messageCount == 0;


            // Save user message
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(request.Message),
                SessionId = session.Id,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);

            // Build chat history
            var chatHistory = await BuildChatHistoryAsync(session.Id);

            // Get AI response
            var aiResponse = await _semanticKernelService.GetChatResponseAsync(session.ModelName, chatHistory);

            // Save AI message
            var aiMessage = new ChatMessage
            {
                Content = aiResponse,
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(aiResponse),
                SessionId = session.Id,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            // Update session
            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            // Generate title for first message if not exists
            if (isFirstMessage && (string.IsNullOrEmpty(session.Title) || session.Title == _configuration["ChatService:DefaultSessionTitle"]))
            {
                session.Title = await _semanticKernelService.GenerateTitleAsync(request.Message);
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
            // Validate message
            var validation = await ValidateMessageAsync(request.Message);
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Message);
            }

            // Get or create session
            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);

            // Check if this is first user message
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == session.Id && m.Role == MessageRole.User);
            var isFirstMessage = userMessages.Count == 0;
            // Save user message
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(request.Message),
                SessionId = session.Id,    
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);

            await _unitOfWork.CommitAsync();

            // Build chat history
            var chatHistory = await BuildChatHistoryAsync(session.Id);

            // Get streaming response
            var responseStream = await _semanticKernelService.GetChatResponseStreamAsync(session.ModelName, chatHistory);

            // Return wrapped stream that saves response
            return WrapStreamWithSave(responseStream, session.Id, userId, request.Message, isFirstMessage);

        }

        public async Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string userId)
        {
            var session = new ChatSession
            {
                Title = string.IsNullOrEmpty(request.Title) ? _configuration["ChatService:DefaultSessionTitle"] : request.Title,
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
                    include: q => q
                .Include(a => a.Messages)
                .Include(a => a.Preferences));


            if (session == null)
                throw new ArgumentException("Không tìm thấy phiên chat.");

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

            // Add message count
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

        public async Task<string> SuggestTitleAsync(string firstMessage)
        {
            return await _semanticKernelService.GenerateTitleAsync(firstMessage);
        }

        public async Task<ApiResponse<object>> ValidateMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return ApiResponse<object>.Fail(_configuration["ChatService:Messages:EmptyMessage"]);
            }

            var maxLength = _configuration.GetValue<int>("ChatService:MaxMessageLength");
            if (message.Length > maxLength)
            {
                return ApiResponse<object>.Fail(
                    string.Format(_configuration["ChatService:Messages:MessageTooLong"], maxLength));
            }

            var tokenCount = _tokenCountService.CountTokens(message);
            var maxTokens = _configuration.GetValue<int>("ChatService:MaxTokenLimit");

            if (tokenCount > maxTokens)
            {
                return ApiResponse<object>.Fail(
                    string.Format(_configuration["ChatService:Messages:TokenLimitExceeded"],
                        tokenCount, maxTokens));
            }

            var warningThreshold = _configuration.GetValue<double>("ChatService:TokenWarningThreshold");
            if (tokenCount > maxTokens * warningThreshold)
            {
                return ApiResponse<object>.Ok(null,
                    string.Format(_configuration["ChatService:Messages:TokenWarning"],
                        tokenCount, maxTokens));
            }

            return ApiResponse<object>.Ok(null, _configuration["ChatService:Messages:MessageValid"]);
        }

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId)
        {
            var now = DateTime.UtcNow;
            if (string.IsNullOrEmpty(sessionId))
            {
                var newSession = new ChatSession
                {
                    Title = _configuration["ChatService:DefaultSessionTitle"],
                    UserId = userId,
                    ModelName = modelName ?? _configuration["ChatService:DefaultModelName"],
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                };

                await _unitOfWork.GetRepository<ChatSession>().InsertAsync(newSession);
                await _unitOfWork.CommitAsync();
                return newSession;
            }

            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                throw new ArgumentException(_configuration["ChatService:Messages:SessionNotFound"]);

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

        private async Task<ChatHistory> BuildChatHistoryAsync(string sessionId)
        {
            var messages = await _unitOfWork.GetRepository<ChatMessage>()
            .GetListAsync(predicate: m => m.SessionId == sessionId,
                orderBy: q => q.OrderBy(m => m.Timestamp));

            var chatHistory = new ChatHistory();

            // Add system message with preferences
            var systemPrompt = await BuildSystemPromptAsync(sessionId);
            chatHistory.AddSystemMessage(systemPrompt);

            // Add conversation messages
            foreach (var message in messages)
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

            // Reduce chat history if too long
            var maxHistoryCount = _configuration.GetValue<int>("ChatService:MaxChatHistoryCount");
            if (chatHistory.Count > maxHistoryCount)
            {
                chatHistory = await _semanticKernelService.ReduceChatHistoryAsync(chatHistory);
            }

            systemPrompt += "\n\nBạn có thể sử dụng chức năng SearchDocuments để tìm thông tin trong tài liệu công ty khi người dùng hỏi về chính sách, quy trình, hướng dẫn.";


            return chatHistory;
        }
        private async Task<string> BuildSystemPromptAsync(string sessionId)
        {
            var aiConfig = await GetCurrentAIConfigurationAsync();
            var systemPrompt = aiConfig?.SystemPrompt ?? _configuration["ChatService:SystemPrompt"];

            var session = await _unitOfWork.GetRepository<ChatSession>()
           .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            if (session != null)
            {
                var preferences = await _preferenceService.GetEffectivePreferencesAsync(sessionId, session.UserId);

                if (!string.IsNullOrEmpty(preferences.UserName))
                {
                    systemPrompt += $" Bạn có thể gọi người dùng là {preferences.UserName}.";
                }

                if (preferences.ChatbotCharacteristics.Any())
                {
                    var characteristicNames = preferences.ChatbotCharacteristics
                        .Select(c => ChatbotCharacteristics.GetDisplayName(c))
                        .Where(name => !string.IsNullOrEmpty(name));

                    if (characteristicNames.Any())
                    {
                        systemPrompt += $" Phong cách giao tiếp của bạn nên: {string.Join(", ", characteristicNames)}.";
                    }
                }

                if (!string.IsNullOrEmpty(preferences.AdditionalInfo))
                {
                    systemPrompt += $" Thông tin bổ sung về người dùng: {preferences.AdditionalInfo}.";
                }
            }

            return systemPrompt;
        }

        private async Task<string> GetDefaultModelNameAsync()
        {

            var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return defaultConfig?.ModelName ?? _configuration["ChatService:DefaultModelName"];
        }
        private async Task<AIConfiguration> GetCurrentAIConfigurationAsync()
        {
            return await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);
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

            // Save complete AI response after streaming
            var aiMessage = new ChatMessage
            {
                Content = fullResponse.ToString(),
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(fullResponse.ToString()),
                SessionId = sessionId,
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

                if (isFirstMessage && (string.IsNullOrEmpty(session.Title) || session.Title == _configuration["ChatService:DefaultSessionTitle"]))
                {
                    session.Title = await _semanticKernelService.GenerateTitleAsync(firstMessage);
                }

                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            }

            await _unitOfWork.CommitAsync();
        }
    }
} 
