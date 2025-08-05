using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ChatBox.API.Services.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Connections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.DTOs;

namespace ChatBox.API.Services.Implement
{
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly IRequestClient<ChatBoxDocumentRequest> _requestClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentSearchService> _logger;
        private readonly ICacheService _cacheService; //  THÊM Redis cache

        public DocumentSearchService(
            IRequestClient<ChatBoxDocumentRequest> requestClient,
            IConfiguration configuration,
            ILogger<DocumentSearchService> logger,
            ICacheService cacheService) //  THÊM dependency
        {
            _requestClient = requestClient;
            _configuration = configuration;
            _logger = logger;
            _cacheService = cacheService; //  ASSIGN
        }

        public async Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5)
        {
            // 🔧 FIXED: Sử dụng Redis cache thay vì static cache
            var cacheKey = $"doc_search_{query.GetHashCode()}_{userId}_{maxResults}";

            try
            {
                var cached = await _cacheService.GetAsync<ChatBoxDocumentResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("Returning cached document result for query: {Query} (user: {UserId})",
                        query.Substring(0, Math.Min(50, query.Length)), userId);
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cached result, proceeding with fresh search");
            }

            try
            {
                var request = new ChatBoxDocumentRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    Query = query,
                    UserId = userId,
                    MaxResults = maxResults,
                    MinRelevanceScore = 0.7,
                    OnlyPublic = true,
                    OnlyOfficial = false,
                    RequestTime = DateTime.UtcNow
                };

                _logger.LogInformation("Sending document search: {RequestId} - Query: {Query} - User: {UserId}",
                    request.RequestId, query.Substring(0, Math.Min(50, query.Length)), userId);

                var timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 5));
                var response = await _requestClient.GetResponse<ChatBoxDocumentResponse>(request, timeout: timeout);
                var result = response.Message;

                if (result.Success)
                {
                    // 🔧 FIXED: Cache với Redis
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache search result");
                    }

                    _logger.LogInformation("Document search success: {RequestId} - Sources: {SourceCount} - Time: {ProcessingTime}ms",
                        request.RequestId, result.Sources?.Count ?? 0, result.ProcessingTimeMs);
                }
                else
                {
                    _logger.LogWarning("Document search failed: {RequestId} - Error: {Error}",
                        request.RequestId, result.ErrorMessage);
                }

                return result;
            }
            catch (RequestTimeoutException ex)
            {
                _logger.LogError(ex, "Document search timeout for query: {Query} (user: {UserId})",
                    query.Substring(0, Math.Min(50, query.Length)), userId);
                return new ChatBoxDocumentResponse
                {
                    Success = false,
                    ErrorMessage = "Tìm kiếm tài liệu quá thời gian chờ (5s).",
                    QueryProcessed = query,
                    Sources = new List<ChatBoxDocumentSource>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document search error for query: {Query} (user: {UserId})",
                    query.Substring(0, Math.Min(50, query.Length)), userId);
                return new ChatBoxDocumentResponse
                {
                    Success = false,
                    ErrorMessage = "Đã xảy ra lỗi khi tìm kiếm tài liệu.",
                    QueryProcessed = query,
                    Sources = new List<ChatBoxDocumentSource>()
                };
            }
        }

        public async Task<ChatBoxDocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId)
        {
            // 🔧 FIXED: Cũng sử dụng Redis cache cho official documents
            var cacheKey = $"official_doc_search_{query.GetHashCode()}_{userId}";

            try
            {
                var cached = await _cacheService.GetAsync<ChatBoxDocumentResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("Returning cached official document result for query: {Query}",
                        query.Substring(0, Math.Min(50, query.Length)));
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cached official document result");
            }

            try
            {
                var request = new ChatBoxDocumentRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    Query = query,
                    UserId = userId,
                    MaxResults = 5,
                    MinRelevanceScore = 0.8, // Higher threshold for official docs
                    OnlyPublic = true,
                    OnlyOfficial = true,
                    RequestTime = DateTime.UtcNow
                };

                var timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 5));
                var response = await _requestClient.GetResponse<ChatBoxDocumentResponse>(request, timeout: timeout);
                var result = response.Message;

                if (result.Success)
                {
                    // Cache official documents longer (30 minutes)
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache official document result");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Official document search error for query: {Query}",
                    query.Substring(0, Math.Min(50, query.Length)));
                return new ChatBoxDocumentResponse
                {
                    Success = false,
                    ErrorMessage = "Đã xảy ra lỗi khi tìm kiếm tài liệu chính thức.",
                    QueryProcessed = query,
                    Sources = new List<ChatBoxDocumentSource>()
                };
            }
        }

        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId);

            if (result?.Success == true && !string.IsNullOrEmpty(result.Answer))
            {
                return result.Answer;
            }

            return "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.";
        }

        public async Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId);

            if (result?.Success == true && !string.IsNullOrEmpty(result.Answer))
            {
                var answer = new StringBuilder();
                answer.AppendLine(result.Answer);

                if (result.Sources?.Any() == true)
                {
                    answer.AppendLine();
                    answer.AppendLine("📚 **Nguồn tham khảo:**");

                    foreach (var source in result.Sources.Take(3))
                    {
                        answer.AppendLine($"• {source.Title} v{source.VersionName} (Score: {source.RelevanceScore:F2})");
                        if (!string.IsNullOrEmpty(source.DepartmentName))
                        {
                            answer.AppendLine($"  📂 {source.DepartmentName}");
                        }
                    }
                }

                return answer.ToString();
            }

            return "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.";
        }
    }
}
