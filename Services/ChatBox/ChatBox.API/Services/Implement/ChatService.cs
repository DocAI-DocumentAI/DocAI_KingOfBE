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
        private readonly string _emptyAnswerText; // REVIEW POINT: Empty Answer Text
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
            _emptyAnswerText = _configuration["ChatService:EmptyAnswerText"] ?? "I'm sorry, I couldn't find relevant information in your internal documents. Could you please rephrase your question or provide more context? I can only answer based on the documents provided to me."; // REVIEW POINT: Lấy EmptyAnswerText
            _contextWindowSize = _configuration.GetValue<int>("ChatService:ContextWindowSize", 10);
            _docSearchLimit = _configuration.GetValue<int>("ChatService:DocSearchLimit", 5);
            _docMinRelevance = _configuration.GetValue<double>("ChatService:DocMinRelevance", 0.7);
            _streamResponseDefault = _configuration.GetValue<bool>("ChatService:StreamResponseDefault", false);

            _logger.LogInformation("ChatService initialized with SystemPrompt: {SystemPrompt}, ContextWindowSize: {ContextWindowSize}", _systemPrompt, _contextWindowSize);
        }

        // Khởi tạo một cuộc hội thoại mới
        public async Task<ConversationResponse> StartNewConversationAsync(string userId, ChatRequestPayload requestPayload)
        {
            _logger.LogInformation($"Starting new conversation for user {userId} with question: {requestPayload.Question}");

            var conversation = new Conversation
            {
                UserId = userId,
                Title = requestPayload.Question.Substring(0, Math.Min(requestPayload.Question.Length, 50)) + (requestPayload.Question.Length > 50 ? "..." : ""),
                LastActive = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<Conversation>().InsertAsync(conversation);
            await _unitOfWork.CommitAsync();

            // Bước 2: Gọi Document Service để tìm tài liệu liên quan cho câu hỏi đầu tiên
            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = requestPayload.Question,
                Filters = BuildDocumentFilters(userId), // REVIEW POINT: Thêm logic phân quyền
                MinRelevance = _docMinRelevance
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            // Bước 3: Xây dựng AIRequest External (không có lịch sử ban đầu)
            // AIRequestExternal sẽ định dạng các Message cho AI Microservice
            var aiRequestExternal = BuildAIRequestExternal(requestPayload.Question, new List<MessageHistory>(), searchDocResponse.RelevantSources.ToList(), _streamResponseDefault);

            // Bước 4: Gọi AI Microservice
            string aiAnswer = "";
            if (searchDocResponse.NoResult) // Nếu không có tài liệu liên quan
            {
                aiAnswer = _emptyAnswerText; // Trả lời bằng câu khôn ngoan
                _logger.LogInformation($"No relevant documents found for conversation {conversation.Id}. Responding with empty answer text.");
            }
            else
            {
                var aiResponseExternal = await _aiClient.GenerateAIResponseAsync(aiRequestExternal);
                aiAnswer = aiResponseExternal.Answer;
            }

            // Bước 5: Lưu trữ cả câu hỏi người dùng và câu trả lời của AI
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

            conversation.LastActive = DateTime.UtcNow; // Cập nhật thời gian hoạt động
            _unitOfWork.GetRepository<Conversation>().UpdateAsync(conversation);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<ConversationResponse>(conversation);
            response.Messages = new List<MessageResponse>
            {
                _mapper.Map<MessageResponse>(userMessage),
                _mapper.Map<MessageResponse>(assistantMessage)
            };
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
        // Tiếp tục chat trong một cuộc hội thoại đã có (non-streaming)
        public async Task<ChatResponse> ContinueChatAsync(string conversationId, string userId, ChatRequestPayload requestPayload)
        {
            _logger.LogInformation($"Continuing chat in conversation {conversationId} for user {userId} with question: {requestPayload.Question}");

            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogError($"Conversation {conversationId} not found or unauthorized for user {userId}.");
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found or you are not authorized to continue it.");
            }

            // Bước 1: Tải lịch sử hội thoại và giới hạn
            var history = await GetConversationHistoryAsync(conversationId, userId);
            // Fix lỗi 'Cannot convert method group 'Order' to non-delegate type 'long?'.'
            // History đã là List<MessageResponse>. Cần map nó về MessageHistory để hàm LimitConversationHistory xử lý.
            var recentHistory = LimitConversationHistory(history.Select(m => _mapper.Map<MessageHistory>(m)).ToList());

            // Bước 2: Gọi Document Service để tìm tài liệu liên quan và áp dụng bộ lọc quyền/hiệu lực
            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = requestPayload.Question,
                MinRelevance = _docMinRelevance,
                // REVIEW POINT: Cấu hình Limit từ ChatService settings
                // REVIEW POINT: Áp dụng bộ lọc quyền truy cập và hiệu lực tài liệu
                Filters = BuildDocumentFilters(userId) // userRoles và currentDateTime sẽ được xử lý trong hàm này
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            // Bước 3: Xây dựng AIRequest External
            var aiRequestExternal = BuildAIRequestExternal(requestPayload.Question, recentHistory, searchDocResponse.RelevantSources.ToList(), _streamResponseDefault);

            string aiAnswer = "";
            // REVIEW POINT: Xử lý khi AI không tìm thấy thông tin hoặc không đủ thẩm quyền
            if (searchDocResponse.NoResult)
            {
                aiAnswer = _emptyAnswerText; // Trả lời bằng câu khôn ngoan
                _logger.LogInformation($"No relevant documents found for conversation {conversationId}. Responding with empty answer text.");
            }
            else
            {
                var aiResponseExternal = await _aiClient.GenerateAIResponseAsync(aiRequestExternal);
                aiAnswer = aiResponseExternal.Answer;
            }

            // Bước 4: Lưu trữ câu hỏi người dùng và câu trả lời của AI vào lịch sử
            var nextOrder = history.Any() ? history.Max(m => m.Order) + 1 : 0;
            var userMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "user",
                Content = requestPayload.Question,
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

            conversation.LastActive = DateTime.UtcNow; // Cập nhật thời gian hoạt động
            _unitOfWork.GetRepository<Conversation>().UpdateAsync(conversation);
            await _unitOfWork.CommitAsync();

            return new ChatResponse
            {
                ConversationId = conversation.Id,
                Answer = aiAnswer,
                Timestamp = assistantMessage.CreateAt
            };
        }

        public async IAsyncEnumerable<string> StreamContinueChatAsync(string conversationId, string userId, ChatRequestPayload requestPayload)
        {
            _logger.LogInformation($"Streaming chat requested for conversation {conversationId} for user {userId} with question: {requestPayload.Question}");

            var conversation = await _unitOfWork.GetRepository<Conversation>().SingleOrDefaultAsync(predicate: c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null)
            {
                _logger.LogError($"Conversation {conversationId} not found or unauthorized for user {userId} for streaming.");
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found or you are not authorized to stream chat for it.");
            }

            // Bước 1: Tải lịch sử hội thoại và giới hạn
            var history = await GetConversationHistoryAsync(conversationId, userId);
            var recentHistory = LimitConversationHistory(history.Select(m => _mapper.Map<MessageHistory>(m)).ToList());

            // Bước 2: Gọi Document Service để tìm tài liệu liên quan và áp dụng bộ lọc quyền/hiệu lực
            var searchDocRequest = new SearchDocumentRequestExternal
            {
                Query = requestPayload.Question,
                MinRelevance = _docMinRelevance,
                Filters = BuildDocumentFilters(userId)
            };
            var searchDocResponse = await _documentClient.SearchRelevantDocumentsAsync(searchDocRequest);

            // Bước 3: Xây dựng AIRequest External (yêu cầu streaming)
            var aiRequestExternal = BuildAIRequestExternal(requestPayload.Question, recentHistory, searchDocResponse.RelevantSources.ToList(), streamResponse: true);

            var responseBuilder = new StringBuilder();
            var nextOrder = history.Any() ? history.Max(m => m.Order) + 1 : 0;

            // REVIEW POINT: Xử lý khi AI không tìm thấy thông tin hoặc không đủ thẩm quyền cho streaming
            if (searchDocResponse.NoResult)
            {
                var emptyAnswer = _emptyAnswerText;
                _logger.LogInformation($"No relevant documents found for streaming conversation {conversation.Id}. Responding with empty answer text.");
                yield return emptyAnswer; // Trả về thông báo rỗng ngay lập tức
            }
            else
            {
                // Bước 4: Gọi AI Microservice và stream từng chunk
                await foreach (var chunk in _aiClient.StreamAIResponseAsync(aiRequestExternal))
                {
                    responseBuilder.Append(chunk);
                    yield return chunk; // Trả về từng chunk ngay lập tức
                }
            }

            // Bước 5: Lưu trữ câu hỏi người dùng và câu trả lời của AI vào lịch sử sau khi stream hoàn tất
            var userMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "user",
                Content = requestPayload.Question,
                Order = nextOrder,
                CreateAt = DateTime.UtcNow
            };
            var assistantMessage = new MessageHistory
            {
                ConversationId = conversation.Id,
                SenderRole = "assistant",
                Content = responseBuilder.ToString(), // Lưu trữ toàn bộ câu trả lời
                Order = nextOrder + 1,
                CreateAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<MessageHistory>().InsertRangeAsync(new[] { userMessage, assistantMessage });
            await _unitOfWork.CommitAsync();

            conversation.LastActive = DateTime.UtcNow; // Cập nhật thời gian hoạt động
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

            var aiMessages = new List<MessageExternal>(); // REVIEW POINT: Dùng MessageExternal từ Chat.API.Payload.External.AI

            // Luôn thêm System Prompt vào đầu tiên
            aiMessages.Add(new MessageExternal
            {
                Role = "system",
                Content = systemPrompt
            });

            // Thêm các tin nhắn lịch sử (User và Assistant)
            foreach (var msg in history)
            {
                aiMessages.Add(new MessageExternal
                {
                    Role = msg.SenderRole,
                    Content = msg.Content
                });
            }

            // Thêm thông tin tài liệu liên quan vào AIRequestExternal.Documents
            // AI Microservice sẽ tự định dạng chúng vào prompt nội bộ của nó
            var documentsForAI = relevantDocuments.Select(doc => new DocumentExternal
            {
                Id = doc.FileName, // Sử dụng FileName làm ID tạm thời cho DocumentExternal
                Content = doc.TextSnippet,
                Title = doc.FileName, // Giả sử FileName là Title
                DocumentName = doc.FileName,
                ChunkId = doc.FileName // Giả sử FileName là ChunkId
            }).ToList();

            var aiRequest = new AIRequestExternal
            {
                Question = userQuestion,
                SystemPrompt = systemPrompt, // System prompt vẫn được truyền riêng cho AI
                Documents = documentsForAI,
                StreamResponse = streamResponse
            };

            return aiRequest;
        }

        // Phương thức hỗ trợ: Giới hạn lịch sử hội thoại
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

        // REVIEW POINT: Phương thức hỗ trợ: Xây dựng Filters cho Document Service
        private List<string> BuildDocumentFilters(string userId) // Có thể thêm List<string> userRoles, DateTime currentDateTime nếu cần
        {
            var filters = new List<string>();

            // 1. Bộ lọc quyền truy cập: Chỉ hiển thị tài liệu mà userId này được phép xem
            // Giả sử mỗi tài liệu có một tag "user:<userId>" hoặc "access_group:<groupName>"
            // Bạn cần lấy thông tin nhóm/roles của userId từ Auth Service
            filters.Add($"user:{userId}"); // Ví dụ đơn giản: chỉ user được xem tài liệu của mình

            // 2. Bộ lọc hiệu lực tài liệu: Chỉ hiển thị tài liệu còn hiệu lực
            // Giả sử Document Service có tag "status:active" và "effective_until:<date>"
            filters.Add("status:active");
            // Thêm logic để kiểm tra ngày tháng hiện tại với effective_until
            // Ví dụ: filters.Add($"effective_until_gte:{DateTime.UtcNow.ToString("yyyy-MM-dd")}");
            // Điều này đòi hỏi Document Service hỗ trợ các filter về ngày tháng.

            // 3. Các bộ lọc khác nếu có (ví dụ: "category:policy", "department:hr")
            // filters.Add("category:policy");

            return filters;
        }
    }
}
