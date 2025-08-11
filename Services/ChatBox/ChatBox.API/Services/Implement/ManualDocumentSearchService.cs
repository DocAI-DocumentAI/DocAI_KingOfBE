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
        /// ✅ ENHANCED: Get raw document content with intelligent caching and query optimization
        /// </summary>
        public async Task<string> SearchAndAnswerAsync(string query, string userId)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("🔍 [MINIMAL] Empty query provided for user: {UserId}", userId);
                return null;
            }

            try
            {
                var simplifiedQuery = SimplifyQueryForCache(query);
                var cacheKey = $"doc_raw_minimal_v2_{simplifiedQuery.GetHashCode():X}_{userId}";

                // ✅ ENHANCED: Try cache first with better error handling
                try
                {
                    var cached = await _cacheService.GetStringAsync(cacheKey);
                    if (!string.IsNullOrEmpty(cached))
                    {
                        if (cached == "NO_RESULT")
                        {
                            _logger.LogDebug("💾 [MINIMAL] Cache hit - no result for simplified query: '{Query}'", simplifiedQuery);
                            return null;
                        }

                        _logger.LogInformation("💾 [MINIMAL] Cache hit for user: {UserId}, query: '{Query}'", userId, simplifiedQuery);
                        return cached;
                    }
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "💾 [MINIMAL] Cache read failed, proceeding with search");
                }

                _logger.LogInformation("🔍 [MINIMAL] Performing RAW CONTENT search for user: {UserId}, query: '{Query}'",
                    userId, query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                // ✅ ENHANCED: Get raw content with better error handling
                var rawContent = await _documentSearchService.GetRawContentAsync(query, userId);

                // ✅ ENHANCED: Better result validation and logging
                if (!string.IsNullOrEmpty(rawContent))
                {
                    _logger.LogInformation("✅ [MINIMAL] Found RAW CONTENT: {Length} chars for user: {UserId}",
                        rawContent.Length, userId);

                    // ✅ ENHANCED: Validate content quality before caching
                    if (IsContentWorthCaching(rawContent))
                    {
                        var cacheHours = DetermineCacheHoursByContentQuality(rawContent);
                        await CacheResultSafely(cacheKey, rawContent, TimeSpan.FromHours(cacheHours));
                        _logger.LogDebug("💾 [MINIMAL] Cached result for {CacheHours} hours", cacheHours);
                    }
                    else
                    {
                        _logger.LogDebug("💾 [MINIMAL] Content quality too low for caching");
                    }
                }
                else
                {
                    _logger.LogInformation("❌ [MINIMAL] No relevant documents found for user: {UserId}, query: '{Query}'",
                        userId, simplifiedQuery);

                    // ✅ Cache negative results for shorter time
                    await CacheResultSafely(cacheKey, "NO_RESULT", TimeSpan.FromMinutes(30));
                }

                return rawContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [MINIMAL] Search error for user: {UserId}, query: '{Query}' - {ErrorType}: {ErrorMessage}",
                    userId, query, ex.GetType().Name, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// ✅ ENHANCED: Get raw content with sources and better error handling
        /// </summary>
        public async Task<(string RawContent, List<DocumentInfo> Sources)> SearchWithSourcesAsync(string query, string userId)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("🔍 [SOURCES] Empty query provided for user: {UserId}", userId);
                return (null, new List<DocumentInfo>());
            }

            try
            {
                _logger.LogInformation("🔍 [SOURCES] Searching with sources for user: {UserId}, query: '{Query}'",
                    userId, query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                var (rawContent, sources) = await _documentSearchService.GetRawContentWithSourcesAsync(query, userId);

                if (!string.IsNullOrEmpty(rawContent))
                {
                    _logger.LogInformation("✅ [SOURCES] Found content: {ContentLength} chars with {SourceCount} sources for user: {UserId}",
                        rawContent.Length, sources?.Count ?? 0, userId);
                }
                else
                {
                    _logger.LogInformation("❌ [SOURCES] No content found for user: {UserId}", userId);
                }

                return (rawContent, sources ?? new List<DocumentInfo>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [SOURCES] Error in search with sources for user: {UserId} - {ErrorType}: {ErrorMessage}",
                    userId, ex.GetType().Name, ex.Message);
                return (null, new List<DocumentInfo>());
            }
        }

        /// <summary>
        /// ✅ ENHANCED: Intelligent document search detection
        /// </summary>
        public bool ShouldSearchDocuments(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var trimmedMessage = message.Trim();

            // ✅ ENHANCED: Minimum length check
            if (trimmedMessage.Length < 3)
            {
                return false;
            }

            var lowerMessage = trimmedMessage.ToLowerInvariant();

            // ✅ ENHANCED: Skip greetings and small talk with better patterns
            var skipPatterns = new[]
            {
                "chào", "hello", "hi", "hey", "xin chào",
                "cảm ơn", "thank", "thanks", "cám ơn",
                "ok", "okay", "được", "rồi", "ừm", "uhm",
                "bye", "tạm biệt", "chào tạm biệt"
            };

            if (lowerMessage.Length < 20 && skipPatterns.Any(p => lowerMessage.Contains(p)))
            {
                return false;
            }

            // ✅ ENHANCED: Better document-related keyword detection
            var documentKeywords = new[]
            {
                // Core document terms
                "tài liệu", "văn bản", "quy định", "hướng dẫn",
                "thông tư", "nghị định", "quyết định", "chỉ thị",
                
                // Process and procedure terms
                "thủ tục", "quy trình", "hồ sơ", "đăng ký",
                "đơn xin", "giấy phép", "chứng nhận",
                
                // Contract and legal terms
                "hợp đồng", "thỏa thuận", "cam kết", "bảo hiểm",
                "lương", "tiền lương", "chế độ", "chính sách",
                
                // Administrative terms
                "lao động", "nhân sự", "quản lý", "điều hành",
                "báo cáo", "thống kê", "đánh giá",
                
                // Department and organizational terms
                "phòng ban", "bộ phận", "đơn vị", "cơ quan",
                "công ty", "tổ chức", "ban", "sở"
            };

            var questionKeywords = new[]
            {
                "làm thế nào", "how", "cách nào", "quy định gì",
                "có phải", "được không", "cần gì", "yêu cầu gì",
                "thời hạn", "hạn chót", "deadline", "khi nào"
            };

            // ✅ Check for document keywords or question patterns
            bool hasDocumentKeyword = documentKeywords.Any(k => lowerMessage.Contains(k));
            bool hasQuestionPattern = questionKeywords.Any(k => lowerMessage.Contains(k));

            // ✅ ENHANCED: More intelligent detection
            if (hasDocumentKeyword)
            {
                return true;
            }

            if (hasQuestionPattern && lowerMessage.Length > 15)
            {
                return true;
            }

            // ✅ Check for procedural language patterns
            var proceduralPatterns = new[]
            {
                "cần làm", "phải làm", "cần thực hiện",
                "bước nào", "như thế nào", "ra sao"
            };

            if (proceduralPatterns.Any(p => lowerMessage.Contains(p)))
            {
                return true;
            }

            return false;
        }

        #region Helper Methods

        /// <summary>
        /// ✅ ENHANCED: Better query simplification for caching
        /// </summary>
        private string SimplifyQueryForCache(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            var simplified = query.Trim()
                .ToLowerInvariant()
                .Replace("?", "").Replace("!", "").Replace(".", "")
                .Replace(",", "").Replace(";", "").Replace(":", "")
                .Replace("  ", " ");

            // ✅ Remove common Vietnamese greeting words
            var wordsToRemove = new[] { "xin chào", "chào", "hello", "hi", "cảm ơn", "thank" };
            foreach (var word in wordsToRemove)
            {
                simplified = simplified.Replace(word, "");
            }

            return simplified.Trim();
        }

        /// <summary>
        /// ✅ ENHANCED: Determine if content is worth caching
        /// </summary>
        private bool IsContentWorthCaching(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            // ✅ Minimum content length
            if (content.Length < 50)
                return false;

            // ✅ Check for meaningful content (not just error messages)
            var errorIndicators = new[] { "error", "lỗi", "không tìm thấy", "not found" };
            var lowerContent = content.ToLowerInvariant();

            if (errorIndicators.Any(indicator => lowerContent.Contains(indicator)))
                return false;

            return true;
        }

        /// <summary>
        /// ✅ ENHANCED: Determine cache duration based on content quality
        /// </summary>
        private int DetermineCacheHoursByContentQuality(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 1;

            // ✅ Base cache time
            var baseHours = 2;

            // ✅ Adjust based on content length (longer content = cache longer)
            if (content.Length > 1000) baseHours += 1;
            if (content.Length > 3000) baseHours += 1;
            if (content.Length > 5000) baseHours += 2;

            // ✅ Check for structured content indicators
            var structureIndicators = new[] { "điều", "khoản", "mục", "phần", "chương", "bước" };
            var lowerContent = content.ToLowerInvariant();

            if (structureIndicators.Any(indicator => lowerContent.Contains(indicator)))
            {
                baseHours += 2; // Structured content is more valuable
            }

            // ✅ Cap at reasonable maximum
            return Math.Min(baseHours, 8); // Max 8 hours
        }

        /// <summary>
        /// ✅ ENHANCED: Safe caching with error handling
        /// </summary>
        private async Task CacheResultSafely(string cacheKey, string value, TimeSpan expiration)
        {
            try
            {
                await _cacheService.SetStringAsync(cacheKey, value, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "💾 [CACHE] Failed to cache result for key: {CacheKey}", cacheKey);
            }
        }

        #endregion
    }
}
