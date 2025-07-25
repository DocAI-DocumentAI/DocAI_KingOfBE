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

namespace ChatBox.API.Services.Implement
{
    public class ConversationOrchestrationService : IConversationOrchestrationService
    {
        private readonly IAiServiceClient _aiServiceClient;
        private readonly IDocumentServiceClient _documentServiceClient;
        private readonly IUserPreferenceService _userPreferenceService;
        private readonly ISecurityService _securityService;
        private readonly IContentModerationService _contentModerationService;
        private readonly IAuditService _auditService;
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<ConversationOrchestrationService> _logger;
        private readonly IMapper _mapper;

      public ConversationOrchestrationService(
            IAiServiceClient aiServiceClient,
            IDocumentServiceClient documentServiceClient,
            IUserPreferenceService userPreferenceService,
            ISecurityService securityService,
            IContentModerationService contentModerationService,
            IAuditService auditService,
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            ILogger<ConversationOrchestrationService> logger,
            IMapper mapper)
        {
            _aiServiceClient = aiServiceClient;
            _documentServiceClient = documentServiceClient;
            _userPreferenceService = userPreferenceService;
            _securityService = securityService;
            _contentModerationService = contentModerationService;
            _auditService = auditService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<ConversationResponse> ProcessMessageAsync(ProcessMessageRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var messageId = Guid.NewGuid();

            try
            {
                _logger.LogInformation("Processing message for user {UserId}, MessageId: {MessageId}",
                    request.UserId, messageId);

                // 1. Validate and moderate content
                var moderationResult = await ValidateAndModerateContentAsync(request.Message, request.UserId);
                if (!moderationResult.IsValid)
                {
                    return CreateErrorResponse(moderationResult.ErrorMessage, messageId, stopwatch.Elapsed);
                }

                // 2. Get or create session
                var session = await GetOrCreateSessionAsync(request.UserId, request.SessionId);

                // 3. Load user preferences
                var userPreferences = await LoadUserPreferencesAsync(request.UserId);

                // 4. Build conversation context
                var conversationHistory = await BuildConversationHistoryAsync(session.Id, request.UserId);

                // 5. Execute RAG workflow
                var ragRequest = new RAGRequest
                {
                    Query = request.Message,
                    UserId = request.UserId,
                    ConversationHistory = conversationHistory,
                    UserPreferences = userPreferences,
                    MaxDocuments = GetMaxDocumentsFromPreferences(userPreferences),
                    MaxTokens = GetMaxTokensFromPreferences(userPreferences)
                };

                var ragResponse = await ExecuteRAGWorkflowAsync(ragRequest);

                if (!ragResponse.Success)
                {
                    return CreateErrorResponse("Failed to generate response", messageId, stopwatch.Elapsed);
                }

                // 6. Enhance response with smart features
                var enhancedResponse = await EnhanceResponseAsync(ragResponse, request.Message, session.Id);

                // 7. Save message to database
                await SaveMessageToSessionAsync(session.Id, request.Message, enhancedResponse.GeneratedResponse,
                    ragResponse.SourceDocuments, messageId, ragResponse.TokensUsed);

                // 8. Log audit trail
                await _auditService.LogAsync(request.UserId, "ProcessMessage", "ChatMessage", messageId.ToString(),
                    null, new { Message = request.Message, Response = enhancedResponse.GeneratedResponse },
                    request.IpAddress, request.UserAgent);

                stopwatch.Stop();

                _logger.LogInformation("Message processed successfully for user {UserId}, MessageId: {MessageId}, Duration: {Duration}ms",
                    request.UserId, messageId, stopwatch.ElapsedMilliseconds);

                return new ConversationResponse
                {
                    Success = true,
                    Message = "Message processed successfully",
                    Response = enhancedResponse.GeneratedResponse,
                    MessageId = messageId,
                    SessionId = session.Id,
                    DocumentReferences = ragResponse.SourceDocuments,
                    SuggestedQuestions = enhancedResponse.SuggestedQuestions,
                    Metadata = enhancedResponse.Metadata,
                    Timestamp = DateTime.UtcNow,
                    TokensUsed = ragResponse.TokensUsed,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for user {UserId}, MessageId: {MessageId}",
                    request.UserId, messageId);

                await _auditService.LogSecurityEventAsync(request.UserId, "ProcessMessageError",
                    $"Error processing message: {ex.Message}", "high", request.IpAddress);

                return CreateErrorResponse("An error occurred while processing your message", messageId, stopwatch.Elapsed);
            }
        }

        public async Task<RAGResponse> ExecuteRAGWorkflowAsync(RAGRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Executing RAG workflow for user {UserId}, Query: {Query}",
                    request.UserId, request.Query);

                // 1. Document Search Phase
                var documentSearchResult = await SearchRelevantDocumentsAsync(request);
                if (documentSearchResult.Documents.Count == 0)
                {
                    _logger.LogWarning("No relevant documents found for query: {Query}", request.Query);
                    return await HandleNoDocumentsFoundAsync(request);
                }

                // 2. Access Control Validation
                var accessibleDocuments = await ValidateDocumentAccessAsync(documentSearchResult.Documents, request.UserId);
                if (accessibleDocuments.Count == 0)
                {
                    _logger.LogWarning("User {UserId} has no access to found documents", request.UserId);
                    return await HandleNoAccessibleDocumentsAsync(request);
                }

                // 3. Context Building
                var contextData = await BuildContextFromDocumentsAsync(accessibleDocuments, request.Query);

                // 4. Token Management
                var optimizedContext = await OptimizeContextForTokenLimitAsync(contextData, request.MaxTokens);

                // 5. AI Generation Phase
                var aiGenerationResult = await GenerateAIResponseAsync(request, optimizedContext);

                // 6. Response Validation
                var validatedResponse = await ValidateAndSanitizeResponseAsync(aiGenerationResult.Response);

                stopwatch.Stop();

                _logger.LogInformation("RAG workflow completed for user {UserId}, Duration: {Duration}ms, TokensUsed: {TokensUsed}",
                    request.UserId, stopwatch.ElapsedMilliseconds, aiGenerationResult.TokensUsed);

                return new RAGResponse
                {
                    Success = true,
                    GeneratedResponse = validatedResponse,
                    SourceDocuments = accessibleDocuments.Select(MapDocumentToReference).ToList(),
                    Context = optimizedContext,
                    TokensUsed = aiGenerationResult.TokensUsed,
                    Model = aiGenerationResult.Model,
                    ConfidenceScore = aiGenerationResult.ConfidenceScore,
                    Metadata = new Dictionary<string, object>
                    {
                        { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds },
                        { "DocumentsFound", documentSearchResult.Documents.Count },
                        { "AccessibleDocuments", accessibleDocuments.Count },
                        { "ContextLength", optimizedContext.Length }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RAG workflow for user {UserId}", request.UserId);

                return new RAGResponse
                {
                    Success = false,
                    GeneratedResponse = "I'm sorry, I encountered an error while searching for information. Please try again.",
                    Metadata = new Dictionary<string, object>
                    {
                        { "Error", ex.Message },
                        { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
                    }
                };
            }
        }

        // Private helper methods

        private async Task<(bool IsValid, string ErrorMessage)> ValidateAndModerateContentAsync(string content, Guid userId)
        {
            try
            {
                // Security analysis
                var securityResult = await _securityService.AnalyzeContentAsync(content, userId, null);
                if (securityResult.HasSecurityIssues)
                {
                    return (false, "Your message contains potentially unsafe content.");
                }

                // Content moderation
                var moderationResult = await _contentModerationService.ModerateContentAsync(content, userId);
                if (!moderationResult.IsApproved)
                {
                    return (false, moderationResult.Reason ?? "Your message violates our content policy.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content validation for user {UserId}", userId);
                return (false, "Unable to validate your message. Please try again.");
            }
        }

        private async Task<ChatSession> GetOrCreateSessionAsync(Guid userId, Guid? sessionId)
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

            // Create new session
            var newSession = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "New Conversation",
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                MessageCount = 0
            };

            await sessionRepo.InsertAsync(newSession);
            await _unitOfWork.CommitAsync();

            return newSession;
        }

        private async Task<Dictionary<string, object>> LoadUserPreferencesAsync(Guid userId)
        {
            try
            {
                var preferences = await _userPreferenceService.GetPreferenceAsync(userId);
                return new Dictionary<string, object>
                {
                    { "Language", preferences?.Language ?? "en" },
                    { "ResponseStyle", preferences?.ResponseStyle ?? "balanced" },
                    { "MaxResponseLength", preferences?.MaxResponseLength ?? 500 },
                    { "IncludeCitations", preferences?.IncludeCitations ?? true }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading preferences for user {UserId}, using defaults", userId);
                return new Dictionary<string, object>
                {
                    { "Language", "en" },
                    { "ResponseStyle", "balanced" },
                    { "MaxResponseLength", 500 },
                    { "IncludeCitations", true }
                };
            }
        }

        private async Task<List<string>> BuildConversationHistoryAsync(Guid sessionId, Guid userId)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var recentMessages = await messageRepo.GetListAsync(
                    m => m.SessionId == sessionId && m.UserId == userId,
                    orderBy: m => m.OrderByDescending(x => x.CreatedAt),
                    include: null);

                var history = new List<string>();
                foreach (var message in recentMessages.Take(10).Reverse())
                {
                    history.Add($"User: {message.Content}");
                    if (!string.IsNullOrEmpty(message.AiResponse))
                    {
                        history.Add($"Assistant: {message.AiResponse}");
                    }
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error building conversation history for session {SessionId}", sessionId);
                return new List<string>();
            }
        }

        private int GetMaxDocumentsFromPreferences(Dictionary<string, object> preferences)
        {
            if (preferences.TryGetValue("MaxDocuments", out var value) && value is int maxDocs)
            {
                return Math.Min(maxDocs, 10); // Cap at 10 documents
            }
            return 5; // Default
        }

        private int GetMaxTokensFromPreferences(Dictionary<string, object> preferences)
        {
            if (preferences.TryGetValue("MaxTokens", out var value) && value is int maxTokens)
            {
                return Math.Min(maxTokens, 8000); // Cap at 8000 tokens
            }
            return 4000; // Default
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
                return new List<AccessibleDocument>(); // Fail secure - no access
            }
        }

