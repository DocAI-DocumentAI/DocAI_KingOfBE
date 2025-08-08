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
                _logger.LogInformation("🔍 [AUTO] Searching documents for: {Query}", query);
                Console.WriteLine($"🔍 [AUTO] Auto document search for: {query}");

                var result = await _documentSearchService.GetRAGAnswerWithSourcesAsync(query, userId);

                if (!string.IsNullOrEmpty(result) &&
                    !result.Contains("Xin lỗi, tôi không tìm thấy thông tin"))
                {
                    _logger.LogInformation("✅ [AUTO] Found document answer: {Length} chars", result.Length);
                    Console.WriteLine($"✅ [AUTO] Document search successful: {result.Length} chars");
                    return result;
                }

                _logger.LogInformation("❌ [AUTO] No relevant documents found, AI will use general knowledge");
                Console.WriteLine("❌ [AUTO] No relevant documents found");
                return null; // AI sẽ tự trả lời
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [AUTO] Document search error");
                Console.WriteLine($"❌ [AUTO] Document search error: {ex.Message}");
                return null;
            }
        }

        // 🔧 SIMPLIFIED: Always return true - let AI handle everything
        public bool ShouldSearchDocuments(string message)
        {
            // Bỏ tất cả logic phức tạp - luôn search
            if (string.IsNullOrWhiteSpace(message))
                return false;

            //// Skip certain types of messages that clearly don't need document search
            //var lowerMessage = message.ToLowerInvariant().Trim();

            //// Skip greetings and simple interactions
            //var skipPhrases = new[]
            //{
            //    "xin chào", "hello", "hi", "chào", "good morning", "good afternoon",
            //    "cảm ơn", "thank you", "thanks", "ok", "được rồi", "tốt",
            //    "bye", "tạm biệt", "goodbye", "see you"
            //};

            //if (skipPhrases.Any(phrase => lowerMessage == phrase || lowerMessage.StartsWith(phrase + " ")))
            //{
            //    Console.WriteLine($"⏭️ [AUTO] Skipping document search for greeting/simple interaction");
            //    return false;
            //}

            //// Skip very short messages (< 3 words)
            //if (lowerMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3)
            //{
            //    Console.WriteLine($"⏭️ [AUTO] Skipping document search for very short message");
            //    return false;
            //}

            //Console.WriteLine($"🔍 [AUTO] Will search documents for: {message.Substring(0, Math.Min(50, message.Length))}...");
            return true;
        }
    }
}
