using AutoMapper;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using System.Diagnostics;
using System.Text;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.Domain.Enum;
using System.Text.Json;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Request.AIClientService;

namespace ChatBox.API.Services.Implement
{
    public class ConversationOrchestrationService : IConversationOrchestrationService
    {
        private readonly IAiServiceClient _aiServiceClient;
        private readonly IDocumentServiceClient _documentServiceClient;
        private readonly IUserPreferenceService _userPreferenceService;
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<ConversationOrchestrationService> _logger;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConversationOrchestrationService(
            IAiServiceClient aiServiceClient,
            IDocumentServiceClient documentServiceClient,
            IUserPreferenceService userPreferenceService,
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            ILogger<ConversationOrchestrationService> logger,
            IMapper mapper,
            IConfiguration configuration)
        {
            _aiServiceClient = aiServiceClient;
            _documentServiceClient = documentServiceClient;
            _userPreferenceService = userPreferenceService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<ConversationResponse> ProcessMessageAsync(ProcessMessageRequest request)
        {
            var messageId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing message for user {UserId}, MessageId: {MessageId}", request.UserId, messageId);

                // 1. Intent Detection trước khi xử lý
                var intentResult = await DetectUserIntentAsync(request.Message, request.UserId);

                // 2. Load user preferences
                var userPreferences = await LoadUserPreferencesAsync(request.UserId);

                // 3. Build conversation context với token validation
                var conversationHistory = await BuildConversationHistoryAsync(request.SessionId, request.UserId);

                // 4. Execute RAG workflow với intent-aware processing
                var ragRequest = new RAGRequest
                {
                    Query = request.Message,
                    UserId = request.UserId,
                    ConversationHistory = conversationHistory,
                    UserPreferences = userPreferences,
                    AIModelId = request.AIModelId ?? _configuration["ChatService:DefaultAIModel"],
                    Temperature = request.Temperature ?? _configuration.GetValue<double>("ChatService:DefaultTemperature", 0.7),
                    MaxTokens = request.MaxTokens ?? _configuration.GetValue<int>("ChatService:DefaultMaxTokens", 2000),
                    MaxDocuments = _configuration.GetValue<int>("ChatService:DocSearchLimit", 5),
                    MinRelevance = _configuration.GetValue<double>("ChatService:DocMinRelevance", 0.7),
                    DetectedIntent = intentResult.PredictedIntent
                };

                var ragResponse = await ExecuteRAGWorkflowAsync(ragRequest);

                if (!ragResponse.Success)
                {
                    return new ConversationResponse
                    {
                        Success = false,
                        Message = ragResponse.ErrorMessage ?? _configuration["ChatService:Messages:ProcessingError"],
                        MessageId = messageId,
                        Timestamp = DateTime.UtcNow,
                        ProcessingTime = stopwatch.Elapsed
                    };
                }

                // 5. Save message to database với AI model info
                await SaveMessageToSessionAsync(request.SessionId, request.Message, ragResponse.GeneratedResponse,
                    ragResponse.SourceDocuments, messageId, ragResponse.TokensUsed, request.AIModelId, intentResult.PredictedIntent);

                stopwatch.Stop();

                return new ConversationResponse
                {
                    Success = true,
                    Message = _configuration["ChatService:Messages:ProcessingSuccess"] ?? "Message processed successfully",
                    Response = ragResponse.GeneratedResponse,
                    MessageId = messageId,
                    SessionId = request.SessionId,
                    DocumentReferences = ragResponse.SourceDocuments,
                    SuggestedQuestions = await GenerateSuggestedQuestionsAsync(ragResponse.GeneratedResponse, request.Message, intentResult.PredictedIntent),
                    Metadata = BuildResponseMetadata(ragResponse, intentResult, request.AIModelId),
                    Timestamp = DateTime.UtcNow,
                    TokensUsed = ragResponse.TokensUsed,
                    ProcessingTime = stopwatch.Elapsed,
                    AIModelUsed = request.AIModelId ?? ragRequest.AIModelId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for user {UserId}, MessageId: {MessageId}", request.UserId, messageId);

                return new ConversationResponse
                {
                    Success = false,
                    Message = _configuration["ChatService:Messages:UnexpectedError"] ?? "An unexpected error occurred",
                    MessageId = messageId,
                    Timestamp = DateTime.UtcNow,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<RAGResponse> ExecuteRAGWorkflowAsync(RAGRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Executing RAG workflow for user {UserId}, Model: {AIModel}, Intent: {Intent}",
                    request.UserId, request.AIModelId, request.DetectedIntent);

                // 1. Token validation trước khi xử lý
                var isWithinTokenLimit = await ValidateTokenLimitAsync(request.Query, request.MaxTokens);
                if (!isWithinTokenLimit)
                {
                    return new RAGResponse
                    {
                        Success = false,
                        ErrorMessage = _configuration["ChatService:Messages:TokenLimitExceeded"] ?? "Query too long for processing"
                    };
                }

                // 2. Document Search Phase (chỉ khi intent cần document)
                List<AccessibleDocument> accessibleDocuments = new();
                if (RequiresDocumentSearch(request.DetectedIntent))
                {
                    var documentSearchResult = await SearchRelevantDocumentsAsync(request);
                    if (!documentSearchResult.Documents.Any())
                    {
                        return await HandleNoDocumentsFoundAsync(request);
                    }

                    // 3. Access Control Validation
                    accessibleDocuments = await ValidateDocumentAccessAsync(documentSearchResult.Documents, request.UserId);
                    if (!accessibleDocuments.Any())
                    {
                        return await HandleNoAccessibleDocumentsAsync(request);
                    }
                }

                // 4. Context Building với token optimization
                var contextData = await BuildOptimizedContextAsync(accessibleDocuments, request);

                // 5. AI Generation với full configuration
                var aiGenerationResult = await GenerateAIResponseWithFullConfigAsync(request, contextData);

                return new RAGResponse
                {
                    Success = true,
                    GeneratedResponse = aiGenerationResult.Response,
                    SourceDocuments = accessibleDocuments.Select(MapDocumentToReference).ToList(),
                    Context = contextData,
                    TokensUsed = aiGenerationResult.TokensUsed,
                    Model = aiGenerationResult.Model,
                    ConfidenceScore = aiGenerationResult.ConfidenceScore,
                    Metadata = BuildRAGMetadata(accessibleDocuments, request, aiGenerationResult)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RAG workflow for user {UserId}", request.UserId);

                return new RAGResponse
                {
                    Success = false,
                    ErrorMessage = _configuration["ChatService:Messages:RAGWorkflowError"] ?? "RAG workflow failed",
                    Metadata = new Dictionary<string, object> { { "Error", ex.Message } }
                };
            }
        }
        private async Task<IntentDetectionResult> DetectUserIntentAsync(string message, Guid userId)
        {
            try
            {
                var intentRequest = new IntentDetectionRequest
                {
                    Text = message,
                    UserId = userId,
                    PossibleIntents = _configuration.GetSection("ChatService:PossibleIntents").Get<List<string>>() ??
                        new List<string> { "question", "document_search", "help_request", "greeting", "general" },
                    Context = null
                };

                return await _aiServiceClient.DetectIntentAsync(intentRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting intent for user {UserId}, using fallback", userId);
                return new IntentDetectionResult
                {
                    PredictedIntent = "general",
                    Confidence = 0.5
                };
            }
        }
        private async Task<bool> ValidateTokenLimitAsync(string query, int maxTokens)
        {
            try
            {
                var tokenCount = await _aiServiceClient.CountTokensAsync(query);
                var inputTokenLimit = _configuration.GetValue<int>("ChatService:MaxInputTokens", 3000);
                return tokenCount <= Math.Min(maxTokens / 2, inputTokenLimit); // Reserve half for response
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating token limit, allowing request");
                return true; // Fail open
            }
        }
        private bool RequiresDocumentSearch(string intent)
        {
            var documentSearchIntents = _configuration.GetSection("ChatService:DocumentSearchIntents").Get<List<string>>() ??
                new List<string> { "document_search", "question", "general" };

            return documentSearchIntents.Contains(intent);
        }
        private async Task<Dictionary<string, object>> LoadUserPreferencesAsync(Guid userId)
        {
            try
            {
                var preferences = await _userPreferenceService.GetPreferenceAsync(userId);
                return new Dictionary<string, object>
                {
                    { "Language", preferences?.Language ?? _configuration["ChatService:DefaultPreferences:Language"] },
                    { "ResponseStyle", preferences?.ResponseStyle ?? _configuration["ChatService:DefaultPreferences:ResponseStyle"] },
                    { "Tone", preferences?.Tone ?? _configuration["ChatService:DefaultPreferences:Tone"] },
                    { "MaxResponseLength", preferences?.MaxResponseLength ?? _configuration.GetValue<int>("ChatService:DefaultPreferences:MaxResponseLength", 500) },
                    { "IncludeCitations", preferences?.IncludeCitations ?? _configuration.GetValue<bool>("ChatService:DefaultPreferences:IncludeCitations", true) }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading preferences for user {UserId}, using defaults", userId);
                return GetDefaultUserPreferences();
            }
        }

        private Dictionary<string, object> GetDefaultUserPreferences()
        {
            return new Dictionary<string, object>
            {
                { "Language", _configuration["ChatService:DefaultPreferences:Language"] ?? "vi" },
                { "ResponseStyle", _configuration["ChatService:DefaultPreferences:ResponseStyle"] ?? "balanced" },
                { "Tone", _configuration["ChatService:DefaultPreferences:Tone"] ?? "professional" },
                { "MaxResponseLength", _configuration.GetValue<int>("ChatService:DefaultPreferences:MaxResponseLength", 500) },
                { "IncludeCitations", _configuration.GetValue<bool>("ChatService:DefaultPreferences:IncludeCitations", true) }
            };
        }
        private async Task<List<string>> BuildConversationHistoryAsync(Guid sessionId, Guid userId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var contextWindowSize = _configuration.GetValue<int>("ChatService:ContextWindowSize", 10);

                var recentMessages = await messageRepo.GetListAsync(
                    predicate: m => m.SessionId == sessionId && m.UserId == userId && !m.IsDeleted,
                    orderBy: m => m.OrderByDescending(x => x.CreatedAt));

                var history = new List<string>();
                foreach (var message in recentMessages.Take(contextWindowSize).Reverse())
                {
                    history.Add($"User: {message.Content}");
                    if (!string.IsNullOrEmpty(message.AiResponse))
                    {
                        history.Add($"Assistant: {message.AiResponse}");
                    }
                }

                // Validate total token count của conversation history
                var historyText = string.Join("\n", history);
                var historyTokens = await _aiServiceClient.CountTokensAsync(historyText);
                var maxHistoryTokens = _configuration.GetValue<int>("ChatService:MaxHistoryTokens", 1000);

                if (historyTokens > maxHistoryTokens)
                {
                    var truncatedHistory = await _aiServiceClient.TruncateToTokenLimitAsync(historyText, maxHistoryTokens);
                    return truncatedHistory.Split('\n').ToList();
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error building conversation history for session {SessionId}", sessionId);
                return new List<string>();
            }
        }
        private async Task<DocumentSearchResult> SearchRelevantDocumentsAsync(RAGRequest request)
        {
            var searchRequest = new DocumentSearchRequest
            {
                Query = request.Query,
                UserId = request.UserId,
                MaxResults = request.MaxDocuments,
                IncludeContent = true,
                FilterByAccess = true
            };

            var searchResponse = await _documentServiceClient.SearchDocumentsAsync(searchRequest);

            return new DocumentSearchResult
            {
                Documents = searchResponse.Documents?.Select(d => new AccessibleDocument
                {
                    Id = d.Id,
                    Title = d.Title,
                    Content = d.Content,
                    RelevanceScore = d.RelevanceScore,
                    LastModified = d.LastModified,
                    DocumentType = d.Type
                }).ToList() ?? new List<AccessibleDocument>()
            };
        }

        private async Task<List<AccessibleDocument>> ValidateDocumentAccessAsync(List<AccessibleDocument> documents, Guid userId)
        {
            try
            {
                var documentIds = documents.Select(d => d.Id).ToList();
                var batchAccessRequest = new BatchDocumentRequest
                {
                    DocumentIds = documentIds,
                    UserId = userId
                };

                var accessResponse = await _documentServiceClient.CheckBatchAccessAsync(batchAccessRequest);
                return documents.Where(d => accessResponse.AccessibleDocuments.Contains(d.Id)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating document access for user {UserId}", userId);
                return new List<AccessibleDocument>();
            }
        }

        private async Task<string> BuildOptimizedContextAsync(List<AccessibleDocument> documents, RAGRequest request)
        {
            var contextBuilder = new StringBuilder();
            var maxContextLength = _configuration.GetValue<int>("ChatService:MaxContextLength", 8000);

            contextBuilder.AppendLine($"Query: {request.Query}");
            if (!string.IsNullOrEmpty(request.DetectedIntent))
            {
                contextBuilder.AppendLine($"Intent: {request.DetectedIntent}");
            }
            contextBuilder.AppendLine("Relevant Documents:");

            foreach (var doc in documents.OrderByDescending(d => d.RelevanceScore))
            {
                var docContext = $"Document: {doc.Title}\nContent: {doc.Content}\n---\n";
                if (contextBuilder.Length + docContext.Length > maxContextLength)
                    break;

                contextBuilder.Append(docContext);
            }

            var context = contextBuilder.ToString();

            // Sử dụng AI service để optimize context cho model cụ thể
            var contextTokens = await _aiServiceClient.CountTokensAsync(context, request.AIModelId);
            var maxContextTokens = GetModelTokenLimit(request.AIModelId) - _configuration.GetValue<int>("ChatService:ReservedTokensForResponse", 500);

            if (contextTokens > maxContextTokens)
            {
                context = await _aiServiceClient.TruncateToTokenLimitAsync(context, maxContextTokens);
            }

            return context;
        }

        private int GetModelTokenLimit(string modelId)
        {
            var modelLimits = _configuration.GetSection("ChatService:ModelTokenLimits").Get<Dictionary<string, int>>();
            return modelLimits?.GetValueOrDefault(modelId, 4000) ?? 4000;
        }

        private async Task<AiGenerationResult> GenerateAIResponseWithFullConfigAsync(RAGRequest request, string context)
        {
            var systemPrompt = _configuration["ChatService:SystemPrompt"];

            // Customize system prompt based on intent
            if (!string.IsNullOrEmpty(request.DetectedIntent))
            {
                var intentPrompts = _configuration.GetSection("ChatService:IntentPrompts").Get<Dictionary<string, string>>();
                if (intentPrompts?.ContainsKey(request.DetectedIntent) == true)
                {
                    systemPrompt += "\n" + intentPrompts[request.DetectedIntent];
                }
            }

            var aiRequest = new AdvancedAiGenerationRequest
            {
                Query = request.Query,
                Context = context,
                ConversationHistory = request.ConversationHistory,
                UserPreferences = request.UserPreferences,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                Model = request.AIModelId,
                SystemPrompt = systemPrompt,
                UserId = request.UserId
            };

            return await _aiServiceClient.GenerateResponseAsync(aiRequest);
        }

        private async Task<List<string>> GenerateSuggestedQuestionsAsync(string response, string originalQuery, string intent)
        {
            try
            {
                // Sử dụng AI service để generate suggestions thông minh hơn
                var suggestionRequest = new TitleSuggestionRequest
                {
                    Content = $"Original Question: {originalQuery}\nAI Response: {response}\nIntent: {intent}",
                    MaxLength = _configuration.GetValue<int>("ChatService:MaxSuggestionLength", 100),
                    Language = _configuration["ChatService:DefaultPreferences:Language"] ?? "vi",
                    Style = "question"
                };

                // Generate multiple suggestions
                var suggestions = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    var suggestion = await _aiServiceClient.SuggestTitleAsync(suggestionRequest);
                    if (!string.IsNullOrEmpty(suggestion) && !suggestions.Contains(suggestion))
                    {
                        suggestions.Add(suggestion);
                    }
                }

                return suggestions.Any() ? suggestions : GetFallbackSuggestions(response, intent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating AI suggestions, using fallback");
                return GetFallbackSuggestions(response, intent);
            }
        }

        private List<string> GetFallbackSuggestions(string response, string intent)
        {
            var suggestions = new List<string>();

            // Intent-based suggestions
            var intentSuggestions = _configuration.GetSection("ChatService:IntentSuggestions").Get<Dictionary<string, List<string>>>();
            if (intentSuggestions?.ContainsKey(intent) == true)
            {
                suggestions.AddRange(intentSuggestions[intent]);
            }

            // Content-based suggestions
            if (response.Contains("policy") || response.Contains("chính sách"))
            {
                suggestions.Add("Bạn có thể cho xem toàn bộ tài liệu chính sách này không?");
                suggestions.Add("Có ngoại lệ nào cho chính sách này không?");
            }

            if (response.Contains("process") || response.Contains("quy trình"))
            {
                suggestions.Add("Bạn có thể giải thích chi tiết hơn về quy trình này không?");
                suggestions.Add("Điều gì xảy ra nếu tôi bỏ qua một bước?");
            }

            if (response.Contains("requirement") || response.Contains("yêu cầu"))
            {
                suggestions.Add("Có yêu cầu thay thế nào khác không?");
                suggestions.Add("Thường thì việc này mất bao lâu?");
            }

            if (suggestions.Count == 0)
            {
                suggestions.AddRange(new[]
                {
                    "Bạn có thể cung cấp thêm chi tiết về điều này không?",
                    "Có chủ đề liên quan nào tôi nên biết không?",
                    "Tôi có thể tìm tài liệu chính thức ở đâu?"
                });
            }

            return suggestions.Take(3).ToList();
        }

        private async Task SaveMessageToSessionAsync(Guid sessionId, string userMessage, string aiResponse,
            List<DocumentReference> sourceDocuments, Guid messageId, int tokensUsed, string aiModelUsed, string detectedIntent)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var sessionRepo = _unitOfWork.GetRepository<ChatSession>();

                var message = new ChatMessage
                {
                    Id = messageId,
                    SessionId = sessionId,
                    UserId = await GetUserIdFromSessionAsync(sessionId),
                    Content = userMessage,
                    AiResponse = aiResponse,
                    MessageType = MessageType.Text,
                    TokensUsed = tokensUsed,
                    CreatedAt = DateTime.UtcNow,
                    SourceDocuments = string.Join(",", sourceDocuments.Select(d => d.DocumentId)),
                    Metadata = JsonSerializer.Serialize(new
                    {
                        AIModelUsed = aiModelUsed,
                        DetectedIntent = detectedIntent,
                        TokensUsed = tokensUsed,
                        DocumentCount = sourceDocuments.Count
                    }, _jsonOptions)
                };

                await messageRepo.InsertAsync(message);

                // Update session với auto-generated title
                var session = await sessionRepo.SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
                if (session != null)
                {
                    session.MessageCount++;
                    session.LastActivityAt = DateTime.UtcNow;
                    session.AIModelId = aiModelUsed;

                    // Auto-generate title cho session mới bằng AI
                    if (string.IsNullOrEmpty(session.Title) || session.Title == _configuration["ChatService:DefaultSessionTitle"])
                    {
                        session.Title = await GenerateSessionTitleWithAIAsync(userMessage);
                    }
                    sessionRepo.UpdateAsync(session);
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message to session {SessionId}", sessionId);
            }
        }

        private async Task<string> GenerateSessionTitleWithAIAsync(string firstMessage)
        {
            try
            {
                var titleRequest = new TitleSuggestionRequest
                {
                    Content = firstMessage,
                    MaxLength = _configuration.GetValue<int>("ChatService:MaxSessionTitleLength", 50),
                    Language = _configuration["ChatService:DefaultPreferences:Language"] ?? "vi",
                    Style = "concise"
                };

                var title = await _aiServiceClient.SuggestTitleAsync(titleRequest);
                return string.IsNullOrEmpty(title) ? _configuration["ChatService:DefaultSessionTitle"] : title;
            }
            catch
            {
                return _configuration["ChatService:DefaultSessionTitle"] ?? "Cuộc trò chuyện mới";
            }
        }

        private async Task<Guid> GetUserIdFromSessionAsync(Guid sessionId)
        {
            var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
            var session = await sessionRepo.SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
            return session?.UserId ?? Guid.Empty;
        }

        private Dictionary<string, object> BuildResponseMetadata(RAGResponse ragResponse, IntentDetectionResult intentResult, string aiModelUsed)
        {
            return new Dictionary<string, object>
            {
                { "DocumentsFound", ragResponse.SourceDocuments.Count },
                { "AIModelUsed", aiModelUsed },
                { "DetectedIntent", intentResult.PredictedIntent },
                { "IntentConfidence", intentResult.Confidence },
                { "RequiresClarification", intentResult.RequiresClarification },
                { "TokensUsed", ragResponse.TokensUsed },
                { "ConfidenceScore", ragResponse.ConfidenceScore }
            };
        }

        private Dictionary<string, object> BuildRAGMetadata(List<AccessibleDocument> documents, RAGRequest request, AiGenerationResult aiResult)
        {
            return new Dictionary<string, object>
            {
                { "DocumentsFound", documents.Count },
                { "AccessibleDocuments", documents.Count },
                { "AIModelUsed", request.AIModelId },
                { "Temperature", request.Temperature },
                { "MaxTokens", request.MaxTokens },
                { "DetectedIntent", request.DetectedIntent },
                { "ProcessingTime", aiResult.ProcessingTime.TotalMilliseconds }
            };
        }

        private async Task<RAGResponse> HandleNoDocumentsFoundAsync(RAGRequest request)
        {
            return new RAGResponse
            {
                Success = true,
                GeneratedResponse = _configuration["ChatService:Messages:NoDocumentsFound"] ??
                    "Tôi không thể tìm thấy tài liệu phù hợp cho câu hỏi của bạn. Bạn có thể thử diễn đạt lại câu hỏi hoặc liên hệ quản trị viên nếu tin rằng thông tin này có sẵn.",
                SourceDocuments = new List<DocumentReference>(),
                Metadata = new Dictionary<string, object> { { "Reason", "NoDocumentsFound" } }
            };
        }

        private async Task<RAGResponse> HandleNoAccessibleDocumentsAsync(RAGRequest request)
        {
            return new RAGResponse
            {
                Success = true,
                GeneratedResponse = _configuration["ChatService:Messages:NoAccessibleDocuments"] ??
                    "Tôi tìm thấy một số tài liệu liên quan, nhưng bạn không có quyền truy cập. Vui lòng liên hệ quản trị viên nếu bạn cần truy cập thông tin này.",
                SourceDocuments = new List<DocumentReference>(),
                Metadata = new Dictionary<string, object> { { "Reason", "NoAccessibleDocuments" } }
            };
        }

        private DocumentReference MapDocumentToReference(AccessibleDocument doc)
        {
            return new DocumentReference
            {
                DocumentId = doc.Id,
                Title = doc.Title,
                Excerpt = doc.Content?.Length > 200 ? doc.Content.Substring(0, 200) + "..." : doc.Content,
                Url = $"/documents/{doc.Id}",
                LastModified = doc.LastModified,
                RelevanceScore = doc.RelevanceScore,
                DocumentType = doc.DocumentType
            };
        }
    }

    // Supporting classes
    public class DocumentSearchResult
    {
        public List<AccessibleDocument> Documents { get; set; } = new();
    }

    public class AccessibleDocument
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public double RelevanceScore { get; set; }
        public DateTime LastModified { get; set; }
        public string DocumentType { get; set; }
    }

}

        // Private helper methods

       