        private async Task<string> BuildContextFromDocumentsAsync(List<AccessibleDocument> documents, string query)
        {
            var contextBuilder = new StringBuilder();

            foreach (var doc in documents.OrderByDescending(d => d.RelevanceScore))
            {
                contextBuilder.AppendLine($"Document: {doc.Title}");
                contextBuilder.AppendLine($"Content: {doc.Content}");
                contextBuilder.AppendLine($"Last Modified: {doc.LastModified:yyyy-MM-dd}");
                contextBuilder.AppendLine("---");
            }

            return contextBuilder.ToString();
        }

        private async Task<string> OptimizeContextForTokenLimitAsync(string context, int maxTokens)
        {
            try
            {
                var isWithinLimit = await _aiServiceClient.CountTokensAsync(context);
                if (isWithinLimit <= maxTokens)
                {
                    return context;
                }

                // Truncate context to fit token limit
                var truncatedContext = await _aiServiceClient.TruncateToTokenLimitAsync(context, maxTokens - 500); // Reserve 500 tokens for response
                return truncatedContext;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error optimizing context for token limit, using original");
                return context;
            }
        }

        private async Task<AIGenerationResult> GenerateAIResponseAsync(RAGRequest request, string context)
        {
            var aiRequest = new AdvancedAiGenerationRequest
            {
                Query = request.Query,
                Context = context,
                ConversationHistory = request.ConversationHistory,
                UserPreferences = request.UserPreferences,
                MaxTokens = request.MaxTokens / 2, // Reserve half for response
                Temperature = GetTemperatureFromPreferences(request.UserPreferences)
            };

            var result = await _aiServiceClient.GenerateResponseAsync(aiRequest);

            return new AIGenerationResult
            {
                Response = result.Response,
                TokensUsed = result.TokensUsed,
                Model = result.Model,
                ConfidenceScore = result.ConfidenceScore
            };
        }

