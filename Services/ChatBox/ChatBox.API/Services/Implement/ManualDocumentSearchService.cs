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

        public async Task<string> SearchAndAnswerAsync(string query, string userId, string? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("🔍 [MINIMAL] Empty query provided for user: {UserId}", userId);
                return null;
            }

            try
            {
                var simplifiedQuery = SimplifyQueryForCache(query);
                var cacheKey = $"doc_raw_minimal_v3_{simplifiedQuery.GetHashCode():X}_{userId}";

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

                _logger.LogInformation("🔍 [MINIMAL] Performing RAW CONTENT search for user: {UserId}, query: '{Query}', DocumentId: {DocumentId}",
                          userId, query.Length > 50 ? query.Substring(0, 50) + "..." : query, documentId ?? "None");

                var rawContent = await _documentSearchService.GetRawContentAsync(query, userId, documentId);

                if (!string.IsNullOrEmpty(rawContent))
                {
                    _logger.LogInformation("✅ [MINIMAL] Found RAW CONTENT: {Length} chars for user: {UserId}",
                        rawContent.Length, userId);

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
                    _logger.LogInformation("ℹ️ [MINIMAL] No relevant documents found for user: {UserId}, query: '{Query}'",
                        userId, simplifiedQuery);

                    await CacheResultSafely(cacheKey, "NO_RESULT", TimeSpan.FromMinutes(30));
                }

                return rawContent;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "⏰ [MINIMAL] Search timeout");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "💥 [MINIMAL] Service error");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [MINIMAL] Search error for user: {UserId}, query: '{Query}' - {ErrorType}: {ErrorMessage}",
                    userId, query, ex.GetType().Name, ex.Message);
                throw new InvalidOperationException($"Document search failed: {ex.Message}", ex);
            }
        }

        public async Task<(string RawContent, List<DocumentInfo> Sources)> SearchWithSourcesAsync(string query, string userId, string? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("🔍 [SOURCES] Empty query provided for user: {UserId}", userId);
                return (null, new List<DocumentInfo>());
            }

            try
            {
                _logger.LogInformation("🔍 [SOURCES] Searching with sources for user: {UserId}, query: '{Query}', DocumentId: {DocumentId}",
                  userId, query.Length > 50 ? query.Substring(0, 50) + "..." : query, documentId ?? "None");

                var (rawContent, sources) = await _documentSearchService.GetRawContentWithSourcesAsync(query, userId, documentId);

                if (!string.IsNullOrEmpty(rawContent))
                {
                    _logger.LogInformation("✅ [SOURCES] Found content: {ContentLength} chars with {SourceCount} sources for user: {UserId}",
                        rawContent.Length, sources?.Count ?? 0, userId);
                }
                else
                {
                    _logger.LogInformation("ℹ️ [SOURCES] No content found for user: {UserId}", userId);
                }

                return (rawContent, sources ?? new List<DocumentInfo>());
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool ShouldSearchDocuments(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var trimmedMessage = message.Trim();

            if (trimmedMessage.Length < 3)
            {
                return false;
            }

            var lowerMessage = trimmedMessage.ToLowerInvariant();

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

            var documentKeywords = new[]
            {
                "tài liệu", "văn bản", "quy định", "hướng dẫn",
                "thông tư", "nghị định", "quyết định", "chỉ thị",
                "thủ tục", "quy trình", "hồ sơ", "đăng ký",
                "đơn xin", "giấy phép", "chứng nhận",
                "hợp đồng", "thỏa thuận", "cam kết", "bảo hiểm",
                "lương", "tiền lương", "chế độ", "chính sách",
                "lao động", "nhân sự", "quản lý", "điều hành",
                "báo cáo", "thống kê", "đánh giá",
                "phòng ban", "bộ phận", "đơn vị", "cơ quan",
                "công ty", "tổ chức", "ban", "sở"
            };

            var questionKeywords = new[]
            {
                "làm thế nào", "how", "cách nào", "quy định gì",
                "có phải", "được không", "cần gì", "yêu cầu gì",
                "thời hạn", "hạn chót", "deadline", "khi nào"
            };

            bool hasDocumentKeyword = documentKeywords.Any(k => lowerMessage.Contains(k));
            bool hasQuestionPattern = questionKeywords.Any(k => lowerMessage.Contains(k));

            if (hasDocumentKeyword)
            {
                return true;
            }

            if (hasQuestionPattern && lowerMessage.Length > 15)
            {
                return true;
            }

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

        private string SimplifyQueryForCache(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            var simplified = query.Trim()
                .ToLowerInvariant()
                .Replace("?", "").Replace("!", "").Replace(".", "")
                .Replace(",", "").Replace(";", "").Replace(":", "")
                .Replace("  ", " ");

            var wordsToRemove = new[] { "xin chào", "chào", "hello", "hi", "cảm ơn", "thank" };
            foreach (var word in wordsToRemove)
            {
                simplified = simplified.Replace(word, "");
            }

            return simplified.Trim();
        }

        private bool IsContentWorthCaching(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            if (content.Length < 50)
                return false;

            var errorIndicators = new[] { "error", "lỗi", "không tìm thấy", "not found" };
            var lowerContent = content.ToLowerInvariant();

            if (errorIndicators.Any(indicator => lowerContent.Contains(indicator)))
                return false;

            return true;
        }

        private int DetermineCacheHoursByContentQuality(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 1;

            var baseHours = 2;

            if (content.Length > 1000) baseHours += 1;
            if (content.Length > 3000) baseHours += 1;
            if (content.Length > 5000) baseHours += 2;

            var structureIndicators = new[] { "điều", "khoản", "mục", "phần", "chương", "bước" };
            var lowerContent = content.ToLowerInvariant();

            if (structureIndicators.Any(indicator => lowerContent.Contains(indicator)))
            {
                baseHours += 2;
            }

            return Math.Min(baseHours, 8);
        }

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
    }
}
