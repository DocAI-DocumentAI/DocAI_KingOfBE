using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using System.Text;

namespace ChatBox.API.Services.Implement
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<ChatService> _logger;
        private readonly IAIClient _aiClient;
        private readonly IDocumentClient _documentClient;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly string _systemPrompt;
        private readonly string _emptyAnswerText;
        private readonly int _contextWindowSize;
        private readonly int _docSearchLimit;
        private readonly double _docMinRelevance;
        private readonly bool _streamResponseDefault;

        public ChatService(
                 IUnitOfWork<ChatBoxDbContext> unitOfWork,
                 ILogger<ChatService> logger,
                 IAIClient aiClient,
                 IDocumentClient documentClient,
                 IConfiguration configuration,
                 IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
            _documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

            _systemPrompt = _configuration["ChatService:SystemPrompt"] ?? "You are a helpful assistant. Please answer questions based only on the provided documents. If the information is not available in the documents, state that you cannot find the relevant information.";
            _emptyAnswerText = _configuration["ChatService:EmptyAnswerText"] ?? "I'm sorry, I couldn't find relevant information in your internal documents. Could you please rephrase your question or provide more context? I can only answer based on the documents provided to me.";
            _contextWindowSize = _configuration.GetValue<int>("ChatService:ContextWindowSize", 10);
            _docSearchLimit = _configuration.GetValue<int>("ChatService:DocSearchLimit", 5);
            _docMinRelevance = _configuration.GetValue<double>("ChatService:DocMinRelevance", 0.7);
            _streamResponseDefault = _configuration.GetValue<bool>("ChatService:StreamResponseDefault", false);

            _logger.LogInformation("ChatService initialized with SystemPrompt: {SystemPrompt}, ContextWindowSize: {ContextWindowSize}", _systemPrompt, _contextWindowSize);
        }

        public async Task<ConversationResponse> StartNewConversationAsync(string userId, List<string> userRoles, ChatRequestPayload requestPayload)
        {
            _logger.LogInformation($"Starting new conversation for user {userId} with roles {string.Join(",", userRoles)} and question: {requestPayload.Question}");

            var conversation = new Conversation
            {
                UserId = userId,
                Title = requestPayload.Question.Substring(0, Math.Min(requestPayload.Question.Length, 50)) + (requestPayload.Question.Length > 50 ? "..." : ""),
                LastActive = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<Conversation>().InsertAsync(conversation);
            await _unitOfWork.CommitAsync();

            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = requestPayload.Question,
                MinRelevance = _docMinRelevance,
                Filters = BuildDocumentFilters(userId, userRoles) // REVIEW POINT: Truyền userId và userRoles
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            string aiAnswer = "";
            if (searchDocResponse.NoResult)
            {
                aiAnswer = _emptyAnswerText;
                _logger.LogInformation($"No relevant documents found for conversation {conversation.Id}. Responding with empty answer text.");
            }
            else
            {
                var aiRequestExternal = BuildAIRequestExternal(requestPayload.Question, new List<MessageHistory>(), searchDocResponse.RelevantSources.ToList(), _streamResponseDefault);
                var aiResponseExternal = await _aiClient.GenerateAIResponseAsync(aiRequestExternal);
                aiAnswer = aiResponseExternal.Answer;

                if (aiAnswer.Contains("I cannot find relevant information", StringComparison.OrdinalIgnoreCase) ||
                    aiAnswer.Contains("not covered by my current knowledge base", StringComparison.OrdinalIgnoreCase) ||
                    aiAnswer.Contains("I don't know", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"AI responded with a 'not found' message based on SystemPrompt for conversation {conversation.Id}.");
                    aiAnswer = _emptyAnswerText;
                }
            }

            var userMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "user",
                Content = requestPayload.Question,
                Order = 0,
                CreateAt = DateTime.UtcNow
            };
            var assistantMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "assistant",
                Content = aiAnswer,
                Order = 1,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<MessageHistory>().InsertRangeAsync(new[] { userMessage, assistantMessage });
            await _unitOfWork.CommitAsync();

            conversation.LastActive = DateTime.UtcNow;
            _unitOfWork.GetRepository<Conversation>().UpdateAsync(conversation);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation($"New conversation {conversation.Id} started for user {userId}");

            var response = _mapper.Map<ConversationResponse>(conversation);
            var savedMessages = new List<MessageHistory> { userMessage, assistantMessage };
            response.Messages = _mapper.Map<List<MessageResponse>>(savedMessages);

            return response;
        }
        public async Task<List<ConversationSummaryResponse>> GetUserConversationsAsync(string userId)
        {
            _logger.LogInformation($"Retrieving conversations for user {userId}");
            var conversations = await _unitOfWork.GetRepository<Conversation>().GetListAsync(
                predicate: c => c.UserId == userId,
                orderBy: c => c.OrderByDescending(conv => conv.LastActive)
            );
            return _mapper.Map<List<ConversationSummaryResponse>>(conversations.ToList());
        }
        public async Task<List<MessageResponse>> GetConversationHistoryAsync(string conversationId, string userId)
        {
            _logger.LogInformation($"Retrieving history for conversation {conversationId} for user {userId}");
            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogWarning($"Conversation {conversationId} not found or unauthorized for user {userId}.");
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found or you are not authorized to view it.");
            }

            var messages = await _unitOfWork.GetRepository<MessageHistory>().GetListAsync(
                predicate: m => m.ConversationId == conversationId,
                orderBy: m => m.OrderBy(msg => msg.Order)
            );
            return _mapper.Map<List<MessageResponse>>(messages.ToList());
        }
        public async Task<ChatResponse> ContinueChatAsync(string conversationId, string userQuestion, string userId, List<string> userRoles) // REVIEW POINT: Thêm userRoles
        {
            _logger.LogInformation($"Continuing chat in conversation {conversationId} for user {userId} with question: {userQuestion}");

            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogError($"Conversation {conversationId} not found or unauthorized for user {userId}.");
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found or you are not authorized to continue it.");
            }

            var history = await GetConversationHistoryAsync(conversationId, userId);
            var recentHistory = LimitConversationHistory(history.Select(m => _mapper.Map<MessageHistory>(m)).ToList());

            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = userQuestion,
                MinRelevance = _docMinRelevance,
                Filters = BuildDocumentFilters(userId, userRoles) // REVIEW POINT: Truyền userId và userRoles
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            var aiRequestExternal = BuildAIRequestExternal(userQuestion, recentHistory, searchDocResponse.RelevantSources.ToList(), _streamResponseDefault);

            string aiAnswer = "";
            if (searchDocResponse.NoResult)
            {
                aiAnswer = _emptyAnswerText;
                _logger.LogInformation($"No relevant documents found for conversation {conversationId}. Responding with empty answer text.");
            }
            else
            {
                var aiResponseExternal = await _aiClient.GenerateAIResponseAsync(aiRequestExternal);
                aiAnswer = aiResponseExternal.Answer;

                if (aiAnswer.Contains("I cannot find relevant information", StringComparison.OrdinalIgnoreCase) ||
                    aiAnswer.Contains("not covered by my current knowledge base", StringComparison.OrdinalIgnoreCase) ||
                    aiAnswer.Contains("I don't know", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"AI responded with a 'not found' message based on SystemPrompt for conversation {conversation.Id}.");
                    aiAnswer = _emptyAnswerText;
                }
            }

            var nextOrder = history.Any() ? history.Max(m => m.Order) + 1 : 0;
            var userMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "user",
                Content = userQuestion,
                Order = nextOrder,
                CreateAt = DateTime.UtcNow
            };
            var assistantMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "assistant",
                Content = aiAnswer,
                Order = nextOrder + 1,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<MessageHistory>().InsertRangeAsync(new[] { userMessage, assistantMessage });
            await _unitOfWork.CommitAsync();

            conversation.LastActive = DateTime.UtcNow;
            _unitOfWork.GetRepository<Conversation>().UpdateAsync(conversation);
            await _unitOfWork.CommitAsync();

            return new ChatResponse
            {
                ConversationId = conversation.Id,
                Answer = aiAnswer,
                Timestamp = assistantMessage.CreateAt
            };
        }


        public async IAsyncEnumerable<string> StreamContinueChatAsync(string conversationId, string userQuestion, string userId, List<string> userRoles) // REVIEW POINT: Thêm userRoles
        {
            _logger.LogInformation($"Streaming chat requested for conversation {conversationId} for user {userId} with question: {userQuestion}");

            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogError($"Conversation {conversationId} not found or unauthorized for user {userId} for streaming.");
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found or you are not authorized to stream chat for it.");
            }

            var history = await GetConversationHistoryAsync(conversationId, userId);
            var recentHistory = LimitConversationHistory(history.Select(m => _mapper.Map<MessageHistory>(m)).ToList());

            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = userQuestion,
                MinRelevance = _docMinRelevance,
                Filters = BuildDocumentFilters(userId, userRoles) // REVIEW POINT: Truyền userId và userRoles
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            var aiRequestExternal = BuildAIRequestExternal(userQuestion, recentHistory, searchDocResponse.RelevantSources.ToList(), streamResponse: true);

            var responseBuilder = new StringBuilder();
            var nextOrder = history.Any() ? history.Max(m => m.Order) + 1 : 0;

            if (searchDocResponse.NoResult)
            {
                var emptyAnswer = _emptyAnswerText;
                _logger.LogInformation($"No relevant documents found for streaming conversation {conversation.Id}. Responding with empty answer text.");
                yield return emptyAnswer;
            }
            else
            {
                await foreach (var chunk in _aiClient.StreamAIResponseAsync(aiRequestExternal))
                {
                    responseBuilder.Append(chunk);
                    yield return chunk;
                }
                string fullStreamedAnswer = responseBuilder.ToString();
                if (fullStreamedAnswer.Contains("I cannot find relevant information", StringComparison.OrdinalIgnoreCase) ||
                    fullStreamedAnswer.Contains("not covered by my current knowledge base", StringComparison.OrdinalIgnoreCase) ||
                    fullStreamedAnswer.Contains("I don't know", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Streaming AI responded with a 'not found' message based on SystemPrompt for conversation {conversation.Id}.");
                }
            }

            var userMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "user",
                Content = userQuestion,
                Order = nextOrder,
                CreateAt = DateTime.UtcNow
            };
            var assistantMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "assistant",
                Content = responseBuilder.ToString(),
                Order = nextOrder + 1,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<MessageHistory>().InsertRangeAsync(new[] { userMessage, assistantMessage });
            await _unitOfWork.CommitAsync();

            conversation.LastActive = DateTime.UtcNow;
            _unitOfWork.GetRepository<Conversation>().UpdateAsync(conversation);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation($"Conversation {conversationId} streamed and continued for user {userId}.");
        }

        // Xóa một cuộc hội thoại
        public async Task<bool> DeleteConversationAsync(string conversationId, string userId)
        {
            _logger.LogInformation($"Attempting to delete conversation {conversationId} for user {userId}.");
            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogWarning($"Attempted to delete non-existent or unauthorized conversation {conversationId} for user {userId}");
                return false;
            }
            _unitOfWork.GetRepository<Conversation>().DeleteAsync(conversation);
            var isSuccess = await _unitOfWork.CommitAsync() > 0;
            if (isSuccess)
            {
                _logger.LogInformation($"Conversation {conversationId} deleted successfully for user {userId}.");
            }
            return isSuccess;
        }

        // Phương thức hỗ trợ: Xây dựng AIRequestExternal
        private AIRequestExternal BuildAIRequestExternal(
            string userQuestion,
            List<MessageHistory> history,
            List<RelevantSourceResponseExternal> relevantDocuments,
            bool streamResponse)
        {
            var systemPrompt = _systemPrompt;

            var aiMessages = new List<MessageExternal>();

            aiMessages.Add(new MessageExternal
            {
                Role = "system",
                Content = systemPrompt
            });

            foreach (var msg in history)
            {
                aiMessages.Add(new MessageExternal
                {
                    Role = msg.SenderRole,
                    Content = msg.Content
                });
            }

            var documentsForAI = relevantDocuments.Select(doc => new DocumentExternal
            {
                Id = doc.FileName,
                Content = doc.TextSnippet,
                Title = doc.FileName,
                DocumentName = doc.FileName,
                ChunkId = doc.FileName
            }).ToList();

            var aiRequest = new AIRequestExternal
            {
                Question = userQuestion,
                SystemPrompt = systemPrompt,
                Documents = documentsForAI,
                StreamResponse = streamResponse
            };

            return aiRequest;
        }
        private List<MessageHistory> LimitConversationHistory(List<MessageHistory> fullHistory)
        {
            if (fullHistory.Count > _contextWindowSize * 2)
            {
                return fullHistory
                    .OrderByDescending(m => m.Order)
                    .Take(_contextWindowSize * 2)
                    .OrderBy(m => m.Order)
                    .ToList();
            }
            return fullHistory;
        }

        private List<string> BuildDocumentFilters(string userId, List<string> userRoles) // REVIEW POINT: Nhận userRoles
        {
            var filters = new List<string>();

            // 1. Bộ lọc quyền truy cập: Chỉ hiển thị tài liệu mà userId/roles này được phép xem
            filters.Add($"user:{userId}"); // Mọi user thấy tài liệu của chính họ

            // Giả lập logic phân quyền phức tạp hơn dựa trên userRoles (Ví dụ: từ Auth.API.Attributes.Roles)
            if (userRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                userRoles.Contains("Manager", StringComparer.OrdinalIgnoreCase))
            {
                filters.Add("access_group:management"); // Admin/Manager thấy tài liệu quản lý
            }
            if (userRoles.Contains("Editor", StringComparer.OrdinalIgnoreCase))
            {
                filters.Add("access_group:editor_docs"); // Editor thấy tài liệu chỉnh sửa
            }
            filters.Add("status:active"); // Chỉ tài liệu active

            return filters;
        }
    }
}