        private double GetTemperatureFromPreferences(Dictionary<string, object> preferences)
        {
            if (preferences.TryGetValue("ResponseStyle", out var style))
            {
                return style.ToString().ToLower() switch
                {
                    "creative" => 0.8,
                    "balanced" => 0.5,
                    "precise" => 0.2,
                    _ => 0.5
                };
            }
            return 0.5;
        }

        private async Task<string> ValidateAndSanitizeResponseAsync(string response)
        {
            // Basic sanitization
            if (string.IsNullOrWhiteSpace(response))
            {
                return "I apologize, but I couldn't generate a proper response. Please try rephrasing your question.";
            }

            // Remove any potentially harmful content
            var sanitized = response.Trim();

            // Ensure response doesn't exceed reasonable length
            if (sanitized.Length > 10000)
            {
                sanitized = sanitized.Substring(0, 10000) + "... [Response truncated]";
            }

            return sanitized;
        }

        private async Task<EnhancedResponse> EnhanceResponseAsync(RAGResponse ragResponse, string originalQuery, Guid sessionId)
        {
            var suggestedQuestions = await GenerateSuggestedQuestionsAsync(ragResponse.GeneratedResponse, originalQuery);

            return new EnhancedResponse
            {
                GeneratedResponse = ragResponse.GeneratedResponse,
                SuggestedQuestions = suggestedQuestions,
                Metadata = new Dictionary<string, object>
                {
                    { "SourceDocumentCount", ragResponse.SourceDocuments.Count },
                    { "ConfidenceScore", ragResponse.ConfidenceScore },
                    { "Model", ragResponse.Model }
                }
            };
        }

