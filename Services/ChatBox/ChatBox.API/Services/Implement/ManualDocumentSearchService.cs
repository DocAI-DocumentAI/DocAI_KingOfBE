using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class ManualDocumentSearchService : IManualDocumentSearchService
    {
        private readonly IDocumentSearchService _documentSearchService;
        private readonly ILogger<ManualDocumentSearchService> _logger;
        private readonly ICacheService _cacheService;

        public ManualDocumentSearchService(
            IDocumentSearchService documentSearchService,
            ILogger<ManualDocumentSearchService> logger,
            ICacheService cacheService)
        {
            _documentSearchService = documentSearchService;
            _logger = logger;
            _cacheService = cacheService;
        }

        /// <summary>
        /// ✅ UPDATED: Get raw document content instead of processed answer
        /// </summary>
        public async Task<string> SearchAndAnswerAsync(string query, string userId)
        {
            try
            {
                var simplifiedQuery = SimplifyQueryForCache(query);
                var cacheKey = $"doc_raw_minimal_{simplifiedQuery.GetHashCode()}_{userId}";

                var cached = await _cacheService.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogInformation("💾 [MINIMAL] Cache hit for simplified query");
                    return cached == "NO_RESULT" ? null : cached;
                }

                _logger.LogInformation("🔍 [MINIMAL] Minimal cost RAW CONTENT search for: {Query}",
                    query.Substring(0, Math.Min(30, query.Length)));

                // ✅ Get raw content instead of processed answer
                var rawContent = await _documentSearchService.GetRawContentAsync(query, userId);

                if (!string.IsNullOrEmpty(rawContent))
                {
                    _logger.LogInformation("✅ [MINIMAL] Found RAW CONTENT: {Length} chars", rawContent.Length);
                }
                else
                {
                    _logger.LogInformation("❌ [MINIMAL] No relevant documents found");
                }

                // Cache both positive and negative results for 2 hours
                var cacheValue = string.IsNullOrEmpty(rawContent) ? "NO_RESULT" : rawContent;
                await _cacheService.SetStringAsync(cacheKey, cacheValue, TimeSpan.FromHours(2));

                return rawContent; // ✅ Return raw content, not processed answer
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [MINIMAL] Search error");
                return null;
            }
        }

        /// <summary>
        /// ✅ UPDATED: Get raw content with sources
        /// </summary>
        public async Task<(string RawContent, List<DocumentInfo> Sources)> SearchWithSourcesAsync(string query, string userId)
        {
            try
            {
                var (rawContent, sources) = await _documentSearchService.GetRawContentWithSourcesAsync(query, userId);

                return (rawContent, sources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in search with sources");
                return (null, new List<DocumentInfo>());
            }
        }

        public bool ShouldSearchDocuments(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Trim().Length < 5)
                return false;

            var lowerMessage = message.ToLowerInvariant().Trim();

            // Skip greetings and small talk
            var skipPatterns = new[] { "chào", "hello", "hi", "cảm ơn", "thank", "ok", "được", "rồi" };
            if (lowerMessage.Length < 15 && skipPatterns.Any(p => lowerMessage.Contains(p)))
                return false;

            // Require document-related content
            var docKeywords = new[] { "tài liệu", "hợp đồng", "quy định", "thủ tục", "đăng ký" };
            return docKeywords.Any(k => lowerMessage.Contains(k));
        }

        private string SimplifyQueryForCache(string query)
        {
            return query.Trim()
                .ToLowerInvariant()
                .Replace("?", "").Replace("!", "").Replace(".", "")
                .Replace("  ", " ")
                .Replace("xin chào", "").Replace("hello", "").Replace("hi", "")
                .Replace("cảm ơn", "").Replace("thank", "")
                .Trim();
        }
    }
}
