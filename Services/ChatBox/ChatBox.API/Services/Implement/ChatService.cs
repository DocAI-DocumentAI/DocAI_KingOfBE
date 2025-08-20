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
                    var versionInfo = documentSources?.FirstOrDefault()?.VersionId;
                    var versionSuffix = !string.IsNullOrEmpty(versionInfo) ? $" - Version: {versionInfo}" : "";

                    var citationSuffix = !string.IsNullOrEmpty(actualSourceDocumentTitle)
                        ? $"[Trích từ tài liệu: {actualSourceDocumentTitle}{versionSuffix}]"
                        : "[Trích từ tài liệu nội bộ]";

                    // ✅ STRICT: Base system prompt FIRST, then document-specific rules
                    enhancedSystemPrompt = $@"{originalSystemMessage.Content}

🔒 STRICT DOCUMENT EXPERT - ZERO TOLERANCE FOR VIOLATIONS 🔒

**CRITICAL LANGUAGE RULE:** Always respond in Vietnamese. Never mix languages.

**CURRENT QUESTION:** {currentQuestion}

**MANDATORY RELEVANCE CHECK - NO EXCEPTIONS:**
Before using ANY document, you MUST verify:
1. Does the document title/summary DIRECTLY match the question topic?
2. Is there a CLEAR, OBVIOUS connection between document content and question?
3. Would a normal person immediately understand why this document helps answer this question?

**IF ANY ANSWER IS NO → DO NOT USE THE DOCUMENT**

**SEMANTIC RELEVANCE TEST:**
- Question: {currentQuestion}
- For each document: Check title, summary, tags
- ONLY use documents where connection is IMMEDIATELY OBVIOUS
- If connection requires explanation → DO NOT USE

**ABSOLUTE VIOLATIONS DETECTED - STRICTLY FORBIDDEN:**

❌ **VIOLATION 1: Using irrelevant documents**
Example of FORBIDDEN behavior: Using lương tối thiểu document to answer nghỉ phép question
CORRECT: Say Không có thông tin về nghỉ phép trong tài liệu

❌ **VIOLATION 2: Adding general knowledge**
Example of FORBIDDEN behavior: Mentioning Bộ luật Lao động, Điều 113, or any external legal references
CORRECT: Only use information from provided documents

❌ **VIOLATION 3: Suggesting alternative sources**
Example of FORBIDDEN behavior: tôi khuyến nghị bạn tham khảo..., bạn có thể xem..., tìm hiểu thêm tại...
CORRECT: Only state what you found or didn't find in provided documents

❌ **VIOLATION 4: Implicit recommendations**
Example of FORBIDDEN behavior: Nếu bạn cần thông tin cụ thể về X, hãy tham khảo Y
CORRECT: Không có thông tin về X trong tài liệu hiện có

**ONLY ALLOWED RESPONSES:**

✅ **When relevant document found:**
Theo tài liệu '[EXACT_TITLE]':
[EXACT_CONTENT_FROM_DOCUMENT_ONLY]

{citationSuffix}

✅ **When no relevant document found:**
Không có thông tin về [TOPIC] trong các tài liệu hiện có.
**STOP IMMEDIATELY. DO NOT ADD ANYTHING ELSE.**

✅ **When partial information found:**
Dựa trên tài liệu '[EXACT_TITLE]', tôi có thông tin sau:
[EXACT_AVAILABLE_INFO]

Tuy nhiên, tài liệu không đề cập chi tiết về [MISSING_ASPECT].

**EXAMPLES OF CORRECT vs INCORRECT RESPONSES:**

🔴 **INCORRECT (like the detected violation):**
Theo tài liệu về lương tối thiểu, không có thông tin về nghỉ phép. Tuy nhiên, theo Bộ luật Lao động...

✅ **CORRECT:**
Không có thông tin về quy định nghỉ phép trong các tài liệu hiện có.

🔴 **INCORRECT:**
Tài liệu không đề cập. Tôi khuyến nghị bạn tham khảo...

✅ **CORRECT:**
Không có thông tin về [TOPIC] trong tài liệu.

**DECISION FLOWCHART:**
1. Read question: {currentQuestion}
2. Check each document title/summary
3. Is connection IMMEDIATELY OBVIOUS? 
   - YES → Use document with exact quotes only
   - NO → Không có thông tin về [TOPIC] trong các tài liệu hiện có
4. NEVER add external knowledge or suggestions

**ZERO TOLERANCE RULES:**
- NO general knowledge (laws, regulations, common practices)
- NO external references (Bộ luật, Nghị định, websites, etc.)
- NO suggestions to look elsewhere
- NO recommendations of any kind
- NO however, but, alternatively followed by external info
- NO advice not directly from provided documents

=== DOCUMENT INFORMATION ===
{completeDocumentInfo}
=== END DOCUMENT INFORMATION ===

**FINAL CHECKPOINT:**
Before responding, ask yourself:
1. Am I using ONLY information from provided documents?
2. Am I suggesting or recommending anything? (If YES → DELETE IT)
3. Am I adding any external knowledge? (If YES → DELETE IT)
4. Does my response ONLY contain what's in the documents or state no information found?

**RESPONSE MUST BE ONE OF THESE FORMATS ONLY:**

**Format 1 - Information Found:**
Theo tài liệu '[TITLE]':
[EXACT_QUOTE_FROM_DOCUMENT]

{citationSuffix}

**Format 2 - No Information:**
Không có thông tin về [TOPIC] trong các tài liệu hiện có.

**Format 3 - Partial Information:**
Dựa trên tài liệu '[TITLE]':
[EXACT_AVAILABLE_INFO]

Tuy nhiên, tài liệu không đề cập chi tiết về [MISSING_PART].