        private async Task<List<string>> GenerateSuggestedQuestionsAsync(string response, string originalQuery)
        {
            try
            {
                var suggestions = new List<string>();

                // Rule-based suggestions based on response content
                if (response.Contains("policy") || response.Contains("procedure"))
                {
                    suggestions.Add("Can you show me the complete policy document?");
                    suggestions.Add("What are the exceptions to this policy?");
                }

                if (response.Contains("step") || response.Contains("process"))
                {
                    suggestions.Add("Can you explain this process in more detail?");
                    suggestions.Add("What happens if I skip a step?");
                }

                if (response.Contains("requirement") || response.Contains("needed"))
                {
                    suggestions.Add("Are there any alternative requirements?");
                    suggestions.Add("How long does this typically take?");
                }

                // Fallback generic suggestions
                if (suggestions.Count == 0)
                {
                    suggestions.AddRange(new[]
                    {
                        "Can you provide more details about this?",
                        "Are there related topics I should know about?",
                        "Where can I find the official documentation?"
                    });
                }

                return suggestions.Take(3).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating suggested questions");
                return new List<string> { "Can you provide more information about this topic?" };
            }
        }

        private async Task SaveMessageToSessionAsync(Guid sessionId, string userMessage, string aiResponse,
            List<DocumentReference> sourceDocuments, Guid messageId, int tokensUsed)
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
                    MessageType = (Domain.Enum.MessageType)MessageType.UserMessage,
                    TokensUsed = tokensUsed,
                    CreatedAt = DateTime.UtcNow,
                    SourceDocuments = string.Join(",", sourceDocuments.Select(d => d.DocumentId))
                };

                await messageRepo.InsertAsync(message);

                // Update session
                var session = await sessionRepo.SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
                if (session != null)
                {
                    session.MessageCount++;
                    session.LastActivityAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(session.Title) || session.Title == "New Conversation")
                    {
                        session.Title = await GenerateSessionTitleAsync(userMessage);
                    }
                    sessionRepo.UpdateAsync(session);
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message to session {SessionId}", sessionId);
                // Don't throw - this shouldn't break the user experience
            }
        }

        private async Task<Guid> GetUserIdFromSessionAsync(Guid sessionId)
        {
            var sessionRepo = _unitOfWork.GetRepository<ChatSession>();
            var session = await sessionRepo.SingleOrDefaultAsync(predicate: s => s.Id == sessionId);
            return session?.UserId ?? Guid.Empty;
        }

        private async Task<string> GenerateSessionTitleAsync(string firstMessage)
        {
            try
            {
                var titleRequest = new TitleSuggestionRequest
                {
                    Content = firstMessage,
                    MaxLength = 50
                };

                var title = await _aiServiceClient.SuggestTitleAsync(titleRequest);
                return string.IsNullOrEmpty(title) ? "New Conversation" : title;
            }
            catch
            {
                return "New Conversation";
            }
        }

        private async Task<RAGResponse> HandleNoDocumentsFoundAsync(RAGRequest request)
        {
            return new RAGResponse
            {
                Success = true,
                GeneratedResponse = "I couldn't find any relevant documents for your question. You might want to try rephrasing your question or contact your administrator if you believe this information should be available.",
                SourceDocuments = new List<DocumentReference>(),
                Context = string.Empty,
                TokensUsed = 0,
                Model = "fallback",
                ConfidenceScore = 0.0,
                Metadata = new Dictionary<string, object>
                {
                    { "Reason", "NoDocumentsFound" }
                }
            };
        }

        private async Task<RAGResponse> HandleNoAccessibleDocumentsAsync(RAGRequest request)
        {
            return new RAGResponse
            {
                Success = true,
                GeneratedResponse = "I found some relevant documents, but you don't have permission to access them. Please contact your administrator if you need access to this information.",
                SourceDocuments = new List<DocumentReference>(),
                Context = string.Empty,
                TokensUsed = 0,
                Model = "fallback",
                ConfidenceScore = 0.0,
                Metadata = new Dictionary<string, object>
                {
                    { "Reason", "NoAccessibleDocuments" }
                }
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

        private ConversationResponse CreateErrorResponse(string errorMessage, Guid messageId, TimeSpan processingTime)
        {
            return new ConversationResponse
            {
                Success = false,
                Message = errorMessage,
                Response = "I apologize, but I encountered an issue processing your request. Please try again.",
                MessageId = messageId,
                SessionId = Guid.Empty,
                DocumentReferences = new List<DocumentReference>(),
                SuggestedQuestions = new List<string> { "Can you help me with something else?" },
                Metadata = new Dictionary<string, object> { { "Error", errorMessage } },
                Timestamp = DateTime.UtcNow,
                TokensUsed = 0,
                ProcessingTime = processingTime
            };
        }
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

public class AIGenerationResult
{
    public string Response { get; set; }
    public int TokensUsed { get; set; }
    public string Model { get; set; }
    public double ConfidenceScore { get; set; }
}

public class EnhancedResponse
{
    public string GeneratedResponse { get; set; }
    public List<string> SuggestedQuestions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// Enums


public enum MessageType
{
    UserMessage = 1,
    SystemMessage = 2,
    ErrorMessage = 3
}
