using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            await ValidateSessionModelConsistency(session, request.ModelName);
            await EnsureSessionModelIsActive(session);

            var isFirstMessage = await IsFirstUserMessageInSession(session.Id);

            _logger.LogInformation("Processing chat message for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // ✅ UPDATED: Get raw document content instead of processed answer
            var (documentContent, documentSources, hasDocumentContext) = await SearchDocumentContext(request.Message, userId, request.DocumentId);
            var aiResponse = await GenerateAIResponse(session, request.Message, documentContent, hasDocumentContext);

            await ValidateAIResponse(aiResponse, session.Id);

            var (userMessage, aiMessage) = CreateChatMessages(request.Message, aiResponse, session, userId, documentSources);
            await SaveMessagesAndUpdateSession(userMessage, aiMessage, session, userId, isFirstMessage, request.Message);

            _logger.LogInformation("Chat message processed successfully for session {SessionId}", session.Id);

            return CreateChatResponse(session, aiResponse, aiMessage, documentSources, hasDocumentContext);
        }

        public async Task<IAsyncEnumerable<ChatStreamResponse>> SendMessageStreamAsync(ChatRequest request, string userId, CancellationToken cancellationToken = default)
        {
            await ValidateMessageStrictAsync(request.Message);

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId);
            await ValidateSessionModelConsistency(session, request.ModelName);
            await EnsureSessionModelIsActive(session);

            var isFirstMessage = await IsFirstUserMessageInSession(session.Id);

            _logger.LogInformation("Processing streaming chat for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // ✅ UPDATED: Get raw document content
            var (documentContent, documentSources, hasDocumentContext) = await SearchDocumentContext(request.Message, userId, request.DocumentId);
            var responseStream = await GenerateAIResponseStream(session, request.Message, documentContent);

            return WrapStreamWithChatResponse(responseStream, session, userId, request.Message, isFirstMessage, documentSources, hasDocumentContext, cancellationToken);
        }

        /// <summary>
        /// ✅ UPDATED: SearchDocumentContext - now returns RAW CONTENT
        /// </summary>
        private async Task<(string DocumentContent, List<DocumentInfo> Sources, bool HasContext)> SearchDocumentContext(string message, string userId, string? documentId = null) // ✅ THÊM documentId
        {
            string documentContent = null;
            List<DocumentInfo> documentSources = new();
            bool hasDocumentContext = false;

            try
            {
                _logger.LogInformation("🔍 [ALWAYS] Performing minimal cost RAW CONTENT search");

                // ✅ FIX: Use SearchWithSourcesAsync ONCE to get both content and sources
                try
                {
                    var (content, sources) = await _manualDocumentSearchService.SearchWithSourcesAsync(message, userId, documentId);

                    if (!string.IsNullOrEmpty(content))
                    {
                        documentContent = content;
                        hasDocumentContext = true;
                        _logger.LogInformation("✅ [ALWAYS] RAW CONTENT found: {Length} chars", content.Length);
                    }

                    if (sources != null && sources.Any())
                    {
                        documentSources = sources;
                        _logger.LogInformation("📄 [SOURCES] Found {Count} document sources", sources.Count);

                        // Log source details for debugging
                        foreach (var source in sources.Take(3))
                        {
                            _logger.LogDebug("📄 [SOURCE] Doc: {DocId}, Title: {Title}, Relevance: {Score}",
                                source.DocumentId, source.Title, source.RelevanceScore);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [SOURCES] No sources returned despite having content");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [SEARCH] SearchWithSourcesAsync failed, trying basic search");

                    // Fallback to basic search if SearchWithSourcesAsync fails
                    try
                    {
                        documentContent = await _manualDocumentSearchService.SearchAndAnswerAsync(message, userId);
                        if (!string.IsNullOrEmpty(documentContent))
                        {
                            hasDocumentContext = true;
                            _logger.LogInformation("✅ [FALLBACK] Got content from basic search: {Length} chars", documentContent.Length);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "❌ [FALLBACK] Basic search also failed");
                    }
                }

                if (!hasDocumentContext)
                {
                    _logger.LogInformation("❌ [ALWAYS] No relevant documents found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "❌ [ALWAYS] Document search failed");
            }

            return (documentContent, documentSources, hasDocumentContext);
        }

        /// <summary>
        /// ✅ UPDATED: Generate AI response with raw document content
        /// </summary>
        private async Task<string> GenerateAIResponse(ChatSession session, string userMessage, string documentContent, bool hasDocumentContext)
        {
            var cleanChatHistory = await BuildCleanChatHistoryAsync(session.Id);
            cleanChatHistory.AddUserMessage(userMessage);

            var aiChatHistory = hasDocumentContext
                ? CreateEnhancedChatHistoryForAI(cleanChatHistory, documentContent, userMessage)
                : cleanChatHistory;

            LogDocumentContextUsage(hasDocumentContext);

            return await _semanticKernelService.GetChatResponseAsync(session.ModelName, aiChatHistory);
        }

        /// <summary>
        /// ✅ UPDATED: Generate streaming AI response with raw content
        /// </summary>
        private async Task<IAsyncEnumerable<string>> GenerateAIResponseStream(ChatSession session, string userMessage, string documentContent)
        {
            var cleanChatHistory = await BuildCleanChatHistoryAsync(session.Id);
            cleanChatHistory.AddUserMessage(userMessage);
            var aiChatHistory = CreateEnhancedChatHistoryForAI(cleanChatHistory, documentContent, userMessage);

            return await _semanticKernelService.GetChatResponseStreamAsync(session.ModelName, aiChatHistory);
        }

        /// <summary>
        /// ✅ KEEP EXISTING: CreateEnhancedChatHistoryForAI - same prompt logic, now with raw content
        /// </summary>
        private ChatHistory CreateEnhancedChatHistoryForAI(ChatHistory cleanHistory, string documentContent, string currentQuestion, List<DocumentInfo> documentSources = null)
        {
            var enhancedHistory = new ChatHistory();
            var originalSystemMessage = cleanHistory.FirstOrDefault(m => m.Role == AuthorRole.System);

            if (originalSystemMessage != null)
            {
                string enhancedSystemPrompt;

                if (!string.IsNullOrEmpty(documentContent))
                {
                    _logger.LogInformation("📄 [CHAT] RAW DOCUMENT CONTENT received: {DocumentContent}",
                        documentContent.Substring(0, Math.Min(200, documentContent.Length)));

                    var actualSourceDocumentTitle = GetActualSourceDocumentTitle(documentContent, documentSources);
                    var citationSuffix = !string.IsNullOrEmpty(actualSourceDocumentTitle)
                        ? $"[Trích từ tài liệu: {actualSourceDocumentTitle}]"
                        : "[Trích từ tài liệu nội bộ]";
                    // ✅ UNCHANGED: Same strict prompt, but now with raw content instead of processed answer
                    enhancedSystemPrompt = originalSystemMessage.Content + $@"

🔒 NGHIÊM NGẶT: CHỈ SỬ DỤNG THÔNG TIN TỪ TÀI LIỆU NỘI BỘ
=== THÔNG TIN TÀI LIỆU NỘI BỘ (DUY NHẤT ĐƯỢC PHÉP SỬ DỤNG) ===
{documentContent}
=== HẾT THÔNG TIN TÀI LIỆU ===

🚨 QUY TẮC BẮT BUỘC KHÔNG ĐƯỢC VI PHẠM:
0. ĐƯỢC GIAO TIẾP CƠ BẢN

1. **NGUỒN THÔNG TIN DUY NHẤT**
   - CHỈ sử dụng thông tin từ phần 'THÔNG TIN TÀI LIỆU NỘI BỘ' ở trên
   - KHÔNG được sử dụng kiến thức bên ngoài hoặc suy luận thêm
   - KHÔNG được bổ sung thông tin không có trong tài liệu

2. **CÁCH TRẢ LỜI HỢP LÝ**
   - Bắt đầu: Theo tài liệu nội bộ:
   - Trích dẫn nguyên văn phần liên quan từ tài liệu
   - Có thể tóm tắt ngắn gọn nếu user yêu cầu cụ thể
   - Kết thúc: {citationSuffix}

3. **KHI THÔNG TIN KHÔNG ĐỦ**
   - Trích dẫn phần có sẵn trong tài liệu trước
   - Sau đó nói: Tài liệu chỉ đề cập đến những nội dung trên, không có thông tin chi tiết về [phần user hỏi thêm]""

4. **ĐƯỢC PHÉP**
   - Trích dẫn nguyên văn từ tài liệu
   - Tóm tắt ngắn gọn khi user yêu cầu
   - Trả lời câu hỏi yes/no dựa trên nội dung tài liệu
   - Giải thích ý nghĩa của thuật ngữ có trong tài liệu

5. **TUYỆT ĐỐI CẤM**
   - Thêm thông tin không có trong tài liệu
   - Sử dụng kiến thức chung hoặc bên ngoài
   - Tự tạo cấu trúc đánh số phức tạp không có trong gốc
   - Thêm câu hỏi tương tác không cần thiết


 VI PHẠM = TỪ CHỐI HOÀN TOÀN";
                }
                else
                {
                    // ✅ UNCHANGED: Same flexible prompt for no document cases
                    enhancedSystemPrompt = originalSystemMessage.Content + $@"

🚨 KHÔNG TÌM THẤY TÀI LIỆU NỘI BỘ - CHỈ ĐƯỢC GIAO TIẾP CƠ BẢN

🔒 QUY TẮC NGHIÊM NGẶT TUYỆT ĐỐI:
- Hệ thống KHÔNG tìm thấy tài liệu nội bộ liên quan
- BẠN PHẢI TỪ CHỐI trả lời MỌI câu hỏi không có tài liệu
- TUYỆT ĐỐI KHÔNG được sử dụng kiến thức chung hoặc bên ngoài
- KHÔNG được trả lời về bất kỳ chủ đề nào khác

🚫 CHỈ ĐƯỢC TRẢ LỜI DUY NHẤT:
'Xin lỗi, hiện tại không có tài liệu nội bộ nào liên quan đến câu hỏi này. Tôi chỉ có thể trả lời các câu hỏi dựa trên tài liệu nội bộ của công ty. Bạn có thể liên hệ bộ phận quản lý tài liệu để được hỗ trợ thêm.'

⛔ TUYỆT ĐỐI KHÔNG ĐƯỢC:
- Trả lời về trứng gà, con gà, hay bất kỳ chủ đề chung nào
- Giải thích về triết học, khoa học, lịch sử
- Đưa ra ý kiến cá nhân hoặc thảo luận
- Chào hỏi dài dòng hoặc hướng dẫn chung
- Thể hiện sự hiểu biết về bất kỳ vấn đề nào ngoài tài liệu nội bộ

🔴 BẮT BUỘC: Chỉ trả lời đúng câu từ chối ở trên, KHÔNG THÊM BỚT GÌ KHÁC!";
                }

                enhancedHistory.AddSystemMessage(enhancedSystemPrompt);
            }

            foreach (var message in cleanHistory.Where(m => m.Role != AuthorRole.System))
            {
                enhancedHistory.Add(message);
            }

            return enhancedHistory;
        }
        private string GetActualSourceDocumentTitle(string documentContent, List<DocumentInfo> documentSources)
        {
            // ✅ PRIORITY 1: Use title from sources (từ DocumentRAGService)
            if (documentSources?.Any() == true)
            {
                var firstSource = documentSources.First();
                if (!string.IsNullOrWhiteSpace(firstSource.Title))
                {
                    _logger.LogInformation("📄 [SOURCE-TITLE] Using title from DocumentRAGService: {Title}", firstSource.Title);
                    return FormatDocumentTitle(firstSource.Title);
                }
            }

            // ✅ PRIORITY 2: Extract from content headers (fallback)
            var titleMatch = ExtractTitleFromContentHeaders(documentContent);
            if (!string.IsNullOrEmpty(titleMatch))
            {
                _logger.LogInformation("📄 [CONTENT-TITLE] Extracted title from content: {Title}", titleMatch);
                return titleMatch;
            }

            // ✅ PRIORITY 3: Use highest relevance source if multiple
            if (documentSources?.Count > 1)
            {
                var bestSource = documentSources
                    .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                    .OrderByDescending(s => s.RelevanceScore)
                    .FirstOrDefault();

                if (bestSource != null)
                {
                    _logger.LogInformation("📄 [BEST-SOURCE] Using highest relevance source: {Title} (Score: {Score:F3})",
                        bestSource.Title, bestSource.RelevanceScore);
                    return FormatDocumentTitle(bestSource.Title);
                }
            }

            _logger.LogWarning("📄 [NO-TITLE] No title found in sources or content");
            return null;
        }
        private string FormatDocumentTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var formattedTitle = title.Trim();

            // Remove common prefixes
            var prefixesToRemove = new[] { "📄", "Document:", "Tài liệu:" };
            foreach (var prefix in prefixesToRemove)
            {
                if (formattedTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    formattedTitle = formattedTitle.Substring(prefix.Length).Trim();
                }
            }

            // Truncate if too long
            if (formattedTitle.Length > 80)
            {
                formattedTitle = formattedTitle.Substring(0, 77) + "...";
            }

            return formattedTitle;
        }
        private string ExtractTitleFromContentHeaders(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Take(10)) // Check first 10 lines
            {
                var trimmedLine = line.Trim();

                // Look for patterns like "📄 Title:" or "Document: Title"
                if (trimmedLine.StartsWith("📄") && trimmedLine.Contains(":"))
                {
                    var titlePart = trimmedLine.Substring(trimmedLine.IndexOf("📄") + 1);
                    if (titlePart.Contains(":"))
                    {
                        titlePart = titlePart.Substring(0, titlePart.IndexOf(":"));
                    }

                    var extractedTitle = titlePart.Trim();
                    if (extractedTitle.Length > 5 && extractedTitle.Length < 100)
                    {
                        return extractedTitle;
                    }
                }
            }

            return null;
        }
        private void LogDocumentContextUsage(bool hasDocumentContext)
        {
            if (hasDocumentContext)
            {
                _logger.LogInformation("📄 [CONTEXT] RAW document content injected into AI prompt");
            }
            else
            {
                _logger.LogInformation("🧠 [AI-ONLY] Using AI knowledge only (no document context)");
            }
        }

        #region Session Management (Unchanged)

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return await CreateNewSession(modelName, userId);

            return await GetExistingSession(sessionId, userId);
        }

        private async Task<ChatSession> CreateNewSession(string modelName, string userId)
        {
            var validModelName = await DetermineModelForNewSession(modelName, userId);

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

        private async Task<ChatSession> GetExistingSession(string sessionId, string userId)
        {
            var session = await GetSessionByIdAndUser(sessionId, userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            return session;
        }

        private async Task ValidateSessionModelConsistency(ChatSession session, string requestedModelName)
        {
            if (!string.IsNullOrEmpty(requestedModelName) &&
                !string.IsNullOrEmpty(session.Id) &&
                session.ModelName != requestedModelName)
            {
                throw new InvalidOperationException(
                    $"Không thể thay đổi model trong session đã có conversation. " +
                    $"Session hiện tại sử dụng {session.ModelName}. " +
                    $"Để sử dụng {requestedModelName}, vui lòng tạo session mới.");
            }
        }

        private async Task EnsureSessionModelIsActive(ChatSession session)
        {
            var isSessionModelActive = await IsModelActiveAsync(session.ModelName);
            if (!isSessionModelActive)
            {
                throw new InvalidOperationException(
                    $"Model '{session.ModelName}' đã bị tắt bởi admin. " +
                    $"Session này không thể tiếp tục. Vui lòng tạo session mới với model khác.");
            }
        }

        private async Task<bool> IsFirstUserMessageInSession(string sessionId)
        {
            var userMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId && m.Role == MessageRole.User);

            return userMessages.Count == 0;
        }

        #endregion

        #region Message Validation and Processing (Unchanged)

        private async Task ValidateMessageStrictAsync(string message)
        {
            var validation = await ValidateMessageAsync(message);
            if (!validation.Success)
                throw new ArgumentException(validation.Message);
        }

        public async Task<ApiResponse<object>> ValidateMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return ApiResponse<object>.Fail(MessageConstant.Chat.EmptyMessage);

            if (message.Length > ChatConstants.MaxMessageLength)
                return ApiResponse<object>.Fail(
                    string.Format(MessageConstant.Chat.MessageTooLong, ChatConstants.MaxMessageLength));

            var tokenCount = _tokenCountService.CountTokens(message);

            if (tokenCount > ChatConstants.MaxTokenLimit)
                return ApiResponse<object>.Fail(
                    string.Format(MessageConstant.Chat.TokenLimitExceeded, tokenCount, ChatConstants.MaxTokenLimit));

            var warningThreshold = (int)(ChatConstants.MaxTokenLimit * ChatConstants.TokenWarningThreshold);
            if (tokenCount > warningThreshold)
                return ApiResponse<object>.Ok(null,
                    string.Format(MessageConstant.Chat.TokenWarning, tokenCount, ChatConstants.MaxTokenLimit));

            return ApiResponse<object>.Ok(null, MessageConstant.Chat.MessageValid);
        }

        #endregion

        #region Chat History Management (Unchanged)

        private async Task<ChatHistory> BuildCleanChatHistoryAsync(string sessionId)
        {
            var messages = await GetSessionMessages(sessionId);
            return await BuildCleanChatHistoryFromMessages(sessionId, messages.ToList());
        }

        private async Task<ChatHistory> BuildCleanChatHistoryFromMessages(string sessionId, List<ChatMessage> messages)
        {
            var chatHistory = new ChatHistory();
            var baseSystemPrompt = await BuildBaseSystemPromptAsync(sessionId);
            chatHistory.AddSystemMessage(baseSystemPrompt);

            var recentMessages = messages.TakeLast(ChatConstants.MaxHistoryMessages).ToList();
            AddMessagesToHistory(chatHistory, recentMessages);

            return await EnsureTokenLimitCompliance(chatHistory, sessionId, baseSystemPrompt, messages);
        }

        private void AddMessagesToHistory(ChatHistory chatHistory, List<ChatMessage> messages)
        {
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
        }

        private async Task<ChatHistory> EnsureTokenLimitCompliance(ChatHistory chatHistory, string sessionId, string baseSystemPrompt, List<ChatMessage> allMessages)
        {
            var currentModelName = await GetCurrentModelNameAsync(sessionId);

            if (_tokenCountService.IsContextWithinLimit(chatHistory, currentModelName))
                return chatHistory;

            return await CreateReducedChatHistory(baseSystemPrompt, allMessages);
        }

        private async Task<ChatHistory> CreateReducedChatHistory(string baseSystemPrompt, List<ChatMessage> allMessages)
        {
            var reducedMessages = allMessages.TakeLast(ChatConstants.MinHistoryMessages).ToList();
            var reducedHistory = new ChatHistory();
            reducedHistory.AddSystemMessage(baseSystemPrompt);
            AddMessagesToHistory(reducedHistory, reducedMessages);

            return reducedHistory;
        }

        #endregion

        #region System Prompt Building (Unchanged)

        private async Task<string> BuildBaseSystemPromptAsync(string sessionId)
        {
            var session = await GetSessionByIdOnly(sessionId);
            var baseSystemPrompt = ChatConstants.SystemPrompt;

            if (session != null)
            {
                baseSystemPrompt = await GetAIConfigurationSystemPrompt(session.ModelName, baseSystemPrompt);
                baseSystemPrompt = await EnhanceWithUserPreferences(baseSystemPrompt, sessionId, session.UserId);
            }

            return baseSystemPrompt;
        }

        private async Task<string> GetAIConfigurationSystemPrompt(string modelName, string defaultPrompt)
        {
            var normalizedModelName = NormalizeModelName(modelName);
            var aiConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName && c.IsActive);

            if (aiConfig?.SystemPrompt != null)
            {
                return $"{defaultPrompt}\n\n--- Model Configuration ---\n{aiConfig.SystemPrompt}";
            }

            return defaultPrompt;
        }        

        private async Task<string> EnhanceWithUserPreferences(string basePrompt, string sessionId, string userId)
        {
            try
            {
                var preferences = await _preferenceService.GetEffectivePreferencesAsync(sessionId, userId);
                var enhancedPrompt = basePrompt;

                enhancedPrompt = AddUserNameToPrompt(enhancedPrompt, preferences.UserName);
                enhancedPrompt = AddCharacteristicsToPrompt(enhancedPrompt, preferences.ChatbotCharacteristics);
                enhancedPrompt = AddAdditionalInfoToPrompt(enhancedPrompt, preferences.AdditionalInfo);

                return enhancedPrompt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enhance prompt with user preferences for user {UserId}, session {SessionId}. Using base prompt.", userId, sessionId);
                return basePrompt;
            }
        }

        private string AddUserNameToPrompt(string prompt, string userName)
        {
            if (!string.IsNullOrEmpty(userName))
                prompt += $" {string.Format(ChatConstants.UserNamePromptTemplate, userName)}";

            return prompt;
        }

        private string AddCharacteristicsToPrompt(string prompt, List<string> characteristics)
        {
            if (characteristics.Any())
            {
                var limitedCharacteristics = characteristics
                    .Take(ChatConstants.MaxCharacteristics)
                    .Select(c => ChatbotCharacteristics.GetDisplayName(c))
                    .Where(name => !string.IsNullOrEmpty(name));

                if (limitedCharacteristics.Any())
                    prompt += $" {string.Format(ChatConstants.CharacteristicsPromptTemplate, string.Join(", ", limitedCharacteristics))}";
            }

            return prompt;
        }

        private string AddAdditionalInfoToPrompt(string prompt, string additionalInfo)
        {
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                var truncatedInfo = additionalInfo.Length > ChatConstants.MaxAdditionalInfoLength
                    ? additionalInfo.Substring(0, ChatConstants.MaxAdditionalInfoLength) + "..."
                    : additionalInfo;

                prompt += $" {string.Format(ChatConstants.AdditionalInfoPromptTemplate, truncatedInfo)}";
            }

            return prompt;
        }

        #endregion

        #region Message Creation and Saving (Unchanged)

        private (ChatMessage UserMessage, ChatMessage AiMessage) CreateChatMessages(string userContent, string aiContent, ChatSession session, string userId, List<DocumentInfo> documentSources = null)
        {
            var userMessage = new ChatMessage
            {
                Content = userContent,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(userContent, session.ModelName),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow,
                DocumentSources = "",
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            string sourcesString = null;
            if (documentSources?.Any() == true)
            {
                sourcesString = string.Join(";", documentSources.Select(doc =>
                 $"{doc.DocumentId ?? ""}|{doc.Title ?? ""}|{doc.RelevanceScore:F3}"));
            }
            var aiMessage = new ChatMessage
            {
                Content = aiContent,
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(aiContent, session.ModelName),
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow.AddMilliseconds(1), // Ensure order
                DocumentSources = sourcesString, // ✅ Lưu string đơn giản
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return (userMessage, aiMessage);
        }

        private async Task SaveMessagesAndUpdateSession(
    ChatMessage userMessage,
    ChatMessage aiMessage,
    ChatSession session,
    string userId,
    bool isFirstMessage,
    string firstUserMessage)
        {
            // ✅ Save user message first
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
            await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

            await UpdateSessionWithTitleGeneration(session, userId, isFirstMessage, firstUserMessage);

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();
        }

        private async Task UpdateSessionWithTitleGeneration(ChatSession session, string userId, bool isFirstMessage, string firstUserMessage)
        {
            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            if (isFirstMessage && ShouldGenerateNewTitle(session.Title))
            {
                await GenerateAndSetSessionTitle(session, firstUserMessage);
            }
        }

        private bool ShouldGenerateNewTitle(string currentTitle)
        {
            return string.IsNullOrEmpty(currentTitle) || currentTitle == ChatConstants.DefaultSessionTitle;
        }

        private async Task GenerateAndSetSessionTitle(ChatSession session, string firstUserMessage)
        {
            try
            {
                var newTitle = await _semanticKernelService.GenerateTitleAsync(firstUserMessage);
                if (!string.IsNullOrEmpty(newTitle))
                {
                    session.Title = newTitle;
                    _logger.LogInformation("Generated title for session {SessionId}: {Title}", session.Id, newTitle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Title generation failed for session {SessionId}, keeping default", session.Id);
                session.Title ??= ChatConstants.DefaultSessionTitle;
            }
        }

        private async Task ValidateAIResponse(string aiResponse, string sessionId)
        {
            if (string.IsNullOrEmpty(aiResponse))
            {
                _logger.LogError("AI service returned empty response for session {SessionId}", sessionId);
                throw new InvalidOperationException(MessageConstant.AI.ResponseGenerationFailed);
            }

            _logger.LogInformation("AI response generated successfully, length: {Length}", aiResponse.Length);
        }

        #endregion

        #region Response Creation (Unchanged)

        private ChatResponse CreateChatResponse(ChatSession session, string aiResponse, ChatMessage aiMessage, List<DocumentInfo> documentSources, bool hasDocumentContext)
        {
            return new ChatResponse
            {
                SessionId = session.Id,
                Message = aiResponse,
                Role = MessageRole.Assistant,
                TokenCount = aiMessage.TokenCount,
                Timestamp = aiMessage.Timestamp,
                ModelUsed = session.ModelName,
                DocumentSources = documentSources.Any() ? documentSources : null,
                HasDocumentContext = hasDocumentContext
            };
        }

        #endregion

        #region Database Operations (Unchanged)

        private async Task<ChatSession> GetSessionByIdAndUser(string sessionId, string userId)
        {
            return await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId && s.UserId == userId);
        }

        private async Task<ChatSession> GetSessionByIdOnly(string sessionId)
        {
            return await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
        }

        private async Task<List<ChatMessage>> GetSessionMessages(string sessionId)
        {
            var result = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(
                    predicate: m => m.SessionId == sessionId,
                    orderBy: q => q.OrderBy(m => m.CreatedAt));

            return result.ToList();
        }

        #endregion

        #region Model Validation (Unchanged)

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
            // Priority 1: Look for IsDefault = true model
            var defaultModel = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive && c.IsDefault);

            if (defaultModel != null)
            {
                _logger.LogInformation("Using default model: {ModelName}", defaultModel.ModelName);
                return defaultModel.ModelName;
            }

            // Priority 2: Fallback to any active model (shouldn't happen if admin sets default properly)
            var anyActiveModel = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            if (anyActiveModel != null)
            {
                _logger.LogWarning("No default model set, using first active model: {ModelName}", anyActiveModel.ModelName);
                return anyActiveModel.ModelName;
            }

            // Priority 3: Emergency fallback
            _logger.LogError("No active models found in system");
            throw new InvalidOperationException("Không có model nào được kích hoạt. Vui lòng liên hệ quản trị viên.");
        }
        /// <summary>
        /// ✅ UPDATED: Enhanced model selection for new sessions
        /// </summary>
        private async Task<string> DetermineModelForNewSession(string requestedModelName, string userId)
        {
            // Priority 1: Explicitly requested model
            if (!string.IsNullOrEmpty(requestedModelName))
            {
                var isValid = await IsModelActiveAsync(requestedModelName);
                if (isValid)
                {
                    _logger.LogInformation("Using requested model: {ModelName} for user {UserId}", requestedModelName, userId);
                    return requestedModelName;
                }

                var availableModels = await GetAvailableModelNamesAsync();
                throw new ArgumentException(
                    $"Model '{requestedModelName}' không khả dụng. " +
                    $"Models có sẵn: {string.Join(", ", availableModels)}");
            }

            // Priority 2: User's preferred model (if PreferenceService exists)
            // TODO: Implement when PreferenceService is ready
            // var userPreferredModel = await _preferenceService.GetUserPreferredModelAsync(userId);
            // if (!string.IsNullOrEmpty(userPreferredModel))...

            // Priority 3: System default model
            var defaultModel = await GetDefaultActiveModelAsync();
            _logger.LogInformation("Using default model: {ModelName} for user {UserId}", defaultModel, userId);
            return defaultModel;
        }


        private async Task<List<string>> GetAvailableModelNamesAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.Select(m => m.ModelName).ToList();
        }

        public async Task<List<AvailableModelResponse>> GetAvailableModelsAsync()
        {
            try
            {
                var activeConfigs = await GetActiveAIConfigurations();

                if (!activeConfigs.Any())
                {
                    _logger.LogError("No active models found in database");
                    throw new InvalidOperationException("Không có model nào được kích hoạt. Vui lòng liên hệ quản trị viên.");
                }

                return CreateAvailableModelResponses(activeConfigs);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không có model nào"))
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available models");
                throw;
            }
        }
        private async Task<List<AIConfiguration>> GetActiveAIConfigurations()
        {
            var result = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(
                    predicate: c => c.IsActive,
                    orderBy: q => q.OrderByDescending(c => c.IsDefault).ThenBy(c => c.DisplayName)); // ✅ Order default first

            return result.ToList();
        }

        private List<AvailableModelResponse> CreateAvailableModelResponses(List<AIConfiguration> activeConfigs)
        {
            var defaultModel = activeConfigs.First();

            return activeConfigs.Select(c => new AvailableModelResponse
            {
                ModelName = c.ModelName,
                DisplayName = c.DisplayName,
                MaxTokens = c.MaxTokens,
                IsDefault = c.Id == defaultModel.Id,
                IsFree = c.IsFree,
                Temperature = c.Temperature,
                TopP = c.TopP
            }).ToList();
        }

        private async Task<string> GetCurrentModelNameAsync(string sessionId)
        {
            var session = await GetSessionByIdOnly(sessionId);
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

        #endregion

        #region Streaming Operations (Unchanged)
private async IAsyncEnumerable<ChatStreamResponse> WrapStreamWithChatResponse(
    IAsyncEnumerable<string> stream,
    ChatSession session,
    string userId,
    string userMessageContent,
    bool isFirstMessage,
    List<DocumentInfo> documentSources,
    bool hasDocumentContext,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🔵 [STREAM-START] Session: {SessionId}, User: {UserId}, IsFirstMessage: {IsFirst}",
                session.Id, userId, isFirstMessage);

            var fullResponse = new StringBuilder();
            var timestamp = DateTime.UtcNow;
            var tokenCount = 0;

            // Stream tokens
            await foreach (var token in stream.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("🟡 [STREAM-CANCELLED] Session: {SessionId}", session.Id);
                    yield break;
                }

                if (string.IsNullOrEmpty(token))
                    continue;

                tokenCount++;
                fullResponse.Append(token);
                var currentFullMessage = fullResponse.ToString();

                yield return new ChatStreamResponse
                {
                    SessionId = session.Id,
                    Message = currentFullMessage,
                    MessageChunk = token,
                    Role = MessageRole.Assistant,
                    Timestamp = timestamp,
                    ModelUsed = session.ModelName,
                    DocumentSources = null,
                    HasDocumentContext = hasDocumentContext,
                    IsComplete = false
                };
            }

            _logger.LogInformation("🔵 [STREAM-COMPLETE] Session: {SessionId}, Tokens: {TokenCount}, ResponseLength: {Length}",
                session.Id, tokenCount, fullResponse.Length);

            // Prepare final response data TRƯỚC
            var finalContent = fullResponse.ToString();
            var finalTokenCount = _tokenCountService.CountTokens(finalContent, session.ModelName);

            var cleanDocumentSources = documentSources?.Select(doc => new DocumentInfo
            {
                DocumentId = doc.DocumentId,
                Title = doc.Title,
                RelevanceScore = doc.RelevanceScore,
                Summary = "",
                VersionId = doc.VersionId,
                VersionName = doc.VersionName,
                DepartmentId = "",
                Description = null,
                Tags = null,
                EffectiveFrom = doc.EffectiveFrom,
                EffectiveUntil = doc.EffectiveUntil,
                ApprovalDate = null
            }).ToList();

            // ✅ FIX 1: Save TRƯỚC KHI yield return cuối cùng
            _logger.LogInformation("🟢 [SAVE-BEFORE-FINAL] Starting save for session: {SessionId}", session.Id);

            // Option A: Save đồng bộ (đơn giản, an toàn)
            try
            {
                await SaveStreamingChatData(finalContent, session.Id, userId, userMessageContent,
                    isFirstMessage, session.ModelName, documentSources);
                _logger.LogInformation("✅ [SAVE-SUCCESS] Data saved for session: {SessionId}", session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔴 [SAVE-ERROR] Failed to save for session {SessionId}", session.Id);
                // Vẫn tiếp tục return response cho user
            }

            // Option B: Nếu muốn dùng Task.Run, phải chờ nó start
            // var saveTask = Task.Run(async () =>
            // {
            //     _logger.LogInformation("🟢 [SAVE-TASK-START] Session: {SessionId}", session.Id);
            //     try
            //     {
            //         await SaveStreamingChatData(finalContent, session.Id, userId, userMessageContent,
            //             isFirstMessage, session.ModelName, documentSources);
            //         _logger.LogInformation("✅ [SAVE-TASK-SUCCESS] Session: {SessionId}", session.Id);
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogError(ex, "🔴 [SAVE-TASK-ERROR] Session {SessionId}", session.Id);
            //     }
            // });
            // 
            // // Chờ task start (không chờ complete)
            // await Task.Delay(10); // Đảm bảo task đã start

            // NOW yield final response
            _logger.LogInformation("🔵 [STREAM-FINAL-SENDING] Sending final response for session: {SessionId}", session.Id);

            yield return new ChatStreamResponse
            {
                SessionId = session.Id,
                Message = finalContent,
                MessageChunk = "",
                Role = MessageRole.Assistant,
                Timestamp = timestamp,
                ModelUsed = session.ModelName,
                DocumentSources = cleanDocumentSources,
                HasDocumentContext = hasDocumentContext,
                IsComplete = true,
                TotalTokenCount = finalTokenCount
            };

            _logger.LogInformation("🔵 [STREAM-METHOD-END] Session: {SessionId}", session.Id);
        }

        private async Task SaveStreamingChatData(string fullResponse, string sessionId, string userId, string userMessageContent, bool isFirstMessage, string modelName, List<DocumentInfo> documentSources = null)
        {
            _logger.LogInformation("📝 [SAVE-START] Session: {SessionId}, Creating messages...", sessionId);

            try
            {
                var (userMessage, aiMessage) = CreateStreamingChatMessages(userMessageContent, fullResponse, sessionId, userId, modelName, documentSources);

                await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(userMessage);
                await _unitOfWork.GetRepository<ChatMessage>().InsertAsync(aiMessage);

                // ✅ FIX: Check if session is already tracked
                var trackedSession = _unitOfWork.Context.ChangeTracker
                    .Entries<ChatSession>()
                    .FirstOrDefault(e => e.Entity.Id == sessionId);

                if (trackedSession != null)
                {
                    _logger.LogInformation("📝 [SAVE-SESSION-TRACKED] Using tracked session");
                    var session = trackedSession.Entity;
                    session.LastActiveAt = DateTime.UtcNow;
                    session.UpdatedBy = userId;

                    if (isFirstMessage && ShouldGenerateNewTitle(session.Title))
                    {
                        await GenerateAndSetSessionTitle(session, userMessageContent);
                    }
                    // Không cần UpdateAsync vì đã tracked
                }
                else
                {
                    _logger.LogInformation("📝 [SAVE-SESSION-NOT-TRACKED] Loading session");
                    var session = await GetSessionByIdOnly(sessionId);
                    if (session != null)
                    {
                        session.LastActiveAt = DateTime.UtcNow;
                        session.UpdatedBy = userId;

                        if (isFirstMessage && ShouldGenerateNewTitle(session.Title))
                        {
                            await GenerateAndSetSessionTitle(session, userMessageContent);
                        }

                        _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                    }
                }

                var changes = await _unitOfWork.CommitAsync();
                _logger.LogInformation("✅ [SAVE-COMMIT-SUCCESS] Session: {SessionId}, Changes: {Changes}", sessionId, changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔴 [SAVE-ERROR] Failed to save for session {SessionId}", sessionId);
                throw;
            }
        }

        private (ChatMessage UserMessage, ChatMessage AiMessage) CreateStreamingChatMessages(string userContent, string aiContent, string sessionId, string userId, string modelName, List<DocumentInfo> documentSources = null)
        {
            var userMessage = new ChatMessage
            {
                Content = userContent,
                Role = MessageRole.User,
                TokenCount = _tokenCountService.CountTokens(userContent, modelName),
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow.AddMilliseconds(-1),
                DocumentSources = "",
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            string sourcesString = null;
            if (documentSources?.Any() == true)
            {
                sourcesString = string.Join(";", documentSources.Select(doc =>
                    $"{doc.DocumentId ?? ""}|{doc.Title ?? ""}"));
            }
            var aiMessage = new ChatMessage
            {
                Content = aiContent,
                Role = MessageRole.Assistant,
                TokenCount = _tokenCountService.CountTokens(aiContent, modelName),
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                DocumentSources = sourcesString,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return (userMessage, aiMessage);
        }

        private async Task UpdateStreamingSession(string sessionId, string userId, bool isFirstMessage, string userMessageContent)
        {
            _logger.LogInformation("🔄 [UPDATE-SESSION-START] SessionId: {SessionId}, IsFirstMessage: {IsFirst}",
                sessionId, isFirstMessage);

            // Check tracked entities BEFORE loading
            var trackedSession = _unitOfWork.Context.ChangeTracker
                .Entries<ChatSession>()
                .FirstOrDefault(e => e.Entity.Id == sessionId);

            if (trackedSession != null)
            {
                _logger.LogWarning("⚠️ [UPDATE-SESSION-TRACKED] Session {SessionId} already tracked with state: {State}",
                    sessionId, trackedSession.State);
            }

            var session = await GetSessionByIdOnly(sessionId);
            if (session == null)
            {
                _logger.LogWarning("⚠️ [UPDATE-SESSION-NULL] Session {SessionId} not found", sessionId);
                return;
            }

            _logger.LogInformation("🔄 [UPDATE-SESSION-FOUND] Session: {SessionId}, Title: {Title}",
                sessionId, session.Title);

            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            if (isFirstMessage && ShouldGenerateNewTitle(session.Title))
            {
                _logger.LogInformation("🔄 [UPDATE-SESSION-TITLE] Generating title for session: {SessionId}", sessionId);
                await GenerateAndSetSessionTitle(session, userMessageContent);
            }

            _logger.LogInformation("🔄 [UPDATE-SESSION-CALL] Calling UpdateAsync for session: {SessionId}", sessionId);
            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);

            _logger.LogInformation("🔄 [UPDATE-SESSION-END] Session update queued: {SessionId}", sessionId);
        }
        public async Task<bool> VerifyMessagesInDatabase(string sessionId)
        {
            var messages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId);

            _logger.LogInformation("🔍 [DB-CHECK] Session {SessionId} has {Count} messages in database",
                sessionId, messages.Count);

            foreach (var msg in messages)
            {
                _logger.LogInformation("🔍 [DB-MESSAGE] Id: {Id}, Role: {Role}, Created: {Created}, Content: {Content}",
                    msg.Id, msg.Role, msg.CreatedAt, msg.Content.Substring(0, Math.Min(50, msg.Content.Length)));
            }

            return messages.Any();
        }
        #endregion

        #region Utility Methods (Unchanged)

        private string NormalizeModelName(string modelName) =>
            Uri.UnescapeDataString(modelName ?? string.Empty).Trim().ToLowerInvariant();

        #endregion

        #region Additional Public Methods (Keep existing implementation)

        public async Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string userId)
        {
            var modelName = await DetermineValidModelName(request.ModelName);

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
            var session = await GetSessionWithDetails(sessionId, userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            var response = _mapper.Map<SessionDetailResponse>(session);
            response.Messages = response.Messages.OrderBy(m => m.Timestamp).ToList();
            response.IsModelActive = await IsModelActiveAsync(session.ModelName);

            return response;
        }

        public async Task<List<SessionResponse>> GetUserSessionsAsync(string userId)
        {
            var sessions = await GetUserActiveSessions(userId);
            var responses = _mapper.Map<List<SessionResponse>>(sessions);

            await EnrichSessionResponses(responses, sessions);

            return responses;
        }

        public async Task<bool> DeleteSessionAsync(string sessionId, string userId)
        {
            var session = await GetSessionByIdAndUser(sessionId, userId);

            if (session == null)
                return false;

            await SoftDeleteSession(session, userId);

            _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);
            return true;
        }

        public async Task<bool> SwitchSessionModelAsync(string sessionId, string newModelName, string userId)
        {
            var session = await GetSessionByIdAndUser(sessionId, userId);

            if (session == null)
                throw new ArgumentException(MessageConstant.Chat.SessionNotFound);

            await ValidateSessionIsEmptyForModelSwitch(sessionId);
            await ValidateNewModelIsActive(newModelName);

            if (session.ModelName == newModelName)
                return true; // Already using this model

            await UpdateSessionModel(session, newModelName, userId);

            _logger.LogInformation("Switched model for empty session {SessionId} to {ModelName}", sessionId, newModelName);
            return true;
        }

        private async Task<string> DetermineValidModelName(string requestedModelName)
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

        private async Task<ChatSession> GetSessionWithDetails(string sessionId, string userId)
        {
            return await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(
                    predicate: s => s.Id == sessionId && s.UserId == userId,
                    include: q => q.Include(a => a.Messages)
                                   .Include(a => a.Preferences));
        }

        private async Task<List<ChatSession>> GetUserActiveSessions(string userId)
        {
            var result = await _unitOfWork.GetRepository<ChatSession>()
                 .GetListAsync(
                     predicate: s => s.UserId == userId && s.IsActive,
                     orderBy: q => q.OrderByDescending(s => s.LastActiveAt),
                     include: query => query.Include(s => s.Messages));

            return result.ToList();
        }

        private async Task EnrichSessionResponses(List<SessionResponse> responses, List<ChatSession> sessions)
        {
            foreach (var response in responses)
            {
                var session = sessions.First(s => s.Id == response.Id);
                response.MessageCount = session.Messages.Count;
                response.IsModelActive = await IsModelActiveAsync(session.ModelName);
            }
        }

        private async Task SoftDeleteSession(ChatSession session, string userId)
        {
            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();
        }

        private async Task UpdateSessionModel(ChatSession session, string newModelName, string userId)
        {
            session.ModelName = newModelName;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();
        }

        private async Task ValidateSessionIsEmptyForModelSwitch(string sessionId)
        {
            var hasMessages = await _unitOfWork.GetRepository<ChatMessage>()
                .GetListAsync(predicate: m => m.SessionId == sessionId);

            if (hasMessages.Any())
            {
                throw new InvalidOperationException(
                    "Không thể thay đổi model trong session đã có conversation. " +
                    "Vui lòng tạo session mới để sử dụng model khác.");
            }
        }

        private async Task ValidateNewModelIsActive(string newModelName)
        {
            var isValidModel = await IsModelActiveAsync(newModelName);
            if (!isValidModel)
            {
                var availableModels = await GetAvailableModelNamesAsync();
                throw new ArgumentException(
                    $"Model '{newModelName}' không khả dụng. " +
                    $"Models có sẵn: {string.Join(", ", availableModels)}");
            }
        }

        #endregion
    }
}