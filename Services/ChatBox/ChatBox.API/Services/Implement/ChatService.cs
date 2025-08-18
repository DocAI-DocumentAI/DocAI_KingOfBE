using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly IHttpContextAccessor _httpContextAccessor; 
        public ChatService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            ISemanticKernelService semanticKernelService,
            ITokenCountService tokenCountService,
            IPreferenceService preferenceService,
            IManualDocumentSearchService manualDocumentSearchService,
            ILogger<ChatService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _semanticKernelService = semanticKernelService;
            _tokenCountService = tokenCountService;
            _preferenceService = preferenceService;
            _manualDocumentSearchService = manualDocumentSearchService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
        {
            await ValidateMessageStrictAsync(request.Message);

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId, request.DocumentId);
            await ValidateSessionModelConsistency(session, request.ModelName);
            await EnsureSessionModelIsActive(session);

            var isFirstMessage = await IsFirstUserMessageInSession(session.Id);

            _logger.LogInformation("Processing chat message for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);

            // ✅ UPDATED: Get raw document content instead of processed answer
            var (documentContent, documentSources, hasDocumentContext) = await SearchDocumentContext(request.Message, userId, request.DocumentId);
            var aiResponse = await GenerateAIResponse(session, request.Message, documentContent, documentSources, hasDocumentContext);

            await ValidateAIResponse(aiResponse, session.Id);

            var (userMessage, aiMessage) = CreateChatMessages(request.Message, aiResponse, session, userId, documentSources);
            await SaveMessagesAndUpdateSession(userMessage, aiMessage, session, userId, isFirstMessage, request.Message);

            _logger.LogInformation("Chat message processed successfully for session {SessionId}", session.Id);

            return CreateChatResponse(session, aiResponse, aiMessage, documentSources, hasDocumentContext);
        }

        public async Task<IAsyncEnumerable<ChatStreamResponse>> SendMessageStreamAsync(ChatRequest request, string userId, CancellationToken cancellationToken = default)
        {
            await ValidateMessageStrictAsync(request.Message);

            var session = await GetOrCreateSessionAsync(request.SessionId, request.ModelName, userId, request.DocumentId);
            await ValidateSessionModelConsistency(session, request.ModelName);
            await EnsureSessionModelIsActive(session);

            var isFirstMessage = await IsFirstUserMessageInSession(session.Id);

            _logger.LogInformation("Processing streaming chat for session {SessionId}, isFirstMessage: {IsFirstMessage}",
                session.Id, isFirstMessage);


            // ✅ UPDATED: Get raw document content
            var (documentContent, documentSources, hasDocumentContext) = await SearchDocumentContext(request.Message, userId, request.DocumentId);
            var responseStream = await GenerateAIResponseStream(session, request.Message, documentContent, documentSources);

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
        private async Task<string> GenerateAIResponse(ChatSession session, string userMessage, string documentContent, List<DocumentInfo> documentSources, bool hasDocumentContext)
        {
            var cleanChatHistory = await BuildCleanChatHistoryAsync(session.Id);
            cleanChatHistory.AddUserMessage(userMessage);

            var aiChatHistory = hasDocumentContext
                ? CreateEnhancedChatHistoryForAI(cleanChatHistory, documentContent, userMessage, documentSources)
                : cleanChatHistory;

            LogDocumentContextUsage(hasDocumentContext);

            return await _semanticKernelService.GetChatResponseAsync(session.ModelName, aiChatHistory);
        }

        /// <summary>
        /// ✅ UPDATED: Generate streaming AI response with raw content
        /// </summary>
        private async Task<IAsyncEnumerable<string>> GenerateAIResponseStream(ChatSession session, string userMessage, string documentContent, List<DocumentInfo> documentSources)
        {
            var cleanChatHistory = await BuildCleanChatHistoryAsync(session.Id);
            cleanChatHistory.AddUserMessage(userMessage);
            var aiChatHistory = CreateEnhancedChatHistoryForAI(cleanChatHistory, documentContent, userMessage, documentSources);


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

                if (!string.IsNullOrEmpty(documentContent) || documentSources?.Any() == true)
                {
                    // ✅ BUILD COMPLETE DOCUMENT PACKAGE với metadata
                    var completeDocumentInfo = BuildCompleteDocumentPackage(documentContent, documentSources, currentQuestion);
                    var actualSourceDocumentTitle = GetActualSourceDocumentTitle(documentContent, documentSources);
                    var citationSuffix = !string.IsNullOrEmpty(actualSourceDocumentTitle)
                        ? $"[Trích từ tài liệu: {actualSourceDocumentTitle}]"
                        : "[Trích từ tài liệu nội bộ]";

                    // ✅ STRICT: Base system prompt FIRST, then document-specific rules
                    enhancedSystemPrompt = $@"{originalSystemMessage.Content}

🔒 CHUYÊN GIA TÀI LIỆU NỘI BỘ - QUY TẮC TUYỆT ĐỐI
=== THÔNG TIN TÀI LIỆU HOÀN CHỈNH ===
{completeDocumentInfo}
=== HẾT THÔNG TIN TÀI LIỆU ===

🚨 QUY TẮC TUYỆT ĐỐI - KHÔNG ĐƯỢC VI PHẠM:
1. **NGUỒN THÔNG TIN DUY NHẤT:**
   - CHỈ sử dụng thông tin trong ""THÔNG TIN TÀI LIỆU HOÀN CHỈNH"" ở trên
   - TUYỆT ĐỐI KHÔNG được dùng kiến thức chung, kiến thức bên ngoài, hay bất kỳ thông tin nào không có trong tài liệu
   - KHÔNG được bịa đặt, suy đoán, hoặc giải thích bằng kiến thức riêng

2. **XỬ LÝ TỪNG LOẠI CÂU HỏI:**
   📊 **CÂU HỎI ĐẾM SỐ LƯỢNG:** ('có bao nhiêu', 'có mấy', 'tổng cộng')
   - BƯỚC 1: Đọc ""THỐNG KÊ TÀI LIỆU TỔNG QUAN"" để biết con số chính xác
   - BƯỚC 2: Kiểm tra ""CHI TIẾT TỪNG TÀI LIỆU"" để xác nhận và liệt kê
   - BƯỚC 3: Trả lời: ""Có [SỐ CHÍNH XÁC] tài liệu [LOẠI]: 1. [Tên]..., Tổng: [SỐ] tài liệu.""

   📄 **CÂU HỎI NỘI DUNG:** ('điều 5 nói gì', 'quy định về', 'thủ tục như thế nào')
   - BƯỚC 1: Tìm trong ""NỘI DUNG TÀI LIỆU HOÀN CHỈNH""
   - BƯỚC 2: Nếu có Summary/Description phù hợp thì dùng
   - BƯỚC 3: Trích dẫn chính xác, KHÔNG thêm bớt hay giải thích
   - BƯỚC 4: Kết thúc bằng ""{citationSuffix}""

   📋 **CÂU HỎI METADATA:** ('ai ký', 'hiệu lực khi nào', 'thuộc phòng nào')
   - BƯỚC 1: Tìm trong ""CHI TIẾT TỪNG TÀI LIỆU"" phần metadata
   - BƯỚC 2: Trả lời chính xác thông tin có sẵn
   - BƯỚC 3: Nếu không có thông tin → ""Không có thông tin về [YÊU CẦU] trong tài liệu""

   🔍 **CÂU HỎI TÌM KIẾM:** ('tài liệu về HR', 'quy định bảo mật')
   - BƯỚC 1: Kiểm tra Tags → Title → Summary → Content
   - BƯỚC 2: Liệt kê các tài liệu phù hợp với mô tả ngắn
   - BƯỚC 3: Sắp xếp theo độ liên quan (RelevanceScore nếu có)

   🔗 **CÂU HỎI SO SÁNH:** ('khác biệt giữa A và B', 'tài liệu nào mới hơn')
   - BƯỚC 1: Lấy thông tin của từng tài liệu từ CHI TIẾT TỪNG TÀI LIỆU
   - BƯỚC 2: So sánh dựa trên thông tin có sẵn (ngày, nội dung, metadata)
   - BƯỚC 3: KHÔNG đưa ra nhận xét chủ quan, chỉ nêu sự khác biệt thực tế

   📖 **CÂU HỎI TỔNG QUAN:** ('tóm tắt tài liệu', 'nội dung chính')
   - BƯỚC 1: Sử dụng Summary (nếu có) làm cơ sở
   - BƯỚC 2: Bổ sung từ Description và các điểm chính trong Content
   - BƯỚC 3: KHÔNG thêm ý kiến hay giải thích cá nhân

3. **CHAT HISTORY AWARENESS:**
   - Tham khảo cuộc hội thoại trước để hiểu context
   - Khi user nói ""tài liệu này"", ""quyết định trên"" → dùng tài liệu đã thảo luận
   - Trả lời tự nhiên như cuộc hội thoại liên tục

4. **CÁCH TRẢ LỜI CHUẨN:**
   - Bắt đầu: ""Theo tài liệu [TÊN TÀI LIỆU]:"" (nếu có tên cụ thể)
   - Nội dung: Trích dẫn/tóm tắt chính xác từ tài liệu
   - Kết thúc: ""{citationSuffix}""

🚫 TUYỆT ĐỐI CẤM - VI PHẠM SẼ BỊ ĐÁNH GIÁ SAI:
❌ Đưa ra thông tin KHÔNG CÓ trong ""THÔNG TIN TÀI LIỆU HOÀN CHỈNH""
❌ Sử dụng kiến thức chung để giải thích (ví dụ: giải thích khái niệm HR, IT, pháp lý...)
❌ Bịa đặt số liệu, ngày tháng, tên người, quy định
❌ Đề xuất tìm kiếm internet, liên hệ cơ quan, kiểm tra nguồn khác
❌ Nói ""dựa trên kinh nghiệm"", ""theo thông lệ"", ""thường thì""
❌ Đưa ra lời khuyên không dựa trên tài liệu cụ thể
❌ Giải thích thuật ngữ bằng kiến thức bên ngoài
❌ Đếm sai số lượng tài liệu hoặc bỏ qua tài liệu
❌ Thêm thông tin ""để đầy đủ hơn"" nếu không có trong tài liệu

✅ CHỈ ĐƯỢC PHÉP:
✓ Thông tin CÓ SẴN 100% trong ""THÔNG TIN TÀI LIỆU HOÀN CHỈNH""
✓ Trích dẫn chính xác từ Summary, Description, Content, Metadata
✓ Đếm và liệt kê dựa trên ""CHI TIẾT TỪNG TÀI LIỆU""
✓ Tham khảo chat history để hiểu context câu hỏi
✓ Nói ""Không có thông tin về [YÊU CẦU] trong tài liệu"" khi thiếu thông tin";
                }
                else
                {
                    // ✅ NO DOCUMENT CASE: Strict refusal mode
                    enhancedSystemPrompt = $@"{originalSystemMessage.Content}

🚨 KHÔNG CÓ TÀI LIỆU NỘI BỘ - CHẾ ĐỘ TỪ CHỐI NGHIÊM NGẶT

🔒 QUY TẮC TUYỆT ĐỐI:
- Hệ thống KHÔNG tìm thấy tài liệu nội bộ nào liên quan đến câu hỏi
- BẠN PHẢI TỪ CHỐI trả lời MỌI câu hỏi về nội dung, quy định, thông tin cụ thể
- TUYỆT ĐỐI KHÔNG được sử dụng kiến thức chung hoặc kiến thức bên ngoài
- KHÔNG được trả lời bằng cách ""giúp đỡ"" với thông tin chung

🚫 CHỈ ĐƯỢC TRẢ LỜI DUY NHẤT:
""Xin lỗi, tôi không tìm thấy tài liệu nội bộ nào liên quan đến câu hỏi này. Tôi chỉ có thể trả lời các câu hỏi dựa trên tài liệu nội bộ của công ty. Bạn có thể liên hệ bộ phận quản lý tài liệu để được hỗ trợ.""

⛔ TUYỆT ĐỐI KHÔNG ĐƯỢC:
❌ Trả lời về bất kỳ chủ đề nào (kể cả câu hỏi đơn giản như ""trứng gà"", ""thời tiết"")
❌ Giải thích khái niệm, định nghĩa từ kiến thức chung
❌ Đưa ra ý kiến, thảo luận, phân tích
❌ Chào hỏi dài dòng hoặc hỏi ngược lại
❌ Đề xuất giải pháp thay thế
❌ Thể hiện sự hiểu biết về bất kỳ vấn đề nào ngoài tài liệu nội bộ

🔴 BẮT BUỘC: Chỉ trả lời đúng câu từ chối ở trên, KHÔNG THÊM BỚT!";
                }

                enhancedHistory.AddSystemMessage(enhancedSystemPrompt);
            }

            foreach (var message in cleanHistory.Where(m => m.Role != AuthorRole.System))
            {
                enhancedHistory.Add(message);
            }

            return enhancedHistory;
        }

        /// <summary>
        /// ✅ ENHANCED: BuildCompleteDocumentPackage with comprehensive instructions
        /// </summary>
        private string BuildCompleteDocumentPackage(string documentContent, List<DocumentInfo> documentSources, string currentQuestion)
        {
            var package = new StringBuilder();
            var userContext = GetUserContextFromJWT();

            // ✅ 1. METADATA SECTION với detailed statistics
            if (documentSources?.Any() == true)
            {
                package.AppendLine("📋 **THỐNG KÊ TÀI LIỆU TỔNG QUAN:**");
                package.AppendLine("(AI PHẢI SỬ DỤNG SỐ LIỆU NÀY KHI TRẢ LỜI CÂU HỎI ĐẾM)");

                // ✅ DETAILED statistics for AI
                var totalDocs = documentSources.Count;
                var publicDocs = documentSources.Count(s => s.IsPublic);
                var privateDocs = documentSources.Count(s => !s.IsPublic);
                var myDeptDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName);
                var myDeptPublicDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName && s.IsPublic);
                var myDeptPrivateDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName && !s.IsPublic);

                // ✅ Document type statistics
                var docTypes = documentSources
                    .Where(s => !string.IsNullOrEmpty(s.DocumentType) && s.DocumentType != "Không rõ")
                    .GroupBy(s => s.DocumentType)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ✅ Department statistics  
                var deptStats = documentSources
                    .Where(s => !string.IsNullOrEmpty(s.DepartmentName))
                    .GroupBy(s => s.DepartmentName)
                    .ToDictionary(g => g.Key, g => g.Count());

                package.AppendLine($"📊 **Tổng số tài liệu:** {totalDocs}");
                package.AppendLine($"🔓 **Tài liệu PUBLIC (công khai):** {publicDocs}");
                package.AppendLine($"🔒 **Tài liệu PRIVATE (nội bộ):** {privateDocs}");
                package.AppendLine($"🏢 **Tài liệu phòng ban của tôi ({userContext.DepartmentName}):** {myDeptDocs}");
                package.AppendLine($"🔓🏢 **Tài liệu PUBLIC phòng ban của tôi:** {myDeptPublicDocs}");
                package.AppendLine($"🔒🏢 **Tài liệu PRIVATE phòng ban của tôi:** {myDeptPrivateDocs}");

                if (docTypes.Any())
                {
                    package.AppendLine("📋 **Thống kê theo loại tài liệu:**");
                    foreach (var docType in docTypes.OrderByDescending(x => x.Value))
                    {
                        package.AppendLine($"   • {docType.Key}: {docType.Value} tài liệu");
                    }
                }

                if (deptStats.Any())
                {
                    package.AppendLine("🏢 **Thống kê theo phòng ban:**");
                    foreach (var dept in deptStats.OrderByDescending(x => x.Value))
                    {
                        var isMine = dept.Key == userContext.DepartmentName ? " (PHÒNG BAN CỦA TÔI)" : "";
                        package.AppendLine($"   • {dept.Key}: {dept.Value} tài liệu{isMine}");
                    }
                }

                package.AppendLine();
                package.AppendLine(new string('=', 70));
                package.AppendLine();

                package.AppendLine("📋 **CHI TIẾT TỪNG TÀI LIỆU:**");
                package.AppendLine("(AI PHẢI KIỂM TRA TẤT CẢ TÀI LIỆU DƯỚI ĐÂY, KHÔNG CHỈ TÀI LIỆU ĐẦU TIÊN)");

                // ✅ 2. CHI TIẾT TỪNG TÀI LIỆU với full information
                for (int i = 0; i < documentSources.Count; i++)
                {
                    var source = documentSources[i];
                    package.AppendLine($"📄 **TÀI LIỆU {i + 1}:**");
                    package.AppendLine($"   **Tên:** {source.Title ?? "Không rõ"}");

                    // ✅ Summary và Description ở vị trí prominent
                    if (!string.IsNullOrWhiteSpace(source.Summary))
                        package.AppendLine($"   📝 **Tóm tắt:** {source.Summary.Trim()}");

                    if (!string.IsNullOrWhiteSpace(source.Description))
                        package.AppendLine($"   📖 **Mô tả chi tiết:** {source.Description.Trim()}");

                    if (source.Tags?.Any() == true && source.Tags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
                    {
                        var validTags = source.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();
                        package.AppendLine($"   🏷️ **Từ khóa/Tags:** {string.Join(", ", validTags)}");
                    }

                    // ✅ Access and department info
                    var visibility = source.IsPublic ? "PUBLIC (Công khai - ai cũng xem được)" : "PRIVATE (Nội bộ - hạn chế truy cập)";
                    var isMyDept = source.DepartmentName == userContext.DepartmentName;
                    var deptInfo = isMyDept ? "✅ PHÒNG BAN CỦA TÔI" : "❌ PHÒNG BAN KHÁC";

                    package.AppendLine($"   🔓 **Quyền truy cập:** {visibility}");
                    package.AppendLine($"   🏢 **Phòng ban:** {source.DepartmentName} ({deptInfo})");

                    // ✅ CLASSIFICATION rõ ràng cho AI
                    if (source.IsPublic && isMyDept)
                        package.AppendLine($"   🎯 **PHÂN LOẠI: PUBLIC + PHÒNG BAN CỦA TÔI** 🎯");
                    else if (!source.IsPublic && isMyDept)
                        package.AppendLine($"   🎯 **PHÂN LOẠI: PRIVATE + PHÒNG BAN CỦA TÔI** 🎯");
                    else if (source.IsPublic && !isMyDept)
                        package.AppendLine($"   🎯 **PHÂN LOẠI: PUBLIC + PHÒNG BAN KHÁC** 🎯");
                    else
                        package.AppendLine($"   🎯 **PHÂN LOẠI: PRIVATE + PHÒNG BAN KHÁC** 🎯");

                    // ✅ Document classification
                    if (!string.IsNullOrEmpty(source.DocumentType) && source.DocumentType != "Không rõ")
                        package.AppendLine($"   📋 **Loại tài liệu:** {source.DocumentType}");
                    if (!string.IsNullOrEmpty(source.Category) && source.Category != "Không rõ")
                        package.AppendLine($"   📂 **Danh mục:** {source.Category}");
                    if (!string.IsNullOrEmpty(source.Status))
                        package.AppendLine($"   📊 **Trạng thái:** {source.Status}");
                    if (!string.IsNullOrEmpty(source.Priority) && source.Priority != "Không rõ")
                        package.AppendLine($"   ⭐ **Mức độ ưu tiên:** {source.Priority}");

                    // ✅ People information
                    if (!string.IsNullOrEmpty(source.SignedBy) && source.SignedBy != "Không rõ")
                        package.AppendLine($"   🔴 **Người ký:** {source.SignedBy.ToUpper()}");
                    if (!string.IsNullOrEmpty(source.ApprovedBy) && source.ApprovedBy != "Không rõ")
                        package.AppendLine($"   ✅ **Người phê duyệt:** {source.ApprovedBy}");
                    if (!string.IsNullOrEmpty(source.ReviewerName) && source.ReviewerName != "Không rõ")
                        package.AppendLine($"   👁️ **Người xem xét:** {source.ReviewerName}");
                    if (!string.IsNullOrEmpty(source.CreatedBy) && source.CreatedBy != "Không rõ")
                        package.AppendLine($"   👤 **Người tạo:** {source.CreatedBy}");
                    if (!string.IsNullOrEmpty(source.OwnerName) && source.OwnerName != "Không rõ")
                        package.AppendLine($"   👑 **Chủ sở hữu:** {source.OwnerName}");

                    // ✅ Date information
                    if (source.ApprovalDate.HasValue)
                        package.AppendLine($"   📅 **Ngày phê duyệt:** {source.ApprovalDate.Value:dd/MM/yyyy}");
                    if (source.SignedDate.HasValue)
                        package.AppendLine($"   📅 **Ngày ký:** {source.SignedDate.Value:dd/MM/yyyy}");
                    if (source.ReviewDate.HasValue)
                        package.AppendLine($"   📅 **Ngày xem xét:** {source.ReviewDate.Value:dd/MM/yyyy}");
                    if (source.EffectiveFrom.HasValue)
                        package.AppendLine($"   ⏰ **Có hiệu lực từ:** {source.EffectiveFrom.Value:dd/MM/yyyy}");
                    if (source.EffectiveUntil.HasValue)
                        package.AppendLine($"   ⏰ **Hết hiệu lực:** {source.EffectiveUntil.Value:dd/MM/yyyy}");

                    // ✅ File and version info
                    if (!string.IsNullOrEmpty(source.VersionName))
                        package.AppendLine($"   🔢 **Phiên bản:** {source.VersionName}");
                    if (source.IsLatestVersion)
                        package.AppendLine($"   🆕 **Phiên bản mới nhất:** Có");
                    if (source.FileSize.HasValue)
                        package.AppendLine($"   📁 **Kích thước:** {source.FileSize.Value / 1024.0:F1} KB");
                    if (!string.IsNullOrEmpty(source.FileType))
                        package.AppendLine($"   📄 **Định dạng:** {source.FileType}");
                    if (!string.IsNullOrEmpty(source.FileName))
                        package.AppendLine($"   📎 **Tên file:** {source.FileName}");

                    // ✅ Relevance score for AI
                    if (source.RelevanceScore > 0)
                        package.AppendLine($"   🎯 **Độ liên quan với câu hỏi:** {source.RelevanceScore:F3}/1.000 (cao = phù hợp hơn)");

                    package.AppendLine(); // Separator
                }

                package.AppendLine(new string('=', 70));
                package.AppendLine();
            }

            // ✅ 3. USER CONTEXT
            package.AppendLine("👤 **THÔNG TIN NGƯỜI DÙNG HIỆN TẠI:**");
            package.AppendLine($"🏢 **Phòng ban của tôi:** {userContext.DepartmentName ?? "Không rõ"}");
            package.AppendLine($"📂 **Mã phòng ban:** {userContext.DepartmentId ?? "Không rõ"}");
            package.AppendLine($"👤 **Vai trò/Chức vụ:** {userContext.Role ?? "Không rõ"}");
            package.AppendLine($"👤 **Họ tên đầy đủ:** {userContext.FullName ?? "Không rõ"}");
            package.AppendLine($"📧 **Email:** {userContext.Email ?? "Không rõ"}");
            package.AppendLine();

            // ✅ 4. DOCUMENT CONTENT
            if (!string.IsNullOrEmpty(documentContent))
            {
                package.AppendLine("📄 **NỘI DUNG TÀI LIỆU HOÀN CHỈNH:**");
                package.AppendLine("(AI CHỈ ĐƯỢC DÙNG THÔNG TIN TRONG PHẦN NÀY ĐỂ TRẢ LỜI VỀ NỘI DUNG)");
                var organizedContent = OrganizeContentForQuestion(documentContent, currentQuestion);
                package.AppendLine(organizedContent);
                package.AppendLine();
                package.AppendLine(new string('=', 70));
                package.AppendLine();
            }

            // ✅ 6. COMPREHENSIVE INSTRUCTIONS
            package.AppendLine(BuildComprehensiveInstructions());

            return package.ToString();
        }

        /// <summary>
        /// ✅ NEW: Comprehensive instructions for all question types
        /// </summary>
        private string BuildComprehensiveInstructions()
        {
            var instructions = new StringBuilder();

            instructions.AppendLine("🗺️ **HƯỚNG DẪN XỬ LÝ TẤT CẢ LOẠI CÂU HỎI:**");
            instructions.AppendLine();

            instructions.AppendLine("🔢 **A. CÂU HỎI ĐẾM SỐ LƯỢNG:**");
            instructions.AppendLine("Từ khóa: 'có bao nhiêu', 'có mấy', 'tổng cộng', 'số lượng'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Đọc 'THỐNG KÊ TÀI LIỆU TỔNG QUAN' để lấy số chính xác");
            instructions.AppendLine("   2. Kiểm tra 'CHI TIẾT TỪNG TÀI LIỆU' để xác nhận");
            instructions.AppendLine("   3. Format: 'Có [SỐ] tài liệu [LOẠI]: 1. [Tên 1], 2. [Tên 2]... Tổng: [SỐ] tài liệu.'");
            instructions.AppendLine("⚠️ **Cấm:** Đếm sai, bỏ qua tài liệu, chỉ nhìn tài liệu đầu tiên");
            instructions.AppendLine();

            instructions.AppendLine("📄 **B. CÂU HỎI VỀ NỘI DUNG:**");
            instructions.AppendLine("Từ khóa: 'điều X nói gì', 'quy định về', 'thủ tục', 'quy trình'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Tìm trong 'NỘI DUNG TÀI LIỆU HOÀN CHỈNH'");
            instructions.AppendLine("   2. Ưu tiên: Summary → Description → Content chi tiết");
            instructions.AppendLine("   3. Trích dẫn chính xác, KHÔNG thêm giải thích từ kiến thức chung");
            instructions.AppendLine("   4. Format: 'Theo tài liệu [TÊN]: [NỘI DUNG CHÍNH XÁC] [Trích từ...]'");
            instructions.AppendLine("⚠️ **Cấm:** Giải thích bằng kiến thức bên ngoài, bịa đặt thông tin");
            instructions.AppendLine();

            instructions.AppendLine("📋 **C. CÂU HỎI VỀ METADATA:**");
            instructions.AppendLine("Từ khóa: 'ai ký', 'khi nào hiệu lực', 'thuộc phòng nào', 'trạng thái'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Tìm trong 'CHI TIẾT TỪNG TÀI LIỆU' phần thông tin cụ thể");
            instructions.AppendLine("   2. Trả lời chính xác thông tin có sẵn");
            instructions.AppendLine("   3. Nếu không có → 'Không có thông tin về [YÊU CẦU] trong tài liệu'");
            instructions.AppendLine("⚠️ **Cấm:** Đoán thông tin, sử dụng thông tin từ tài liệu khác");
            instructions.AppendLine();

            instructions.AppendLine("🔍 **D. CÂU HỎI TÌM KIẾM THEO CHỦ ĐỀ:**");
            instructions.AppendLine("Từ khóa: 'tài liệu về HR', 'quy định bảo mật', 'hợp đồng lao động'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Kiểm tra Tags → Title → Summary → Content");
            instructions.AppendLine("   2. Sắp xếp theo RelevanceScore (nếu có)");
            instructions.AppendLine("   3. Format: 'Có [SỐ] tài liệu về [CHỦ ĐỀ]: 1. [Tên] - [Summary ngắn]...'");
            instructions.AppendLine("⚠️ **Cấm:** Đưa ra tài liệu không liên quan, bỏ qua Tags");
            instructions.AppendLine();

            instructions.AppendLine("🔗 **E. CÂU HỎI SO SÁNH:**");
            instructions.AppendLine("Từ khóa: 'khác biệt giữa', 'so sánh', 'tài liệu nào mới hơn'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Lấy thông tin từ 'CHI TIẾT TỪNG TÀI LIỆU' của từng tài liệu được so sánh");
            instructions.AppendLine("   2. So sánh dựa trên thông tin có sẵn (ngày, nội dung, metadata)");
            instructions.AppendLine("   3. Format: 'So sánh [A] vs [B]: [A] có [ĐẶC ĐIỂM], [B] có [ĐẶC ĐIỂM]'");
            instructions.AppendLine("⚠️ **Cấm:** Đưa ra nhận xét chủ quan, so sánh bằng kiến thức chung");
            instructions.AppendLine();

            instructions.AppendLine("📖 **F. CÂU HỎI TỔNG QUAN/TÓM TẮT:**");
            instructions.AppendLine("Từ khóa: 'tóm tắt tài liệu', 'nội dung chính', 'điểm quan trọng'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Sử dụng Summary (nếu có) làm cơ sở chính");
            instructions.AppendLine("   2. Bổ sung từ Description và điểm chính trong Content");
            instructions.AppendLine("   3. Format: 'Tóm tắt [TÊN]: [SUMMARY]. Chi tiết: [KEY POINTS] [Trích từ...]'");
            instructions.AppendLine("⚠️ **Cấm:** Thêm ý kiến cá nhân, giải thích bằng kiến thức bên ngoài");
            instructions.AppendLine();

            instructions.AppendLine("🔐 **G. CÂU HỎI VỀ QUYỀN TRUY CẬP:**");
            instructions.AppendLine("Từ khóa: 'tôi có thể xem', 'public hay private', 'phòng ban tôi có gì'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Kiểm tra IsPublic + DepartmentName so với THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine("   2. Phân loại: PUBLIC+PHÒNG BAN CỦA TÔI, PRIVATE+PHÒNG BAN CỦA TÔI, etc.");
            instructions.AppendLine("   3. Format: 'Bạn [CÓ/KHÔNG THỂ] truy cập vì [LÝ DO CỤ THỂ]'");
            instructions.AppendLine("⚠️ **Cấm:** Đoán quyền truy cập, bỏ qua phân quyền");
            instructions.AppendLine();

            instructions.AppendLine("💡 **H. CÂU HỎI GỢI Ý/KHUYẾN NGHỊ:**");
            instructions.AppendLine("Từ khóa: 'nên đọc tài liệu nào', 'liên quan đến vấn đề X'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Dùng RelevanceScore + Tags + Summary để đánh giá");
            instructions.AppendLine("   2. Ưu tiên tài liệu có điểm cao và phù hợp với user context");
            instructions.AppendLine("   3. Format: 'Gợi ý: [TÀI LIỆU] (Độ liên quan: [SCORE]) vì [LÝ DO DỰA TRÊN TÀI LIỆU]'");
            instructions.AppendLine("⚠️ **Cấm:** Đưa ra gợi ý không dựa trên dữ liệu có sẵn");
            instructions.AppendLine();

            instructions.AppendLine("👤 **I. CÂU HỎI TÌM THEO NGƯỜI:**");
            instructions.AppendLine("Từ khóa: 'tài liệu do tôi tạo', 'do [TÊN] ký', 'do [TÊN] phê duyệt', 'do [TÊN] xem xét'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Tài liệu do tôi tạo**: So sánh CreatedBy với FullName trong THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine("   2. **Do [TÊN] ký**: Tìm trong SignedBy có chứa tên được hỏi");
            instructions.AppendLine("   3. **Do [TÊN] phê duyệt**: Tìm trong ApprovedBy có chứa tên được hỏi");
            instructions.AppendLine("   4. **Do [TÊN] xem xét**: Tìm trong ReviewerName có chứa tên được hỏi");
            instructions.AppendLine("   5. **Do [TÊN] tạo**: Tìm trong CreatedBy có chứa tên được hỏi");
            instructions.AppendLine("   6. Format: 'Có [SỐ] tài liệu do [TÊN/bạn] [HÀNH ĐỘNG]: 1. [Tên tài liệu]...'");
            instructions.AppendLine("⚠️ **Cấm:** So sánh không chính xác, bỏ qua tài liệu");
            instructions.AppendLine();

            instructions.AppendLine("📅 **J. CÂU HỎI TÌM THEO THỜI GIAN:**");
            instructions.AppendLine("Từ khóa: 'tài liệu hiệu lực năm 2024', 'ký trong tháng X', 'hết hạn khi nào'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Hiệu lực**: Kiểm tra EffectiveFrom và EffectiveUntil");
            instructions.AppendLine("   2. **Ngày ký**: Kiểm tra SignedDate");
            instructions.AppendLine("   3. **Ngày duyệt**: Kiểm tra ApprovalDate");
            instructions.AppendLine("   4. **Ngày xem xét**: Kiểm tra ReviewDate");
            instructions.AppendLine("   5. Format: 'Có [SỐ] tài liệu [ĐIỀU KIỆN THỜI GIAN]: 1. [Tên] - [Ngày cụ thể]...'");
            instructions.AppendLine("⚠️ **Cấm:** Tính toán sai thời gian, đoán ngày tháng");
            instructions.AppendLine();

            instructions.AppendLine("🔧 **K. CÂU HỎI PHỨC TẠP/KẾT HỢP:**");
            instructions.AppendLine("Ví dụ: 'Có bao nhiêu tài liệu HR public và nội dung chính là gì?'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. Chia câu hỏi thành từng phần nhỏ");
            instructions.AppendLine("   2. Xử lý từng phần theo hướng dẫn tương ứng (A,B,C,I,J...)");
            instructions.AppendLine("   3. Tổng hợp kết quả một cách logic");
            instructions.AppendLine("⚠️ **Cấm:** Bỏ qua bất kỳ phần nào của câu hỏi");
            instructions.AppendLine();

            instructions.AppendLine("🤔 **L. CÂU HỎI MƠ HỒ/KHÔNG RÕ:**");
            instructions.AppendLine("Ví dụ: 'Tài liệu này thế nào?', 'Nói về cái đó', 'Giải thích thêm'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Reference không rõ**: 'Bạn có thể nói rõ hơn muốn hỏi về tài liệu nào không?'");
            instructions.AppendLine("   2. **Câu hỏi mơ hồ**: 'Xin lỗi, câu hỏi chưa rõ ràng. Bạn muốn biết thông tin gì cụ thể?'");
            instructions.AppendLine("   3. **Context thiếu**: Tham khảo chat history để hiểu context");
            instructions.AppendLine("   4. Format: 'Để trả lời chính xác, bạn có thể làm rõ [YÊU CẦU CỤ THỂ] không?'");
            instructions.AppendLine("⚠️ **Cấm:** Đoán ý định, trả lời mơ hồ");
            instructions.AppendLine();

            instructions.AppendLine("🔗 **M. CÂU HỎI REFERENCE/CONTEXT:**");
            instructions.AppendLine("Ví dụ: 'Cái này hiệu lực chưa?', 'Họ ký khi nào?', 'Document đó nói gì?'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Kiểm tra chat history**: Tìm tài liệu được mention gần nhất");
            instructions.AppendLine("   2. **'Này/đó/cái này'**: Dùng tài liệu được thảo luận trong tin nhắn trước");
            instructions.AppendLine("   3. **'Họ/người đó'**: Dùng tên người được mention trước đó");
            instructions.AppendLine("   4. **Không tìm thấy context**: 'Bạn đang hỏi về tài liệu/người nào cụ thể?'");
            instructions.AppendLine("   5. Format: 'Về [TÀI LIỆU ĐÃ THẢO LUẬN], [TRẢ LỜI CỤ THỂ]'");
            instructions.AppendLine("⚠️ **Cấm:** Đoán tài liệu/người không đúng");
            instructions.AppendLine();

            instructions.AppendLine("🎯 **N. CÂU HỎI ĐÁNH GIÁ/KHUYẾN NGHỊ:**");
            instructions.AppendLine("Ví dụ: 'Tài liệu nào quan trọng?', 'Nên làm theo cái nào?', 'Ưu tiên thế nào?'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Dựa trên RelevanceScore**: Tài liệu có điểm cao hơn = liên quan hơn");
            instructions.AppendLine("   2. **Dựa trên Status**: 'Đang hiệu lực' > 'Hết hiệu lực'");
            instructions.AppendLine("   3. **Dựa trên ngày**: Tài liệu mới hơn = update hơn (nếu cùng loại)");
            instructions.AppendLine("   4. **Dựa trên Priority**: Nếu có thông tin Priority trong metadata");
            instructions.AppendLine("   5. Format: 'Dựa trên [TIÊU CHÍ], gợi ý [TÀI LIỆU] vì [LÝ DO CỤ THỂ]'");
            instructions.AppendLine("⚠️ **Cấm:** Đánh giá chủ quan, không có căn cứ");
            instructions.AppendLine();

            instructions.AppendLine("📊 **O. CÂU HỎI PHÂN TÍCH/PROCESS:**");
            instructions.AppendLine("Ví dụ: 'Quy trình có mấy bước?', 'Bước tiếp theo?', 'Workflow như nào?'");
            instructions.AppendLine("📝 **Quy trình bắt buộc:**");
            instructions.AppendLine("   1. **Tìm trong content**: Đếm các bước/giai đoạn được liệt kê");
            instructions.AppendLine("   2. **Phân tích cấu trúc**: 'Bước 1:', 'Giai đoạn 1:', 'Thứ nhất:'");
            instructions.AppendLine("   3. **Trích dẫn chính xác**: Không tự sáng tác các bước");
            instructions.AppendLine("   4. Format: 'Theo tài liệu, quy trình có [SỐ] bước: 1. [BƯỚC 1]...'");
            instructions.AppendLine("⚠️ **Cấm:** Sáng tác bước không có trong tài liệu");
            instructions.AppendLine();

            instructions.AppendLine("🎯 **QUY TẮC ƯU TIÊN THÔNG TIN:**");
            instructions.AppendLine("1. **Đếm số lượng:** THỐNG KÊ TỔNG QUAN → Verify bằng CHI TIẾT TỪNG TÀI LIỆU");
            instructions.AppendLine("2. **Nội dung:** Summary → Description → Content → KHÔNG ĐƯỢC dùng kiến thức ngoài");
            instructions.AppendLine("3. **Metadata:** CHI TIẾT TỪNG TÀI LIỆU → KHÔNG đoán thiếu thông tin");
            instructions.AppendLine("4. **Tìm kiếm:** Tags → Title → Summary → Content");
            instructions.AppendLine("5. **Quyền truy cập:** IsPublic + DepartmentName + THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine();

            instructions.AppendLine("🚫 **DANH SÁCH CẤM TUYỆT ĐỐI:**");
            instructions.AppendLine("❌ Sử dụng bất kỳ kiến thức nào NGOÀI 'THÔNG TIN TÀI LIỆU HOÀN CHỈNH'");
            instructions.AppendLine("❌ Bịa đặt số liệu, tên người, ngày tháng, quy định");
            instructions.AppendLine("❌ Giải thích khái niệm bằng kiến thức chung (HR, IT, pháp lý...)");
            instructions.AppendLine("❌ Đề xuất tìm kiếm internet, liên hệ cơ quan, nguồn bên ngoài");
            instructions.AppendLine("❌ Nói 'theo kinh nghiệm', 'thường thì', 'dựa trên thông lệ'");
            instructions.AppendLine("❌ Đưa ra lời khuyên không có trong tài liệu");
            instructions.AppendLine("❌ Đếm sai hoặc bỏ qua tài liệu khi liệt kê");
            instructions.AppendLine("❌ Thêm thông tin 'để đầy đủ hơn' nếu không có trong tài liệu");
            instructions.AppendLine("❌ Trả lời mơ hồ khi có thông tin rõ ràng");
            instructions.AppendLine("❌ Sử dụng thông tin từ tài liệu này để trả lời về tài liệu khác");
            instructions.AppendLine();

            instructions.AppendLine("✅ **CHỈ ĐƯỢC PHÉP:**");
            instructions.AppendLine("✓ Thông tin 100% có trong 'THÔNG TIN TÀI LIỆU HOÀN CHỈNH'");
            instructions.AppendLine("✓ Trích dẫn chính xác từ Summary, Description, Content, Metadata");
            instructions.AppendLine("✓ Đếm và liệt kê dựa trên 'CHI TIẾT TỪNG TÀI LIỆU'");
            instructions.AppendLine("✓ Tham khảo chat history để hiểu context câu hỏi");
            instructions.AppendLine("✓ Nói 'Không có thông tin về [YÊU CẦU] trong tài liệu' khi thiếu thông tin");
            instructions.AppendLine("✓ Trả lời 'Xin lỗi, câu hỏi này không rõ ràng' nếu không hiểu");
            instructions.AppendLine();

            instructions.AppendLine("📝 **TEMPLATE TRẢ LỜI CHUẨN:**");
            instructions.AppendLine();
            instructions.AppendLine("**Đếm số lượng:**");
            instructions.AppendLine("'Có [SỐ CHÍNH XÁC] tài liệu [LOẠI] [PHẠM VI]:");
            instructions.AppendLine("1. [Tên tài liệu 1] - [Summary/mô tả ngắn nếu có]");
            instructions.AppendLine("2. [Tên tài liệu 2] - [Summary/mô tả ngắn nếu có]");
            instructions.AppendLine("Tổng cộng: [SỐ] tài liệu.'");
            instructions.AppendLine();
            instructions.AppendLine("**Tìm theo người:**");
            instructions.AppendLine("'Có [SỐ] tài liệu do [TÊN/bạn] [HÀNH ĐỘNG]:");
            instructions.AppendLine("1. [Tên tài liệu 1] - [Ngày thực hiện nếu có]");
            instructions.AppendLine("2. [Tên tài liệu 2] - [Ngày thực hiện nếu có]");
            instructions.AppendLine("Tổng cộng: [SỐ] tài liệu.'");
            instructions.AppendLine();
            instructions.AppendLine("**Câu hỏi mơ hồ:**");
            instructions.AppendLine("'Để trả lời chính xác, bạn có thể làm rõ [YÊU CẦU CỤ THỂ] không?'");
            instructions.AppendLine();
            instructions.AppendLine("**Câu hỏi reference:**");
            instructions.AppendLine("'Về [TÀI LIỆU ĐÃ THẢO LUẬN TRƯỚC ĐÓ]: [TRẢ LỜI CỤ THỂ]'");
            instructions.AppendLine();
            instructions.AppendLine("**Câu hỏi đánh giá:**");
            instructions.AppendLine("'Dựa trên [TIÊU CHÍ CỤ THỂ], gợi ý [TÀI LIỆU] vì [LÝ DO TRONG TÀI LIỆU]'");
            instructions.AppendLine();
            instructions.AppendLine("**Câu hỏi process:**");
            instructions.AppendLine("'Theo tài liệu, quy trình có [SỐ] bước:");
            instructions.AppendLine("1. [BƯỚC 1 CHÍNH XÁC TỪ TÀI LIỆU]");
            instructions.AppendLine("2. [BƯỚC 2 CHÍNH XÁC TỪ TÀI LIỆU]...'");
            instructions.AppendLine();
            instructions.AppendLine("**Nội dung:**");
            instructions.AppendLine("'Theo tài liệu \"[TÊN TÀI LIỆU]\": [NỘI DUNG CHÍNH XÁC]");
            instructions.AppendLine("Chi tiết: [TRÍCH DẪN CỤ THỂ]");
            instructions.AppendLine("[Trích từ tài liệu: [TÊN]]'");
            instructions.AppendLine();
            instructions.AppendLine("**Metadata:**");
            instructions.AppendLine("'[THÔNG TIN YÊU CẦU] của tài liệu \"[TÊN]\": [GIÁ TRỊ CHÍNH XÁC]");
            instructions.AppendLine("Nguồn: [CHI TIẾT TỪNG TÀI LIỆU]'");
            instructions.AppendLine();
            instructions.AppendLine("**Không có thông tin:**");
            instructions.AppendLine("'Không có thông tin về [YÊU CẦU CỤ THỂ] trong các tài liệu hiện có.'");

            return instructions.ToString();
        }

        /// <summary>
        /// ✅ ENHANCED: System prompt building with AI configuration protection
        /// </summary>
        private async Task<string> GetAIConfigurationSystemPrompt(string modelName, string defaultPrompt)
        {
            var normalizedModelName = NormalizeModelName(modelName);
            var aiConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName && c.IsActive);

            if (aiConfig?.SystemPrompt != null)
            {
                // ✅ AI Configuration được thêm TRƯỚC base prompt để không bị override
                // Base prompt sẽ chứa document rules và sẽ có quyền ưu tiên cao hơn
                return $"{aiConfig.SystemPrompt}\n\n--- Base System Configuration ---\n{defaultPrompt}";
            }

            return defaultPrompt;
        }

        /// <summary>
        /// ✅ SIMPLE: Content organization without keyword detection
        /// </summary>
        private string OrganizeContentForQuestion(string content, string question)
        {
            if (string.IsNullOrEmpty(content)) return "";

            var questionLower = question?.ToLowerInvariant() ?? "";
            var organizedContent = new StringBuilder();

            // ✅ Only highlight specific article/chapter numbers - no keyword extraction
            if (Regex.IsMatch(questionLower, @"điều\s+\d+"))
            {
                var articleMatch = Regex.Match(questionLower, @"điều\s+(\d+)");
                if (articleMatch.Success)
                {
                    var articleNumber = articleMatch.Groups[1].Value;
                    organizedContent.AppendLine($"🎯 **USER HỎI VỀ ĐIỀU {articleNumber}** - AI tìm thông tin này trong nội dung:");
                    organizedContent.AppendLine();
                }
            }
            else if (Regex.IsMatch(questionLower, @"chương\s+\d+"))
            {
                var chapterMatch = Regex.Match(questionLower, @"chương\s+(\d+)");
                if (chapterMatch.Success)
                {
                    var chapterNumber = chapterMatch.Groups[1].Value;
                    organizedContent.AppendLine($"🎯 **USER HỎI VỀ CHƯƠNG {chapterNumber}** - AI tìm thông tin này trong nội dung:");
                    organizedContent.AppendLine();
                }
            }

            // ✅ Always provide full content - no keyword filtering
            organizedContent.AppendLine("📄 **NỘI DUNG ĐẦY ĐỦ CỦA TÀI LIỆU:**");
            organizedContent.AppendLine(content);

            return organizedContent.ToString();
        }
        private string GetActualSourceDocumentTitle(string documentContent, List<DocumentInfo> documentSources)
        {
            if (documentSources?.Any() == true)
            {
                var firstSource = documentSources.First();
                if (!string.IsNullOrWhiteSpace(firstSource.Title))
                {
                    return FormatDocumentTitle(firstSource.Title);
                }
            }
            return null;
        }
        private string FormatDocumentTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var formattedTitle = title.Trim();
            if (formattedTitle.Length > 80)
            {
                formattedTitle = formattedTitle.Substring(0, 77) + "...";
            }
            return formattedTitle;
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

        private async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string modelName, string userId, string documentId = null)
        {
            if (string.IsNullOrEmpty(sessionId))
                return await CreateNewSession(modelName, userId, documentId);

            var session = await GetExistingSession(sessionId, userId);

            // ✅ FIX: Thêm logic cập nhật DocumentId ở đây
            // Kiểm tra xem documentId có được cung cấp và có khác với cái đang được lưu không
            if (documentId != null && session.DocumentId != documentId)
            {
                _logger.LogInformation("Updating DocumentId for session {SessionId} from '{OldDocId}' to '{NewDocId}'",
                    sessionId, session.DocumentId ?? "N/A", documentId);

                session.DocumentId = documentId;
                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            }

            return session;
        }

        private async Task<ChatSession> CreateNewSession(string modelName, string userId, string documentId = null)
        {
            var validModelName = await DetermineModelForNewSession(modelName, userId);

            var newSession = new ChatSession
            {
                Title = ChatConstants.DefaultSessionTitle,
                UserId = userId,
                ModelName = validModelName,
                DocumentId = documentId,  // ✅ Store document ID
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
                LastActiveAt = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<ChatSession>().InsertAsync(newSession);
            await _unitOfWork.CommitAsync();

            var sessionType = string.IsNullOrEmpty(documentId) ? "GENERAL" : "DOCUMENT";
            _logger.LogInformation("Created new {SessionType} session {SessionId} for user {UserId} with model {ModelName}, documentId: {DocumentId}",
                sessionType, newSession.Id, userId, validModelName, documentId ?? "N/A");

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
        public async Task<bool> UpdateSessionTitleAsync(string sessionId, string title, string userId)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(title))
                    throw new ArgumentException("Title không được để trống");

                if (title.Length > ChatConstants.MaxTitleLength)
                    throw new ArgumentException($"Title không được vượt quá {ChatConstants.MaxTitleLength} ký tự");

                // Get session
                var session = await GetSessionByIdAndUser(sessionId, userId);
                if (session == null)
                    return false;

                // Update title
                session.Title = title.Trim();
                session.UpdatedAt = DateTime.UtcNow;
                session.UpdatedBy = userId;

                _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Updated session title: {SessionId} -> {Title}", sessionId, title);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session title for {SessionId}", sessionId);
                throw;
            }
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
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Title generation failed for session {SessionId}, using smart fallback", session.Id);
            }
            try
            {
                var smartTitle = GenerateSmartFallbackTitle(firstUserMessage);
                session.Title = smartTitle;
                _logger.LogInformation("Generated smart fallback title for session {SessionId}: {Title}", session.Id, smartTitle);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Smart fallback title generation failed for session {SessionId}", session.Id);
                session.Title = ChatConstants.DefaultSessionTitle;
            }
        }
        private string GenerateSmartFallbackTitle(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return ChatConstants.DefaultSessionTitle;

            try
            {
                var cleanMessage = userMessage.Trim();

                // Truncate if too long
                if (cleanMessage.Length > 100)
                {
                    cleanMessage = cleanMessage.Substring(0, 100);
                    // Find last complete word
                    var lastSpace = cleanMessage.LastIndexOf(' ');
                    if (lastSpace > 50)
                    {
                        cleanMessage = cleanMessage.Substring(0, lastSpace);
                    }
                    cleanMessage += "...";
                }

                // Remove question marks and common prefixes
                cleanMessage = cleanMessage
                    .Replace("?", "")
                    .Replace("!", "")
                    .Trim();

                // Remove common Vietnamese question starters
                var questionStarters = new[]
                {
            "bạn có thể", "bạn có", "làm thế nào", "làm sao",
            "tôi muốn", "tôi cần", "cho tôi", "giúp tôi",
            "xin chào", "chào bạn", "hello", "hi"
        };

                var lowerMessage = cleanMessage.ToLowerInvariant();
                foreach (var starter in questionStarters)
                {
                    if (lowerMessage.StartsWith(starter))
                    {
                        cleanMessage = cleanMessage.Substring(starter.Length).Trim();
                        break;
                    }
                }

                // Capitalize first letter
                if (!string.IsNullOrEmpty(cleanMessage))
                {
                    cleanMessage = char.ToUpperInvariant(cleanMessage[0]) + cleanMessage.Substring(1);
                }

                // Final validation
                if (string.IsNullOrWhiteSpace(cleanMessage) || cleanMessage.Length < 3)
                {
                    return "Cuộc trò chuyện mới";
                }

                return cleanMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in smart title generation fallback");
                return ChatConstants.DefaultSessionTitle;
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
                Summary = "",  // ❌ Reset thành empty
                VersionId = doc.VersionId,
                VersionName = doc.VersionName,
                DepartmentId = doc.DepartmentId,  
                DepartmentName = doc.DepartmentName,  
                ApprovedBy = doc.ApprovedBy,
                CreatedBy = doc.CreatedBy,
                SignedBy = doc.SignedBy,
                OwnerName = doc.OwnerName,        
                Description = null,  
                Tags = null,  
                EffectiveFrom = doc.EffectiveFrom,
                EffectiveUntil = doc.EffectiveUntil,
                ApprovalDate = doc.ApprovalDate,
                ReviewerName = doc.ReviewerName,
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
            var sourcesData = documentSources.Select(doc => new
            {
                DocumentId = doc.DocumentId,
                VersionName = doc.VersionName,
                Title = doc.Title,
                SignedBy = doc.SignedBy, 
                CreateBy = doc.CreatedBy,
                ReviewName = doc.ReviewerName,
                ApprovedBy =doc.ApprovedBy,
                DepartmentName = doc.DepartmentName,
                EffectiveFrom = doc.EffectiveFrom,
                EffectiveUntil = doc.EffectiveUntil,
                RelevanceScore = doc.RelevanceScore
            });
            sourcesString = JsonSerializer.Serialize(sourcesData);

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
                DocumentId = null, 
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
                     predicate: s => s.UserId == userId &&
                                   s.IsActive &&
                                   string.IsNullOrWhiteSpace(s.DocumentId), 
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
        private UserContextFromJWT GetDefaultUserContext()
        {
            return new UserContextFromJWT
            {
                UserId = "anonymous",
                Email = "anonymous@system.com",
                FullName = "Anonymous User",
                Phone = "",
                Role = "Guest",
                DepartmentId = "",
                DepartmentName = "",
                Permissions = new List<string>()
            };
        }

        private List<string> ParsePermissions(string permissionsString)
        {
            if (string.IsNullOrEmpty(permissionsString))
                return new List<string>();

            try
            {
                return permissionsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔐 [JWT] Failed to parse permissions: {Permissions}", permissionsString);
                return new List<string>();
            }
        }

        private UserContextFromJWT GetUserContextFromJWT()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("🔐 [JWT] User is not authenticated, using default context");
                    return GetDefaultUserContext();
                }

                var userContext = new UserContextFromJWT
                {
                    UserId = user.FindFirst("userId")?.Value ??
                             user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ??
                             string.Empty,
                    Email = user.FindFirst("email")?.Value ??
                            user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value ??
                            string.Empty,
                    FullName = user.FindFirst("fullName")?.Value ??
                               user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value ??
                               string.Empty,
                    Phone = user.FindFirst("phone")?.Value ?? string.Empty,
                    Role = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ??
                           user.FindFirst("role")?.Value ??
                           string.Empty,
                    DepartmentId = user.FindFirst("departmentId")?.Value ?? string.Empty,
                    DepartmentName = user.FindFirst("departmentName")?.Value ?? string.Empty,
                    Permissions = ParsePermissions(user.FindFirst("permissions")?.Value)
                };

                return userContext;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔐 [JWT] Failed to extract user context from JWT, using default");
                return GetDefaultUserContext();
            }
        }
        #endregion
    }

}