**NO OTHER FORMATS ALLOWED. NO EXCEPTIONS.**";
                }
                else
                {
                    // ✅ NO DOCUMENT: Friendly but clear refusal
                    enhancedSystemPrompt = $@"{originalSystemMessage.Content}

🤖 NO DOCUMENT MODE - STRICT GUIDELINES

**LANGUAGE:** Always respond in Vietnamese.

**SITUATION:** No internal documents found.

**ONLY ALLOWED RESPONSE:**
Tôi không tìm thấy tài liệu nội bộ nào liên quan đến '[REQUEST]'.

Tôi chỉ có thể trả lời dựa trên tài liệu nội bộ của công ty.

**ABSOLUTELY FORBIDDEN:**
- Give general knowledge answers
- Suggest external sources
- Provide legal or regulatory information
- Recommend where to find information";
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
        /// ✅ DOCUMENT URLS: MUST use real DocumentId like f286d69e9ee44e94ae916222cd3ae8fb, NOT [DocumentId]
        /// </summary>
        private string BuildCompleteDocumentPackage(string documentContent, List<DocumentInfo> documentSources, string currentQuestion)
        {
            var package = new StringBuilder();
            var userContext = GetUserContextFromJWT();
            package.AppendLine("🚨 CRITICAL INSTRUCTION: ZERO TOLERANCE FOR VIOLATIONS 🚨");
            package.AppendLine();
            package.AppendLine("**DETECTED VIOLATION PATTERN TO AVOID:**");
            package.AppendLine("❌ Saying 'tôi khuyến nghị bạn tham khảo...'");
            package.AppendLine("❌ Any form of suggestion or recommendation");
            package.AppendLine("**CORRECT BEHAVIOR:**");
            package.AppendLine("✅ If no relevant document → 'Không có thông tin về [TOPIC] trong tài liệu' and STOP");
            package.AppendLine("✅ If relevant document → Use ONLY that document's content");
            package.AppendLine("✅ NEVER add external knowledge or suggestions");
            package.AppendLine();
            // ✅ 1. ENHANCED DOCUMENT LINKS SECTION
            if (documentSources?.Any() == true)
            {
                package.AppendLine("🔗 **ĐƯỜNG DẪN TRUY CẬP TÀI LIỆU CHI TIẾT:**");
                package.AppendLine("(AI PHẢI SỬ DỤNG CHÍNH XÁC - KHÔNG TỰ TẠO LINK)");
                package.AppendLine();

                foreach (var source in documentSources.Take(10))
                {
                    if (!string.IsNullOrEmpty(source.DocumentId))
                    {
                        var accessBadge = source.IsPublic ? "🔓 PUBLIC" : "🔒 PRIVATE";
                        var deptBadge = source.DepartmentName == userContext.DepartmentName ? "🏢 PHÒNG TÔI" : $"🏢 {source.DepartmentName}";
                        var versionBadge = source.IsLatestVersion ? "🆕 MỚI NHẤT" : $"📊 v{source.VersionName}";

                        package.AppendLine($"📄 **{source.Title}**");
                        package.AppendLine($"   🎯 Status: {accessBadge} | {deptBadge} | {versionBadge}");
                        package.AppendLine($"   🔗 Link: https://docai.asia/document/{source.DocumentId}");
                        package.AppendLine($"   🆔 DocumentId: {source.DocumentId}");

                        // ✅ Add relevance and file info
                        if (source.RelevanceScore > 0)
                            package.AppendLine($"   📊 Độ liên quan: {source.RelevanceScore:F3} (cao = phù hợp hơn)");

                        if (source.FileSize.HasValue)
                            package.AppendLine($"   📁 Kích thước: {FormatFileSize(source.FileSize.Value)}");

                        package.AppendLine();
                    }
                }

                package.AppendLine("📋 **THỐNG KÊ TÀI LIỆU TỔNG QUAN:**");
                package.AppendLine("(AI PHẢI SỬ DỤNG SỐ LIỆU NÀY KHI TRẢ LỜI CÂU HỎI ĐẾM)");
                package.AppendLine();

                // ✅ ENHANCED statistics with better categorization
                var totalDocs = documentSources.Count;
                var publicDocs = documentSources.Count(s => s.IsPublic);
                var privateDocs = documentSources.Count(s => !s.IsPublic);
                var myDeptDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName);
                var myDeptPublicDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName && s.IsPublic);
                var myDeptPrivateDocs = documentSources.Count(s => s.DepartmentName == userContext.DepartmentName && !s.IsPublic);

                // ✅ Add status-based statistics
                var effectiveDocs = documentSources.Count(s => IsDocumentCurrentlyEffective(s));
                var expiredDocs = documentSources.Count(s => IsDocumentExpired(s));
                var pendingDocs = documentSources.Count(s => IsDocumentPending(s));
                var latestVersionDocs = documentSources.Count(s => s.IsLatestVersion);

                package.AppendLine($"📊 **Tổng số tài liệu:** {totalDocs}");
                package.AppendLine($"🔓 **Tài liệu PUBLIC (công khai):** {publicDocs}");
                package.AppendLine($"🔒 **Tài liệu PRIVATE (nội bộ):** {privateDocs}");
                package.AppendLine($"🏢 **Tài liệu phòng ban của tôi ({userContext.DepartmentName}):** {myDeptDocs}");
                package.AppendLine($"🔓🏢 **Tài liệu PUBLIC phòng ban của tôi:** {myDeptPublicDocs}");
                package.AppendLine($"🔒🏢 **Tài liệu PRIVATE phòng ban của tôi:** {myDeptPrivateDocs}");

                // ✅ NEW: Status-based statistics
                package.AppendLine($"✅ **Tài liệu đang có hiệu lực:** {effectiveDocs}");
                package.AppendLine($"❌ **Tài liệu đã hết hạn:** {expiredDocs}");
                package.AppendLine($"⏳ **Tài liệu chưa có hiệu lực:** {pendingDocs}");
                package.AppendLine($"🆕 **Tài liệu phiên bản mới nhất:** {latestVersionDocs}");

                // ✅ Enhanced document type statistics
                var docTypes = documentSources
                    .Where(s => !string.IsNullOrEmpty(s.DocumentType) && s.DocumentType != "Không rõ")
                    .GroupBy(s => s.DocumentType)
                    .ToDictionary(g => g.Key, g => g.Count());

                if (docTypes.Any())
                {
                    package.AppendLine("📋 **Thống kê theo loại tài liệu:**");
                    foreach (var docType in docTypes.OrderByDescending(x => x.Value))
                    {
                        var effectiveCount = documentSources.Count(s => s.DocumentType == docType.Key && IsDocumentCurrentlyEffective(s));
                        package.AppendLine($"   • {docType.Key}: {docType.Value} tài liệu ({effectiveCount} đang hiệu lực)");
                    }
                }

                // ✅ Enhanced department statistics
                var deptStats = documentSources
                    .Where(s => !string.IsNullOrEmpty(s.DepartmentName))
                    .GroupBy(s => s.DepartmentName)
                    .ToDictionary(g => g.Key, g => g.Count());

                if (deptStats.Any())
                {
                    package.AppendLine("🏢 **Thống kê theo phòng ban:**");
                    foreach (var dept in deptStats.OrderByDescending(x => x.Value))
                    {
                        var isMine = dept.Key == userContext.DepartmentName ? " (PHÒNG BAN CỦA TÔI)" : "";
                        var publicCount = documentSources.Count(s => s.DepartmentName == dept.Key && s.IsPublic);
                        var privateCount = dept.Value - publicCount;
                        package.AppendLine($"   • {dept.Key}: {dept.Value} tài liệu ({publicCount} public, {privateCount} private){isMine}");
                    }
                }

                package.AppendLine();
                package.AppendLine(new string('=', 70));
                package.AppendLine();

                package.AppendLine("📋 **CHI TIẾT TỪNG TÀI LIỆU - METADATA HOÀN CHỈNH:**");
                package.AppendLine("(AI PHẢI KIỂM TRA TẤT CẢ TÀI LIỆU DƯỚI ĐÂY, KHÔNG CHỈ TÀI LIỆU ĐẦU TIÊN)");

                // ✅ 2. ENHANCED INDIVIDUAL DOCUMENT DETAILS
                for (int i = 0; i < documentSources.Count; i++)
                {
                    var source = documentSources[i];
                    package.AppendLine($"📄 **TÀI LIỆU {i + 1}:**");
                    package.AppendLine($"   **Tên:** {source.Title ?? "Không rõ"}");
                    package.AppendLine($"   🆔 **DocumentId:** {source.DocumentId ?? "Không rõ"}");

                    // ✅ Enhanced Summary & Description with length info
                    if (!string.IsNullOrWhiteSpace(source.Summary))
                    {
                        var summaryPreview = source.Summary.Length > 200 ? source.Summary.Substring(0, 200) + "..." : source.Summary;
                        package.AppendLine($"   📝 **Tóm tắt:** {summaryPreview.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(source.Description))
                    {
                        var descPreview = source.Description.Length > 300 ? source.Description.Substring(0, 300) + "..." : source.Description;
                        package.AppendLine($"   📖 **Mô tả chi tiết:** {descPreview.Trim()}");
                    }

                    // ✅ Enhanced Tags with count
                    if (source.Tags?.Any() == true && source.Tags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
                    {
                        var validTags = source.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Take(10).ToList();
                        package.AppendLine($"   🏷️ **Tags ({validTags.Count}):** {string.Join(", ", validTags)}");
                    }

                    // ✅ Enhanced Access & Department with detailed classification
                    var visibility = source.IsPublic ? "PUBLIC (Công khai - ai cũng xem được)" : "PRIVATE (Nội bộ - hạn chế truy cập)";
                    var isMyDept = source.DepartmentName == userContext.DepartmentName;
                    var deptInfo = isMyDept ? "✅ PHÒNG BAN CỦA TÔI" : "❌ PHÒNG BAN KHÁC";

                    package.AppendLine($"   🔓 **Quyền truy cập:** {visibility}");
                    package.AppendLine($"   🏢 **Phòng ban:** {source.DepartmentName ?? "Không rõ"} ({deptInfo})");

                    // ✅ ENHANCED CLASSIFICATION with user permissions
                    var accessLevel = GetAccessLevelForUser(source, userContext);
                    package.AppendLine($"   🎯 **PHÂN LOẠI CHO USER:** {accessLevel}");

                    // ✅ Enhanced Document classification with priority
                    AddDocumentClassificationInfo(package, source);

                    // ✅ ENHANCED People information with role clarification
                    AddPeopleInformation(package, source);

                    // ✅ ENHANCED Date information with relative time
                    AddDateInformation(package, source);

                    // ✅ Enhanced File and version info
                    AddFileAndVersionInfo(package, source);

                    // ✅ Enhanced Status and Effectiveness
                    AddStatusAndEffectivenessInfo(package, source);

                    // ✅ Relevance and search info
                    if (source.RelevanceScore > 0)
                    {
                        var relevanceLevel = GetRelevanceLevel(source.RelevanceScore);
                        package.AppendLine($"   🎯 **Độ liên quan:** {source.RelevanceScore:F3}/1.000 ({relevanceLevel})");
                    }

                    // ✅ Add missing field warnings
                    AddMissingFieldWarnings(package, source);

                    package.AppendLine(); // Separator
                }

                package.AppendLine(new string('=', 70));
                package.AppendLine();
            }

            // ✅ 3. ENHANCED USER CONTEXT
            package.AppendLine("👤 **THÔNG TIN NGƯỜI DÙNG HIỆN TẠI:**");
            package.AppendLine($"🏢 **Phòng ban của tôi:** {userContext.DepartmentName ?? "Không rõ"}");
            package.AppendLine($"📂 **Mã phòng ban:** {userContext.DepartmentId ?? "Không rõ"}");
            package.AppendLine($"👤 **Vai trò/Chức vụ:** {userContext.Role ?? "Không rõ"}");
            package.AppendLine($"👤 **Họ tên đầy đủ:** {userContext.FullName ?? "Không rõ"}");
            package.AppendLine($"📧 **Email:** {userContext.Email ?? "Không rõ"}");

            // ✅ Add user permissions info
            if (userContext.Permissions?.Any() == true)
            {
                package.AppendLine($"🔑 **Quyền hạn:** {string.Join(", ", userContext.Permissions.Take(5))}");
            }
            package.AppendLine();

            // ✅ 4. ENHANCED DOCUMENT CONTENT
            if (!string.IsNullOrEmpty(documentContent))
            {
                package.AppendLine("📄 **NỘI DUNG TÀI LIỆU HOÀN CHỈNH:**");
                package.AppendLine("(AI CHỈ ĐƯỢC DÙNG THÔNG TIN TRONG PHẦN NÀY ĐỂ TRẢ LỜI VỀ NỘI DUNG)");

                // ✅ Add content statistics
                var contentStats = GetContentStatistics(documentContent);
                package.AppendLine($"📊 **Thống kê nội dung:** {contentStats}");
                package.AppendLine();

                var organizedContent = OrganizeContentForQuestion(documentContent, currentQuestion);
                package.AppendLine(organizedContent);
                package.AppendLine();
                package.AppendLine(new string('=', 70));
                package.AppendLine();
            }

            // ✅ 5. COMPREHENSIVE INSTRUCTIONS (keep existing)
            package.AppendLine(BuildComprehensiveInstructions());
            package.AppendLine("🚨 **FINAL REMINDER - ZERO TOLERANCE:**");
            package.AppendLine("❌ NO external knowledge (laws, regulations, common practices)");
            package.AppendLine("❌ NO suggestions ('khuyến nghị', 'tham khảo', 'có thể xem')");
            package.AppendLine("❌ NO recommendations of any kind");
            package.AppendLine("❌ NO general advice not from documents");
            package.AppendLine("✅ ONLY state what's in documents or 'Không có thông tin'");

            return package.ToString();
        }

        // ✅ NEW HELPER METHODS

        private bool IsDocumentCurrentlyEffective(DocumentInfo doc)
        {
            var now = DateTime.Now.Date;
            var isEffective = true;

            if (doc.EffectiveFrom.HasValue && now < doc.EffectiveFrom.Value.Date)
                isEffective = false;

            if (doc.EffectiveUntil.HasValue && now > doc.EffectiveUntil.Value.Date)
                isEffective = false;

            return isEffective;
        }

        private bool IsDocumentExpired(DocumentInfo doc)
        {
            return doc.EffectiveUntil.HasValue && DateTime.Now.Date > doc.EffectiveUntil.Value.Date;
        }

        private bool IsDocumentPending(DocumentInfo doc)
        {
            return doc.EffectiveFrom.HasValue && DateTime.Now.Date < doc.EffectiveFrom.Value.Date;
        }

        private string GetAccessLevelForUser(DocumentInfo source, UserContextFromJWT userContext)
        {
            var isMyDept = source.DepartmentName == userContext.DepartmentName;

            if (source.IsPublic && isMyDept)
                return "🟢 FULL ACCESS - PUBLIC + PHÒNG BAN CỦA TÔI";
            else if (!source.IsPublic && isMyDept)
                return "🟡 RESTRICTED ACCESS - PRIVATE + PHÒNG BAN CỦA TÔI";
            else if (source.IsPublic && !isMyDept)
                return "🔵 LIMITED ACCESS - PUBLIC + PHÒNG BAN KHÁC";
            else
                return "🔴 NO ACCESS - PRIVATE + PHÒNG BAN KHÁC";
        }

        private void AddDocumentClassificationInfo(StringBuilder package, DocumentInfo source)
        {
            if (!string.IsNullOrEmpty(source.DocumentType) && source.DocumentType != "Không rõ")
                package.AppendLine($"   📋 **Loại tài liệu:** {source.DocumentType}");

            if (!string.IsNullOrEmpty(source.Category) && source.Category != "Không rõ")
                package.AppendLine($"   📂 **Danh mục:** {source.Category}");

            if (!string.IsNullOrEmpty(source.Priority) && source.Priority != "Không rõ")
            {
                var priorityIcon = GetPriorityIcon(source.Priority);
                package.AppendLine($"   {priorityIcon} **Mức độ ưu tiên:** {source.Priority}");
            }
        }

        private void AddPeopleInformation(StringBuilder package, DocumentInfo source)
        {
            if (!string.IsNullOrEmpty(source.SignedBy) && source.SignedBy != "Không rõ")
            {
                package.AppendLine($"   🔴 **Người ký:** {source.SignedBy.ToUpper()}");
                package.AppendLine($"       📝 *Lưu ý: Đây là người có thẩm quyền ký ban hành tài liệu*");
            }

            if (!string.IsNullOrEmpty(source.ApprovedBy) && source.ApprovedBy != "Không rõ")
            {
                package.AppendLine($"   ✅ **Người phê duyệt/Người quản lý tài liệu:** {source.ApprovedBy}");
                package.AppendLine($"       📝 *Lưu ý: Khi user hỏi 'người quản lý' → đây chính là ApprovedBy, KHÔNG phải SignedBy*");
            }

            if (!string.IsNullOrEmpty(source.ReviewerName) && source.ReviewerName != "Không rõ")
                package.AppendLine($"   👁️ **Người xem xét:** {source.ReviewerName}");

            if (!string.IsNullOrEmpty(source.CreatedBy) && source.CreatedBy != "Không rõ")
                package.AppendLine($"   👤 **Người tạo:** {source.CreatedBy}");

            if (!string.IsNullOrEmpty(source.OwnerName) && source.OwnerName != "Không rõ")
                package.AppendLine($"   👑 **Chủ sở hữu:** {source.OwnerName}");
        }

        private void AddDateInformation(StringBuilder package, DocumentInfo source)
        {
            if (source.ApprovalDate.HasValue)
                package.AppendLine($"   📅 **Ngày phê duyệt:** {FormatDateWithRelative(source.ApprovalDate.Value)}");

            if (source.SignedDate.HasValue)
                package.AppendLine($"   📅 **Ngày ký:** {FormatDateWithRelative(source.SignedDate.Value)}");

            if (source.ReviewDate.HasValue)
                package.AppendLine($"   📅 **Ngày xem xét:** {FormatDateWithRelative(source.ReviewDate.Value)}");

            if (source.EffectiveFrom.HasValue)
            {
                var effectiveInfo = FormatDateWithRelative(source.EffectiveFrom.Value);
                var isPending = DateTime.Now.Date < source.EffectiveFrom.Value.Date;
                var icon = isPending ? "⏳" : "✅";
                package.AppendLine($"   {icon} **Có hiệu lực từ:** {effectiveInfo}");
            }

            if (source.EffectiveUntil.HasValue)
            {
                var expiryInfo = FormatDateWithRelative(source.EffectiveUntil.Value);
                var isExpired = DateTime.Now.Date > source.EffectiveUntil.Value.Date;
                var icon = isExpired ? "❌" : "⏰";
                package.AppendLine($"   {icon} **Hết hiệu lực:** {expiryInfo}");
            }
        }

        private void AddFileAndVersionInfo(StringBuilder package, DocumentInfo source)
        {
            if (!string.IsNullOrEmpty(source.VersionName))
            {
                var versionIcon = source.IsLatestVersion ? "🆕" : "📊";
                var versionNote = source.IsLatestVersion ? " (MỚI NHẤT)" : "";
                package.AppendLine($"   {versionIcon} **Phiên bản:** {source.VersionName}{versionNote}");
            }

            if (source.FileSize.HasValue)
                package.AppendLine($"   📁 **Kích thước:** {FormatFileSize(source.FileSize.Value)}");

            if (!string.IsNullOrEmpty(source.FileType))
                package.AppendLine($"   📄 **Định dạng:** {source.FileType}");

            if (!string.IsNullOrEmpty(source.FileName))
                package.AppendLine($"   📎 **Tên file:** {source.FileName}");
        }

        private void AddStatusAndEffectivenessInfo(StringBuilder package, DocumentInfo source)
        {
            if (!string.IsNullOrEmpty(source.Status))
                package.AppendLine($"   📊 **Trạng thái:** {source.Status}");

            // ✅ Add comprehensive effectiveness check
            var now = DateTime.Now.Date;
            var effectivenessStatus = GetEffectivenessStatus(source, now);
            package.AppendLine($"   {effectivenessStatus.Icon} **Tình trạng hiệu lực:** {effectivenessStatus.Description}");
        }

        private void AddMissingFieldWarnings(StringBuilder package, DocumentInfo source)
        {
            var missingCriticalFields = new List<string>();

            if (string.IsNullOrEmpty(source.SignedBy) || source.SignedBy == "Không rõ")
                missingCriticalFields.Add("Người ký");

            if (string.IsNullOrEmpty(source.ApprovedBy) || source.ApprovedBy == "Không rõ")
                missingCriticalFields.Add("Người phê duyệt");

            if (!source.EffectiveFrom.HasValue)
                missingCriticalFields.Add("Ngày hiệu lực");

            if (missingCriticalFields.Any())
            {
                package.AppendLine($"   ⚠️ **Thông tin thiếu:** {string.Join(", ", missingCriticalFields)}");
                package.AppendLine($"   📝 **Hướng dẫn AI:** Nếu user hỏi về thông tin thiếu → 'Không có thông tin về [FIELD] trong tài liệu'");
            }
        }


        private string FormatDateWithRelative(DateTime date)
        {
            var formatted = date.ToString("dd/MM/yyyy");
            var daysAgo = (DateTime.Now.Date - date.Date).Days;

            if (daysAgo == 0)
                return $"{formatted} (hôm nay)";
            else if (daysAgo == 1)
                return $"{formatted} (hôm qua)";
            else if (daysAgo > 0 && daysAgo <= 7)
                return $"{formatted} ({daysAgo} ngày trước)";
            else if (daysAgo > 0 && daysAgo <= 30)
                return $"{formatted} ({daysAgo / 7} tuần trước)";
            else if (daysAgo > 0 && daysAgo <= 365)
                return $"{formatted} ({daysAgo / 30} tháng trước)";
            else if (daysAgo > 0)
                return $"{formatted} ({daysAgo / 365} năm trước)";
            else if (daysAgo < 0 && Math.Abs(daysAgo) <= 7)
                return $"{formatted} (sau {Math.Abs(daysAgo)} ngày)";
            else if (daysAgo < 0)
                return $"{formatted} (trong tương lai)";

            return formatted;
        }

        private string FormatFileSize(long sizeInBytes)
        {
            if (sizeInBytes < 1024)
                return $"{sizeInBytes} bytes";
            else if (sizeInBytes < 1024 * 1024)
                return $"{sizeInBytes / 1024.0:F1} KB";
            else if (sizeInBytes < 1024 * 1024 * 1024)
                return $"{sizeInBytes / (1024.0 * 1024):F1} MB";
            else
                return $"{sizeInBytes / (1024.0 * 1024 * 1024):F1} GB";
        }

        private string GetPriorityIcon(string priority)
        {
            return priority?.ToLower() switch
            {
                "cao" or "high" => "🔴",
                "trung bình" or "medium" => "🟡",
                "thấp" or "low" => "🟢",
                _ => "⭐"
            };
        }

        private string GetRelevanceLevel(double score)
        {
            if (score >= 0.8) return "RẤT PHẦN HỢP";
            if (score >= 0.6) return "PHÙ HỢP";
            if (score >= 0.4) return "LIÊN QUAN";
            if (score >= 0.2) return "HƠI LIÊN QUAN";
            return "ÍT LIÊN QUAN";
        }

        private (string Icon, string Description) GetEffectivenessStatus(DocumentInfo source, DateTime now)
        {
            if (!source.EffectiveFrom.HasValue)
                return ("❓", "Không rõ ngày hiệu lực");

            if (now < source.EffectiveFrom.Value.Date)
                return ("⏳", $"Chưa có hiệu lực (từ {source.EffectiveFrom.Value:dd/MM/yyyy})");

            if (source.EffectiveUntil.HasValue && now > source.EffectiveUntil.Value.Date)
                return ("❌", $"Đã hết hiệu lực (hết {source.EffectiveUntil.Value:dd/MM/yyyy})");

            if (source.EffectiveUntil.HasValue)
            {
                var daysLeft = (source.EffectiveUntil.Value.Date - now).Days;
                if (daysLeft <= 30)
                    return ("⚠️", $"Sắp hết hiệu lực ({daysLeft} ngày nữa)");
                else
                    return ("✅", $"Đang có hiệu lực (đến {source.EffectiveUntil.Value:dd/MM/yyyy})");
            }

            return ("✅", "Đang có hiệu lực (không có ngày hết hạn)");
        }

        private string GetContentStatistics(string content)
        {
            if (string.IsNullOrEmpty(content)) return "Không có nội dung";

            var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var charCount = content.Length;
            var lineCount = content.Split('\n').Length;

            return $"{wordCount} từ, {charCount} ký tự, {lineCount} dòng";
        }

        /// <summary>
        /// ✅ NEW: Comprehensive instructions for all question types
        /// </summary>
        private string BuildComprehensiveInstructions()
        {
            var instructions = new StringBuilder();

            instructions.AppendLine("📋 COMPREHENSIVE QUESTION HANDLING GUIDE");
            instructions.AppendLine();

            instructions.AppendLine("🔢 **A. COUNT QUESTIONS** (có bao nhiêu, có mấy, tổng cộng, số lượng):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Read THỐNG KÊ TỔNG QUAN for exact numbers");
            instructions.AppendLine("   2. Verify with CHI TIẾT TỪNG TÀI LIỆU section");
            instructions.AppendLine("   3. COUNT CAREFULLY: opening number MUST match actual count and final total");
            instructions.AppendLine("   4. Format: 'Có [EXACT_NUMBER] tài liệu [TYPE]: 1. [Name 1], 2. [Name 2]... Tổng: [SAME_NUMBER] tài liệu.'");
            instructions.AppendLine("❌ Forbidden: Count wrong, skip docs, say 5 but list 6 documents");
            instructions.AppendLine();

            instructions.AppendLine("📄 **B. CONTENT QUESTIONS** (điều X nói gì, quy định về, thủ tục, quy trình):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Search in NỘI DUNG TÀI LIỆU HOÀN CHỈNH only");
            instructions.AppendLine("   2. Priority: Summary → Description → Content details");
            instructions.AppendLine("   3. Quote accurately, NO general knowledge explanations");
            instructions.AppendLine("   4. Format: 'Theo tài liệu [NAME]: [EXACT_CONTENT] [Citation]'");
            instructions.AppendLine("❌ Forbidden: Explain with external knowledge, create fake info");
            instructions.AppendLine();

            instructions.AppendLine("📋 **C. METADATA QUESTIONS** (ai ký, khi nào hiệu lực, thuộc phòng nà, trạng thái):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Search in CHI TIẾT TỪNG TÀI LIỆU metadata sections");
            instructions.AppendLine("   2. Answer only with available information");
            instructions.AppendLine("   3. If missing → 'Không có thông tin về [FIELD] trong tài liệu'");
            instructions.AppendLine("❌ Forbidden: Guess info, use info from other documents");
            instructions.AppendLine();

            instructions.AppendLine("🔍 **D. SEARCH BY TOPIC/TIME** (tài liệu về HR, quy định bảo mật, tài liệu hôm nay, mới nhất):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Check Tags → Title → Summary → Content");
            instructions.AppendLine("   2. For time queries ('hôm nay', 'tháng này', 'mới nhất'): check creation dates carefully");
            instructions.AppendLine("   3. STOP IMMEDIATELY and answer EXACTLY: 'Không có thông tin về [SPECIFIC_REQUEST] trong các tài liệu hiện có' if no results");
            instructions.AppendLine("   4. ABSOLUTELY DO NOT provide alternative info when no exact matches found");
            instructions.AppendLine("   5. Format only when HAVE results: 'Có [NUMBER] tài liệu về [TOPIC]: 1. [Name] - [Summary]...'");
            instructions.AppendLine("❌ Forbidden: Provide unrelated docs, skip Tags, list other docs when no time match");
            instructions.AppendLine();

            instructions.AppendLine("📄 **E. DOWNLOAD/ACCESS REQUESTS** (tải giúp tôi, cho tôi xem, truy cập, link, đường dẫn):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Find document in ĐƯỜNG DẪN TRUY CẬP TÀI LIỆU section");
            instructions.AppendLine("   2. Use EXACT links provided, never create or modify DocumentId");
            instructions.AppendLine("   3. Format: 'Bạn có thể truy cập tài liệu \"[NAME]\" tại: [EXACT_LINK]'");
            instructions.AppendLine("   4. ABSOLUTELY DO NOT create links or change DocumentId");
            instructions.AppendLine("   5. If not found → Answer EXACTLY: 'Không có thông tin về tài liệu [NAME] trong các tài liệu hiện có'");
            instructions.AppendLine("❌ Forbidden: Say cannot download, put @ before URL, use [DocumentId] symbol instead of real ID");
            instructions.AppendLine();

            instructions.AppendLine("🔗 **F. OBJECTIVE COMPARISON QUESTIONS** (khác biệt giữa, so sánh, tài liệu nào mới hơn, đang hiệu lực):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Get info from CHI TIẾT TỪNG TÀI LIỆU for each compared document");
            instructions.AppendLine("   2. ONLY compare objective data (dates, content, metadata)");
            instructions.AppendLine("   3. DO NOT give subjective judgments about 'better' or 'more important'");
            instructions.AppendLine("   4. Format: 'Về mặt [OBJECTIVE_CRITERIA], document A has [DATA], document B has [DATA]'");
            instructions.AppendLine("❌ Forbidden: Subjective comments, recommend which doc is 'better'");
            instructions.AppendLine();

            instructions.AppendLine("📖 **G. SUMMARY REQUESTS** (tóm tắt tài liệu, nội dung chính, điểm quan trọng, tóm tắt trước):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. MUST extract comprehensively from NỘI DUNG TÀI LIỆU HOÀN CHỈNH: Summary + Description + key points from Content");
            instructions.AppendLine("   2. MUST read all provided content to create full and accurate summary");
            instructions.AppendLine("   3. MUST ensure minimum 150-200 words or 3-5 paragraphs");
            instructions.AppendLine("   4. Include: document purpose, scope, important provisions, effective dates");
            instructions.AppendLine("   5. Must quote important points: at least 3-5 points if available");
            instructions.AppendLine("   6. Format: 'Theo tài liệu \"[NAME]\":\\n[FULL_SUMMARY]\\n\\nCác điểm chính:\\n- [POINT_1]\\n- [POINT_2]...\\n\\n[Citation]'");
            instructions.AppendLine("   7. IF user just says \"Tóm tắt\" or \"Tóm tắt trước\": use current document or highest RelevanceScore document");
            instructions.AppendLine("   8. EVERY summary MUST be detailed, well-structured and include important points");
            instructions.AppendLine("❌ Forbidden: Add personal opinions, explain with external knowledge, too short summary (under 150 words)");
            instructions.AppendLine();

            instructions.AppendLine("🔐 **H. ACCESS QUESTIONS** (tôi có thể xe, public hay private, phòng ban tôi có gì):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Check IsPublic + DepartmentName vs THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine("   2. Classify: PUBLIC+MY_DEPT, PRIVATE+MY_DEPT, etc.");
            instructions.AppendLine("   3. Format: 'Bạn [CAN/CANNOT] truy cập vì [SPECIFIC_REASON]'");
            instructions.AppendLine("❌ Forbidden: Guess access rights, ignore permissions");
            instructions.AppendLine();

            instructions.AppendLine("🚫 **I. SUGGESTION/EVALUATION/RECOMMENDATION QUESTIONS** (nên đọc tài liệu nào, tài liệu nào quan trọng, gợi ý, tài liệu nào tốt, ưu tiên thế nào):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. ABSOLUTELY DO NOT suggest, recommend, evaluate any documents");
            instructions.AppendLine("   2. Answer: \"Tôi chỉ có thể cung cấp thông tin dựa trên câu hỏi cụ thể về tài liệu. Bạn có thể hỏi về:\"");
            instructions.AppendLine("   3. Guide: \"- Nội dung cụ thể: 'Tài liệu X nói gì về chủ đề Y?'\"");
            instructions.AppendLine("   4. Guide: \"- Thông tin metadata: 'Ai ký tài liệu Z?', 'Khi nào có hiệu lực?'\"");
            instructions.AppendLine("   5. Guide: \"- Tìm kiếm: 'Tài liệu nào về HR?', 'Có bao nhiêu tài liệu public?'\"");
            instructions.AppendLine("   6. Guide: \"- So sánh dữ liệu: 'Tài liệu nào mới hơn?', 'Tài liệu nào đang hiệu lực?'\"");
            instructions.AppendLine("❌ Forbidden: All forms of suggestion, recommendation, evaluation, or ranking documents");
            instructions.AppendLine();

            instructions.AppendLine("👤 **J. PEOPLE-BASED SEARCH** (tài liệu do tôi tạo, do [NAME] ký, do [NAME] phê duyệt, do [NAME] xem xét):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. **Created by me**: Compare CreatedBy with FullName in THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine("   2. **Signed by [NAME]**: Find in SignedBy containing asked name");
            instructions.AppendLine("   3. **Approved by [NAME]**: Find in ApprovedBy containing asked name");
            instructions.AppendLine("   4. **Reviewed by [NAME]**: Find in ReviewerName containing asked name");
            instructions.AppendLine("   5. **Created by [NAME]**: Find in CreatedBy or OwnerName containing asked name");
            instructions.AppendLine("   6. COUNT ACCURATELY: First write 'Có [NUMBER] tài liệu' MUST match list count and final total");
            instructions.AppendLine("   7. Format: 'Có [NUMBER] tài liệu do [NAME/bạn] [ACTION]: 1. [Doc name]...'");
            instructions.AppendLine("❌ Forbidden: Incorrect comparison, skip documents, count inaccurately");
            instructions.AppendLine();

            instructions.AppendLine("📅 **K. TIME-BASED SEARCH** (tài liệu hiệu lực năm 2024, ký trong tháng X, hết hạn khi nào)");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. **Effective**: Check EffectiveFrom and EffectiveUntil");
            instructions.AppendLine("   2. **Signed date**: Check SignedDate");
            instructions.AppendLine("   3. **Approval date**: Check ApprovalDate");
            instructions.AppendLine("   4. **Review date**: Check ReviewDate");
            instructions.AppendLine("   5. Format: 'Có [NUMBER] tài liệu [TIME_CONDITION]: 1. [Name] - [Specific date]...'");
            instructions.AppendLine("❌ Forbidden: Wrong time calculation, guess dates");
            instructions.AppendLine();

            instructions.AppendLine("🔧 **L. COMPLEX/COMBINED QUESTIONS** (\"Có bao nhiêu tài liệu HR public và nội dung chính là gì?\"):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. Split question into small parts");
            instructions.AppendLine("   2. Process each part according to corresponding guidelines (A,B,C,H,J...)");
            instructions.AppendLine("   3. Combine results logically");
            instructions.AppendLine("❌ Forbidden: Skip any part of the question");
            instructions.AppendLine();

            instructions.AppendLine("🤔 **M. VAGUE/UNCLEAR QUESTIONS** (\"Tài liệu này thế nào?\", \"Nói về cái đó\", \"Giải thích thêm\"):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. **Unclear reference**: 'Bạn có thể nói rõ hơn muốn hỏi về tài liệu nào không?'");
            instructions.AppendLine("   2. **Vague question**: 'Xin lỗi, câu hỏi chưa rõ ràng. Bạn muốn biết thông tin gì cụ thể?'");
            instructions.AppendLine("   3. **Missing context**: Reference chat history to understand context");
            instructions.AppendLine("   4. Format: 'Để trả lời chính xác, bạn có thể làm rõ [SPECIFIC_REQUIREMENT] không?'");
            instructions.AppendLine("❌ Forbidden: Guess intention, give vague answers");
            instructions.AppendLine();

            instructions.AppendLine("🔗 **N. REFERENCE/CONTEXT QUESTIONS** (\"Cái này hiệu lực chưa?\", \"Họ ký khi nào?\", \"Document đó nói gì?\"):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. **Check chat history**: Find most recently mentioned document");
            instructions.AppendLine("   2. **'Này/đó/cái này'**: Use document discussed in previous message");
            instructions.AppendLine("   3. **'Họ/người đó'**: Use person name mentioned before");
            instructions.AppendLine("   4. **No context found**: 'Bạn đang hỏi về tài liệu/người nào cụ thể?'");
            instructions.AppendLine("   5. Format: 'Về [DISCUSSED_DOCUMENT], [SPECIFIC_ANSWER]'");
            instructions.AppendLine("❌ Forbidden: Guess wrong document/person");
            instructions.AppendLine();

            instructions.AppendLine("📊 **O. PROCESS/ANALYSIS QUESTIONS** (\"Quy trình có mấy bước?\", \"Bước tiếp theo?\", \"Workflow như nào?\"):");
            instructions.AppendLine("Required process:");
            instructions.AppendLine("   1. **Find in content**: Count steps/phases listed in document");
            instructions.AppendLine("   2. **Analyze structure**: 'Bước 1:', 'Giai đoạn 1:', 'Thứ nhất:'");
            instructions.AppendLine("   3. **Quote accurately**: Do not create steps not in document");
            instructions.AppendLine("   4. Format: 'Theo tài liệu, quy trình có [NUMBER] bước: 1. [STEP_1]...'");
            instructions.AppendLine("❌ Forbidden: Create steps not in document");
            instructions.AppendLine();

            instructions.AppendLine("🎯 **INFORMATION PRIORITY RULES:**");
            instructions.AppendLine("1. **Count questions:** THỐNG KÊ TỔNG QUAN → Verify with CHI TIẾT TỪNG TÀI LIỆU");
            instructions.AppendLine("2. **Content:** Summary → Description → Content → NO external knowledge");
            instructions.AppendLine("3. **Metadata:** CHI TIẾT TỪNG TÀI LIỆU → DO NOT guess missing info");
            instructions.AppendLine("4. **Search:** Tags → Title → Summary → Content");
            instructions.AppendLine("5. **Access rights:** IsPublic + DepartmentName + THÔNG TIN NGƯỜI DÙNG");
            instructions.AppendLine("6. **Suggestions/Evaluations:** ABSOLUTELY FORBIDDEN - Only provide objective data");
            instructions.AppendLine();

            instructions.AppendLine("🚫 **CRITICAL PROHIBITIONS:**");
            instructions.AppendLine("❌ Use any knowledge OUTSIDE 'THÔNG TIN TÀI LIỆU HOÀN CHỈNH'");
            instructions.AppendLine("❌ Create fake numbers, dates, names");
            instructions.AppendLine("❌ Explain concepts with general knowledge (HR, IT, legal...)");
            instructions.AppendLine("❌ Suggest internet search, contact agencies, external sources");
            instructions.AppendLine("❌ Say 'theo kinh nghiệm', 'thường thì', 'dựa trên thông lệ'");
            instructions.AppendLine("❌ Give advice not based on documents");
            instructions.AppendLine("❌ Count wrong or skip documents when listing");
            instructions.AppendLine("❌ ABSOLUTELY DO NOT say 'có 5 tài liệu' but list 6 or 7 documents and conclude 'tổng cộng có 6 tài liệu'");
            instructions.AppendLine("❌ ABSOLUTELY DO NOT answer 'Không có thông tin về X. Tuy nhiên, có các tài liệu Y' when no matching results");
            instructions.AppendLine("❌ ABSOLUTELY FORBIDDEN to answer 'Không có thông tin về tài liệu hôm nay. Tuy nhiên, có 4 tài liệu PUBLIC'");
            instructions.AppendLine("❌ ABSOLUTELY FORBIDDEN to answer 'Người quản lý tài liệu X là Y' when Y is signer (SignedBy) not approver (ApprovedBy)");
            instructions.AppendLine("❌ COMPLETELY FORBIDDEN to list any documents after saying no results found for time criteria");
            instructions.AppendLine("❌ ABSOLUTELY FORBIDDEN to suggest, recommend, evaluate any documents");
            instructions.AppendLine("❌ ABSOLUTELY FORBIDDEN to say 'nên đọc', 'quan trọng', 'ưu tiên', 'gợi ý', 'tốt hơn'");
            instructions.AppendLine("❌ FORBIDDEN all forms of implicit or indirect recommendations");
            instructions.AppendLine("❌ Give vague answers when clear info available");
            instructions.AppendLine("❌ Use info from document A to answer about document B");
            instructions.AppendLine();

            instructions.AppendLine("✅ **ALLOWED & ENCOURAGED:**");
            instructions.AppendLine("✓ 100% information from 'THÔNG TIN TÀI LIỆU HOÀN CHỈNH'");
            instructions.AppendLine("✓ Accurate quotes from Summary, Description, Content, Metadata");
            instructions.AppendLine("✓ Count and list based on 'CHI TIẾT TỪNG TÀI LIỆU'");
            instructions.AppendLine("✓ Reference chat history to understand question context");
            instructions.AppendLine("✓ Say EXACTLY 'Không có thông tin về [REQUEST] trong tài liệu' when missing info AND STOP THERE");
            instructions.AppendLine("✓ Compare objective data when requested (dates, status, numbers)");
            instructions.AppendLine("✓ Say 'Xin lỗi, câu hỏi này không rõ ràng' if unclear");
            instructions.AppendLine();

            instructions.AppendLine("📝 **STANDARD RESPONSE TEMPLATES:**");
            instructions.AppendLine();
            instructions.AppendLine("**Count Questions:**");
            instructions.AppendLine("'Có [EXACT_NUMBER] tài liệu [TYPE] [SCOPE]:");
            instructions.AppendLine("1. [Document name 1] - [Summary/brief description if available]");
            instructions.AppendLine("2. [Document name 2] - [Summary/brief description if available]");
            instructions.AppendLine("Tổng cộng: [MUST_MATCH_OPENING_NUMBER_AND_ACTUAL_COUNT] tài liệu.'");
            instructions.AppendLine();
            instructions.AppendLine("**People-based Search:**");
            instructions.AppendLine("'Có [EXACT_NUMBER] tài liệu do [NAME/bạn] [ACTION]:");
            instructions.AppendLine("1. [Document name 1] - [Action date if available]");
            instructions.AppendLine("2. [Document name 2] - [Action date if available]");
            instructions.AppendLine("Tổng cộng: [MUST_MATCH_OPENING_NUMBER_AND_ACTUAL_DOCS] tài liệu.'");
            instructions.AppendLine();
            instructions.AppendLine("**Suggestion/Evaluation Questions:**");
            instructions.AppendLine("'Tôi chỉ có thể cung cấp thông tin dựa trên câu hỏi cụ thể về tài liệu. Bạn có thể hỏi về:");
            instructions.AppendLine("- Nội dung cụ thể: \\'Tài liệu X nói gì về chủ đề Y?\\'");
            instructions.AppendLine("- Thông tin metadata: \\'Ai ký tài liệu Z?\\', \\'Khi nào có hiệu lực?\\'");
            instructions.AppendLine("- Tìm kiếm: \\'Tài liệu nào về HR?\\', \\'Có bao nhiêu tài liệu public?\\'");
            instructions.AppendLine("- So sánh dữ liệu: \\'Tài liệu nào mới hơn?\\', \\'Tài liệu nào đang hiệu lực?\\'");
            instructions.AppendLine();
            instructions.AppendLine("**Vague Questions:**");
            instructions.AppendLine("'Để trả lời chính xác, bạn có thể làm rõ [SPECIFIC_REQUIREMENT] không?'");
            instructions.AppendLine();
            instructions.AppendLine("**Reference Questions:**");
            instructions.AppendLine("'Về [PREVIOUSLY_DISCUSSED_DOCUMENT]: [SPECIFIC_ANSWER]'");
            instructions.AppendLine();
            instructions.AppendLine("**Objective Comparison:**");
            instructions.AppendLine("'Về mặt [OBJECTIVE_CRITERIA], tài liệu A có [ACTUAL_DATA], tài liệu B có [ACTUAL_DATA].'");
            instructions.AppendLine();
            instructions.AppendLine("**Process Questions:**");
            instructions.AppendLine("'Theo tài liệu, quy trình có [NUMBER] bước:");
            instructions.AppendLine("1. [STEP_1_EXACT_FROM_DOCUMENT]");
            instructions.AppendLine("2. [STEP_2_EXACT_FROM_DOCUMENT]...'");
            instructions.AppendLine();
            instructions.AppendLine("**Content or Summary (DETAILED):**");
            instructions.AppendLine("'Theo tài liệu \\\"[DOCUMENT_NAME]\\\":");
            instructions.AppendLine("[ACCURATE_CONTENT - MUST_BE_MINIMUM_150-200_WORDS]");
            instructions.AppendLine("");
            instructions.AppendLine("Các điểm chính:");
            instructions.AppendLine("- [IMPORTANT_POINT_1]");
            instructions.AppendLine("- [IMPORTANT_POINT_2]");
            instructions.AppendLine("- [IMPORTANT_POINT_3]");
            instructions.AppendLine("...");
            instructions.AppendLine("");
            instructions.AppendLine("[Citation]'");
            instructions.AppendLine();
            instructions.AppendLine("**Metadata:**");
            instructions.AppendLine("'[REQUESTED_INFO] của tài liệu \\\"[NAME]\\\": [ACCURATE_VALUE]");
            instructions.AppendLine("Nguồn: [CHI TIẾT TỪNG TÀI LIỆU]'");
            instructions.AppendLine();
            instructions.AppendLine("**Document Access:**");
            instructions.AppendLine("'Bạn có thể truy cập tài liệu \\\"[NAME]\\\" tại đây: https://docai.asia/document/f286d69e9ee44e94ae916222cd3ae8fb'");
            instructions.AppendLine("// NOTE: NEVER put @ before URL and MUST replace with actual DocumentId");
            instructions.AppendLine("// REAL EXAMPLE: If DocumentId = 'f286d69e9ee44e94ae916222cd3ae8fb', URL must be 'https://docai.asia/document/f286d69e9ee44e94ae916222cd3ae8fb'");
            instructions.AppendLine();
            instructions.AppendLine("**No Information:**");
            instructions.AppendLine("'Không có thông tin về [SPECIFIC_REQUEST] trong các tài liệu hiện có.'");
            instructions.AppendLine("// IMPORTANT: When no info, STOP IMMEDIATELY at above sentence!");
            instructions.AppendLine("// ABSOLUTELY DO NOT write 'Tuy nhiên, có X tài liệu...' or provide alternative info!");
            instructions.AppendLine("// Answer must be short, only stating no info available, no additional content!");
            instructions.AppendLine("// ESPECIALLY for time queries like 'hôm nay', 'tháng này', 'mới nhất' - ONLY ANSWER 'Không có thông tin' DO NOT LIST OTHER DOCS!");
            instructions.AppendLine("// If user asks 'Tài liệu hôm nay' and none found, ONLY ANSWER 'Không có thông tin về tài liệu hôm nay trong các tài liệu hiện có' and STOP!");

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
                // ✅ NEW: Check if requested model still exists
                var requestedModelExists = await IsModelActiveAsync(requestedModelName);

                if (!requestedModelExists)
                {
                    // Model đã bị xóa → ignore request, dùng session model hiện tại
                    _logger.LogInformation("Client requested deleted model {RequestedModel}, using session model {SessionModel}",
                        requestedModelName, session.ModelName);
                    return; // Allow to continue
                }

                // ✅ NEW: Check if session model still exists  
                var sessionModelExists = await IsModelActiveAsync(session.ModelName);

                if (!sessionModelExists)
                {
                    // Session model bị xóa → update sang requested model
                    _logger.LogInformation("Session model {SessionModel} deleted, updating to {RequestedModel}",
                        session.ModelName, requestedModelName);

                    session.ModelName = requestedModelName;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.UpdatedBy = "system";

                    _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                    await _unitOfWork.CommitAsync();
                    return;
                }

                // Cả 2 models đều tồn tại → không cho đổi model
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
                // ✅ SAME CALL: Chỉ method implementation thay đổi, call không đổi
                var preferences = await _preferenceService.GetEffectivePreferencesAsync(sessionId, userId);
                // ✅ FIXED: GetEffectivePreferencesAsync giờ có logic đúng (Session Override > User Default)

                var enhancedPrompt = basePrompt;

                // ✅ SAME: Các helper methods này KHÔNG ĐỔI
                enhancedPrompt = AddUserNameToPrompt(enhancedPrompt, preferences.UserName);
                enhancedPrompt = AddCharacteristicsToPrompt(enhancedPrompt, preferences.ChatbotCharacteristics);
                enhancedPrompt = AddAdditionalInfoToPrompt(enhancedPrompt, preferences.AdditionalInfo);

                return enhancedPrompt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enhance prompt with user preferences for user {UserId}, session {SessionId}. Using base prompt.", userId, sessionId);
                return basePrompt; // ✅ SAME: Fallback logic không đổi
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

             UpdateSessionWithTitleGeneration(session, userId, isFirstMessage, firstUserMessage);

            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
            await _unitOfWork.CommitAsync();
        }

        private void UpdateSessionWithTitleGeneration(ChatSession session, string userId, bool isFirstMessage, string firstUserMessage)
        {
            session.LastActiveAt = DateTime.UtcNow;
            session.UpdatedBy = userId;

            if (isFirstMessage && ShouldGenerateNewTitle(session.Title))
            {
                GenerateAndSetSessionTitleSmart(session, firstUserMessage); // ✅ SMART GENERATION
            }
        }

        private bool ShouldGenerateNewTitle(string currentTitle)
        {
            return string.IsNullOrEmpty(currentTitle) || currentTitle == ChatConstants.DefaultSessionTitle;
        }

        private void GenerateAndSetSessionTitleSmart(ChatSession session, string firstUserMessage)
        {
            try
            {
                var smartTitle = GenerateSmartTitle(firstUserMessage);
                session.Title = smartTitle;
                _logger.LogInformation("✅ Generated smart title for session {SessionId}: {Title}", session.Id, smartTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Smart title generation failed for session {SessionId}", session.Id);
                session.Title = $"Trò chuyện {DateTime.Now:HH:mm}";
            }
        }
        private string GenerateSmartTitle(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return "Cuộc trò chuyện mới";

            try
            {
                var cleanMessage = userMessage.Trim().ToLowerInvariant();

                // Remove question marks and common prefixes
                cleanMessage = Regex.Replace(cleanMessage, @"^(xin chào|chào|hello|hi|bạn có thể|giúp tôi|cho tôi|tôi muốn|tôi cần)[\s,]*", "", RegexOptions.IgnoreCase);
                cleanMessage = cleanMessage.Replace("?", "").Replace("!", "").Trim();

                // 1. 📄 Document-specific patterns (highest priority)
                if (Regex.IsMatch(cleanMessage, @"(quy định|chính sách|policy)"))
                {
                    var match = Regex.Match(cleanMessage, @"(quy định|chính sách|policy)\s+(.+)");
                    if (match.Success)
                        return $"Quy định: {CapitalizeWords(match.Groups[2].Value)}";
                    return "Quy định công ty";
                }

                if (Regex.IsMatch(cleanMessage, @"(có bao nhiêu|số lượng|mấy|count)"))
                {
                    var match = Regex.Match(cleanMessage, @"(có bao nhiêu|số lượng|mấy|count)\s+(.+)");
                    if (match.Success)
                        return $"Số lượng {CapitalizeWords(match.Groups[2].Value)}";
                    return "Đếm số lượng";
                }

                if (Regex.IsMatch(cleanMessage, @"(ai ký|người ký|do ai|signed by|approved by)"))
                {
                    return "Người ký tài liệu";
                }

                if (Regex.IsMatch(cleanMessage, @"(tóm tắt|summary|nội dung chính|main content)"))
                {
                    return "Tóm tắt tài liệu";
                }

                if (Regex.IsMatch(cleanMessage, @"(hiệu lực|effective|có hiệu lực|còn hiệu lực)"))
                {
                    return "Hiệu lực tài liệu";
                }

                // 2. 💼 HR/Business specific patterns
                if (Regex.IsMatch(cleanMessage, @"(lương|salary|tiền lương|wage)"))
                    return "Về lương";

                if (Regex.IsMatch(cleanMessage, @"(nghỉ phép|leave|vacation|holiday)"))
                    return "Về nghỉ phép";

                if (Regex.IsMatch(cleanMessage, @"(bảo hiểm|insurance|bhxh|bhyt)"))
                    return "Về bảo hiểm";

                if (Regex.IsMatch(cleanMessage, @"(hợp đồng|contract|agreement)"))
                    return "Về hợp đồng";

                if (Regex.IsMatch(cleanMessage, @"(tuyển dụng|recruitment|hiring|interview)"))
                    return "Về tuyển dụng";

                // 3. 🔍 Action patterns
                if (Regex.IsMatch(cleanMessage, @"^(tìm|tìm kiếm|search|find)"))
                {
                    var match = Regex.Match(cleanMessage, @"^(tìm|tìm kiếm|search|find)\s+(.+)");
                    if (match.Success)
                        return $"Tìm kiếm {CapitalizeWords(match.Groups[2].Value)}";
                    return "Tìm kiếm";
                }

                if (Regex.IsMatch(cleanMessage, @"^(tải|download|tải xuống|tải về)"))
                {
                    var match = Regex.Match(cleanMessage, @"^(tải|download|tải xuống|tải về)\s+(.+)");
                    if (match.Success)
                        return $"Tải {CapitalizeWords(match.Groups[2].Value)}";
                    return "Tải tài liệu";
                }

                if (Regex.IsMatch(cleanMessage, @"^(so sánh|compare|khác biệt)"))
                {
                    return "So sánh tài liệu";
                }

                if (Regex.IsMatch(cleanMessage, @"^(xem|đọc|check|view)"))
                {
                    var match = Regex.Match(cleanMessage, @"^(xem|đọc|check|view)\s+(.+)");
                    if (match.Success)
                        return $"Xem {CapitalizeWords(match.Groups[2].Value)}";
                    return "Xem tài liệu";
                }

                // 4. 📅 Time-based patterns
                if (Regex.IsMatch(cleanMessage, @"(hôm nay|today|ngày hôm nay)"))
                {
                    var match = Regex.Match(cleanMessage, @"(.+?)\s+(hôm nay|today)");
                    if (match.Success)
                        return $"Hôm nay: {CapitalizeWords(match.Groups[1].Value)}";
                    return "Hôm nay";
                }

                if (Regex.IsMatch(cleanMessage, @"(tháng này|this month|trong tháng)"))
                {
                    var match = Regex.Match(cleanMessage, @"(.+?)\s+(tháng này|this month)");
                    if (match.Success)
                        return $"Tháng này: {CapitalizeWords(match.Groups[1].Value)}";
                    return "Tháng này";
                }

                if (Regex.IsMatch(cleanMessage, @"(mới nhất|latest|recent|gần đây)"))
                {
                    var match = Regex.Match(cleanMessage, @"(.+?)\s+(mới nhất|latest|recent)");
                    if (match.Success)
                        return $"Mới nhất: {CapitalizeWords(match.Groups[1].Value)}";
                    return "Mới nhất";
                }

                // 5. 🎯 Department/Category detection
                var departments = new Dictionary<string, string>
        {
            { @"(nhân sự|hr|human resources)", "HR" },
            { @"(it|công nghệ|technology|tech)", "IT" },
            { @"(tài chính|finance|accounting|kế toán)", "Tài chính" },
            { @"(pháp lý|legal|law|luật)", "Pháp lý" },
            { @"(marketing|tiếp thị|quảng cáo)", "Marketing" },
            { @"(bán hàng|sales|kinh doanh)", "Kinh doanh" }
        };

                foreach (var dept in departments)
                {
                    if (Regex.IsMatch(cleanMessage, dept.Key))
                    {
                        return $"Về {dept.Value}";
                    }
                }

                // 6. 🧠 Smart keyword extraction
                var stopWords = new HashSet<string>
        {
            "tôi", "bạn", "của", "và", "với", "cho", "về", "trong", "trên", "dưới",
            "là", "có", "được", "sẽ", "đã", "đang", "rồi", "thì", "mà", "để", "khi",
            "nếu", "hay", "hoặc", "nhưng", "còn", "chỉ", "cũng", "một", "hai", "ba",
            "này", "đó", "kia", "đây", "đấy", "nào", "gì", "sao", "thế", "vậy", "như",
            "theo", "bằng", "từ", "đến", "lên", "xuống", "ra", "vào", "qua", "the",
            "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with"
        };

                var words = cleanMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => !stopWords.Contains(w) && w.Length > 2)
                    .Where(w => !Regex.IsMatch(w, @"^\d+$")) // Remove pure numbers
                    .Take(4) // Limit to prevent long titles
                    .ToArray();

                if (words.Length >= 2)
                {
                    var keywordTitle = string.Join(" ", words);
                    return $"Về {CapitalizeWords(keywordTitle)}";
                }
                else if (words.Length == 1)
                {
                    return $"Câu hỏi về {CapitalizeWords(words[0])}";
                }

                // 7. 📏 Smart truncation with sentence detection
                var originalMessage = userMessage.Trim();
                if (originalMessage.Length > 80)
                {
                    // Try to find sentence boundary
                    var sentenceEnd = originalMessage.IndexOfAny(new char[] { '.', '!', '?' }, 0, Math.Min(60, originalMessage.Length));
                    if (sentenceEnd > 20)
                    {
                        return CapitalizeWords(originalMessage.Substring(0, sentenceEnd));
                    }

                    // Find word boundary
                    var truncated = originalMessage.Substring(0, 60);
                    var lastSpace = truncated.LastIndexOf(' ');
                    if (lastSpace > 20)
                    {
                        truncated = truncated.Substring(0, lastSpace);
                    }
                    return CapitalizeWords(truncated) + "...";
                }

                // 8. 🔄 Final cleanup and capitalization
                var finalTitle = CapitalizeWords(originalMessage);

                // Remove redundant words at the end
                finalTitle = Regex.Replace(finalTitle, @"\s+(Không|Nothing|Empty)$", "", RegexOptions.IgnoreCase);

                return finalTitle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in smart title generation");

                // Emergency fallback with timestamp
                var timeBasedTitle = $"Trò chuyện {DateTime.Now:HH:mm}";
                return timeBasedTitle;
            }
        }
        private string CapitalizeWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            try
            {
                // Clean and normalize
                var cleaned = Regex.Replace(text.Trim(), @"\s+", " ");

                // Split into words and capitalize each
                var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(word =>
                    {
                        var cleanWord = word.Trim();
                        if (string.IsNullOrEmpty(cleanWord))
                            return cleanWord;

                        // Don't capitalize prepositions and articles (unless they're the first word)
                        var lowerWord = cleanWord.ToLowerInvariant();
                        var prepositions = new HashSet<string> { "của", "và", "với", "cho", "về", "trong", "trên", "dưới", "từ", "đến" };

                        if (prepositions.Contains(lowerWord))
                            return lowerWord;

                        return char.ToUpperInvariant(cleanWord[0]) + cleanWord.Substring(1).ToLowerInvariant();
                    });

                var result = string.Join(" ", words);

                // Always capitalize first word
                if (!string.IsNullOrEmpty(result))
                {
                    result = char.ToUpperInvariant(result[0]) + result.Substring(1);
                }

                return result;
            }
            catch (Exception)
            {
                // Fallback: simple first letter capitalization
                return string.IsNullOrEmpty(text) ? text :
                       char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();
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
                        GenerateAndSetSessionTitleSmart(session, userMessageContent); // ✅ SMART GENERATION
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
                            GenerateAndSetSessionTitleSmart(session, userMessageContent); // ✅ SMART GENERATION
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
                GenerateAndSetSessionTitleSmart(session, userMessageContent); // ✅ SMART GENERATION
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