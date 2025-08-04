using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class ManualDocumentSearchService : IManualDocumentSearchService
    {
        private readonly IDocumentSearchService _documentSearchService;
        private readonly ILogger<ManualDocumentSearchService> _logger;

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

        public bool ShouldSearchDocuments(string message)
        {
            var documentKeywords = new[]
            {
                "quyết định", "thủ tục", "quy định", "chính sách", "hướng dẫn",
                "tài liệu", "văn bản", "công bố", "nghị định", "thông tư",
                "hành chính", "pháp lý", "luật", "điều lệ", "quy trình",
                "document", "policy", "procedure", "regulation", "guideline"
            };

            var lowerMessage = message.ToLowerInvariant();
            var shouldSearch = documentKeywords.Any(keyword => lowerMessage.Contains(keyword));

            Console.WriteLine($"🔍 [MANUAL] Should search documents: {shouldSearch} for message: {message.Substring(0, Math.Min(50, message.Length))}...");

            return shouldSearch;
        }
    }
}
