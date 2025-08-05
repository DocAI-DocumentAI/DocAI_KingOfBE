using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class ManualDocumentSearchService : IManualDocumentSearchService
    {
        private readonly IDocumentSearchService _documentSearchService;
        private readonly ILogger<ManualDocumentSearchService> _logger;

        // 🔧 IMPROVED: Better categorized keywords
        private static readonly Dictionary<string, string[]> DocumentCategories = new()
        {
            ["legal"] = new[] { "quyết định", "nghị định", "thông tư", "luật", "pháp lý", "quy định", "điều lệ" },
            ["procedure"] = new[] { "thủ tục", "quy trình", "hướng dẫn", "cách thức", "procedure", "process" },
            ["policy"] = new[] { "chính sách", "policy", "guideline", "regulation" },
            ["document"] = new[] { "tài liệu", "văn bản", "document", "file", "công bố" },
            ["admin"] = new[] { "hành chính", "administrative", "quản lý", "management" }
        };

        // 🔧 IMPROVED: Exclusion phrases để tránh false positive
        private static readonly string[] ExclusionPhrases = new[]
        {
            "không cần", "không muốn", "không có", "chưa có",
            "don't need", "don't want", "no document", "without"
        };

        public ManualDocumentSearchService(
            IDocumentSearchService documentSearchService,
            ILogger<ManualDocumentSearchService> logger)
        {
            _documentSearchService = documentSearchService;
            _logger = logger;
        }

        public async Task<string> SearchAndAnswerAsync(string query, string userId)
        {
            try
            {
                _logger.LogInformation("🔍 [MANUAL] Searching documents for: {Query}", query);
                Console.WriteLine($"🔍 [MANUAL] Manual document search triggered for: {query}");

                var result = await _documentSearchService.GetRAGAnswerWithSourcesAsync(query, userId);

                if (!string.IsNullOrEmpty(result) &&
                    !result.Contains("Xin lỗi, tôi không tìm thấy thông tin"))
                {
                    _logger.LogInformation("✅ [MANUAL] Found document answer: {Length} chars", result.Length);
                    Console.WriteLine($"✅ [MANUAL] Document search successful: {result.Length} chars");
                    return result;
                }

                _logger.LogWarning("❌ [MANUAL] No relevant documents found");
                Console.WriteLine("❌ [MANUAL] No relevant documents found");
                return null; // Trả null để AI tự trả lời
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [MANUAL] Document search error");
                Console.WriteLine($"❌ [MANUAL] Document search error: {ex.Message}");
                return null;
            }
        }

        // 🔧 IMPROVED: Better keyword detection logic
        public bool ShouldSearchDocuments(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var lowerMessage = message.ToLowerInvariant().Trim();

            // 🔧 IMPROVED: Check exclusion phrases first
            if (ExclusionPhrases.Any(phrase => lowerMessage.Contains(phrase)))
            {
                Console.WriteLine($"🔍 [MANUAL] Excluded due to negative phrase: {message.Substring(0, Math.Min(50, message.Length))}...");
                return false;
            }

            // 🔧 IMPROVED: Better category matching
            var matchedCategories = 0;
            var totalMatches = 0;

            foreach (var category in DocumentCategories)
            {
                var categoryMatches = category.Value.Count(keyword => lowerMessage.Contains(keyword));
                if (categoryMatches > 0)
                {
                    matchedCategories++;
                    totalMatches += categoryMatches;
                }
            }

            //  IMPROVED: More sophisticated logic
            var shouldSearch = false;

            if (matchedCategories >= 2) // Multiple categories matched
            {
                shouldSearch = true;
            }
            else if (matchedCategories == 1 && totalMatches >= 2) // Multiple keywords in same category
            {
                shouldSearch = true;
            }
            else if (IsDirectDocumentQuery(lowerMessage)) // Direct document query
            {
                shouldSearch = true;
            }

            Console.WriteLine($"🔍 [MANUAL] Should search: {shouldSearch} (categories: {matchedCategories}, matches: {totalMatches}) for: {message.Substring(0, Math.Min(50, message.Length))}...");

            return shouldSearch;
        }

        // 🔧 IMPROVED: Detect direct document queries
        private static bool IsDirectDocumentQuery(string message)
        {
            var directQueries = new[]
            {
                "tìm tài liệu", "search document", "có tài liệu nào",
                "văn bản về", "quy định về", "chính sách về",
                "hướng dẫn về", "thủ tục về", "quy trình về"
            };

            return directQueries.Any(query => message.Contains(query));
        }
    }
}
