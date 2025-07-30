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
        private readonly IContentFilterService _contentFilterService;
        private readonly IPreferenceService _preferenceService;

        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IContentFilterService contentFilterService,
            IPreferenceService preferenceService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _contentFilterService = contentFilterService;
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
            if (isFirstMessage && (string.IsNullOrEmpty(session.Title) || session.Title == DefaultValues.DefaultSessionTitle))
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
                Title = string.IsNullOrEmpty(request.Title) ? DefaultValues.DefaultSessionTitle : request.Title,
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
            // 1. Kiểm tra độ dài cơ bản
            if (string.IsNullOrWhiteSpace(message))
            {
                return ApiResponse<object>.Fail("Tin nhắn không được để trống.");
            }

            if (message.Length > 8000)
            {
                return ApiResponse<object>.Fail("Tin nhắn quá dài. Vui lòng rút ngắn nội dung xuống dưới 8000 ký tự.");
            }

            // 2. Kiểm tra token count
            var tokenCount = _tokenCountService.CountTokens(message);
            if (tokenCount > DefaultValues.MaxTokenLimit)
            {
                return ApiResponse<object>.Fail(
                    $"Tin nhắn chứa {tokenCount} token, vượt quá giới hạn {DefaultValues.MaxTokenLimit} token. " +
                    "Vui lòng rút ngắn nội dung hoặc chia thành nhiều tin nhắn nhỏ hơn.");
            }

            // 3. Cảnh báo nếu gần giới hạn
            if (tokenCount > DefaultValues.MaxTokenLimit * 0.8) // 80% của giới hạn
            {
                return ApiResponse<object>.Ok(null,
                    $"Cảnh báo: Tin nhắn chứa {tokenCount} token, gần đạt giới hạn {DefaultValues.MaxTokenLimit} token.");
            }

            // 4. Kiểm tra từ cấm
            if (!await _contentFilterService.IsContentAllowedAsync(message))
            {
                var prohibitedWords = await _contentFilterService.GetProhibitedWordsInContentAsync(message);
                return ApiResponse<object>.Fail(
                    $"Tin nhắn chứa từ ngữ không phù hợp: {string.Join(", ", prohibitedWords)}. Vui lòng chỉnh sửa và gửi lại.");
            }

            return ApiResponse<object>.Ok(null, "Tin nhắn hợp lệ.");
        }

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId)
        {
            var now = DateTime.UtcNow;
            if (string.IsNullOrEmpty(sessionId))
            {
                // Create new session
                var newSession = new ChatSession
                {
                    Title = DefaultValues.DefaultSessionTitle,
                    UserId = userId,
                    ModelName = modelName ?? await GetDefaultModelNameAsync(),
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
                throw new ArgumentException("Không tìm thấy phiên chat.");

            // Update model if specified
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
            if (chatHistory.Count > DefaultValues.MaxChatHistoryCount)
            {
                chatHistory = await _semanticKernelService.ReduceChatHistoryAsync(chatHistory);
            }

            return chatHistory;
        }
        private async Task<string> BuildSystemPromptAsync(string sessionId)
        {


            var aiConfig = await GetCurrentAIConfigurationAsync();
            var systemPrompt = aiConfig?.SystemPrompt ??
                "Bạn là trợ lý AI thông minh chuyên về tìm kiếm tài liệu nội bộ. Hãy trả lời bằng tiếng Việt chính xác.";

            var preferences = await _preferenceService.GetSessionPreferencesAsync(sessionId);

            // Add user name
            var namePreference = preferences.FirstOrDefault(p => p.Key == PreferenceKeys.UserName);
            if (namePreference != null && !string.IsNullOrEmpty(namePreference.Value))
            {
                systemPrompt += $" Bạn có thể gọi người dùng là {namePreference.Value}.";
            }

            // Add characteristics
            var characteristicPreference = preferences.FirstOrDefault(p => p.Key == PreferenceKeys.ChatbotCharacter);
            if (characteristicPreference != null && !string.IsNullOrEmpty(characteristicPreference.Value))
            {
                try
                {
                    var characteristics = JsonSerializer.Deserialize<List<string>>(characteristicPreference.Value);
                    if (characteristics?.Any() == true)
                    {
                        var characteristicNames = characteristics
                            .Select(c => ChatbotCharacteristics.GetDisplayName(c))
                            .Where(name => !string.IsNullOrEmpty(name));

                        if (characteristicNames.Any())
                        {
                            systemPrompt += $" Phong cách giao tiếp của bạn nên: {string.Join(", ", characteristicNames)}.";
                        }
                    }
                }
                catch
                {
                    // Fallback for old format
                    systemPrompt += $" Đặc điểm của bạn: {characteristicPreference.Value}.";
                }
            }

            // Add additional info
            var additionalInfoPreference = preferences.FirstOrDefault(p => p.Key == PreferenceKeys.AdditionalInfo);
            if (additionalInfoPreference != null && !string.IsNullOrEmpty(additionalInfoPreference.Value))
            {
                systemPrompt += $" Thông tin bổ sung về người dùng: {additionalInfoPreference.Value}.";
            }

            return systemPrompt;
        }

        private async Task<string> GetDefaultModelNameAsync()
        {
            var defaultConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return defaultConfig?.ModelName ?? DefaultValues.DefaultModelName;
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

                if (isFirstMessage && (string.IsNullOrEmpty(session.Title) || session.Title == DefaultValues.DefaultSessionTitle))
                {
                    session.Title = await _semanticKernelService.GenerateTitleAsync(firstMessage);
                }

                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            }

            await _unitOfWork.CommitAsync();
        }
    }
} 
