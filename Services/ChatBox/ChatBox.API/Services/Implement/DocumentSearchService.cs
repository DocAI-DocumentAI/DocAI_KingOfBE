using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBox.API.Payload.Response;
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
        private readonly ICacheService _cacheService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DocumentSearchService(
            IRequestClient<ChatBoxDocumentRequest> requestClient,
            IConfiguration configuration,
            ILogger<DocumentSearchService> logger,
            ICacheService cacheService,
            IHttpContextAccessor httpContextAccessor)
        {
            _requestClient = requestClient;
            _configuration = configuration;
            _logger = logger;
            _cacheService = cacheService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// ✅ UPDATED: Get raw document content from Document microservice
        /// </summary>
        public async Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 3)
        {
            maxResults = Math.Min(maxResults, 3);

            var userContext = GetUserContextFromJWT();
            var cacheKey = $"doc_raw_{query.GetHashCode()}_{userId}_{userContext.DepartmentId}_{maxResults}";

            try
            {
                var cached = await _cacheService.GetAsync<ChatBoxDocumentResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("💾 [CACHE] Raw content cache hit for user: {UserId}, dept: {DeptId}", userId, userContext.DepartmentId);
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache read failed, proceeding with fresh search");
            }

            try
            {
                var request = new ChatBoxDocumentRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    Query = TruncateQueryForOptimization(query),
                    UserId = userId,
                    Email = userContext.Email,
                    FullName = userContext.FullName,
                    Phone = userContext.Phone,
                    Role = userContext.Role,
                    DepartmentId = userContext.DepartmentId,
                    DepartmentName = userContext.DepartmentName,
                    Permissions = userContext.Permissions,
                    MaxResults = maxResults,
                    MinRelevanceScore = 0.28,
                    OnlyPublic = false,
                    OnlyOfficial = true,
                    RequestTime = DateTime.UtcNow
                };

                _logger.LogInformation("🔍 [SEARCH] Raw content request - User: {FullName} ({Role}), Dept: {DeptName}, MaxResults: {MaxResults}",
                    request.FullName, request.Role, request.DepartmentName, request.MaxResults);

                var timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 45));
                var response = await _requestClient.GetResponse<ChatBoxDocumentResponse>(request, timeout: timeout);
                var result = response.Message;

                if (result.Success && !string.IsNullOrEmpty(result.RawContent))
                {
                    _logger.LogInformation("✅ [SEARCH] Raw content received: {Length} chars", result.RawContent.Length);

                    try
                    {
                        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                        _logger.LogInformation("✅ [CACHE] Stored raw content result for 30 minutes");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache search result");
                    }
                }
                else
                {
                    _logger.LogInformation("❌ [SEARCH] No raw content found or request failed");
                }

                return result;
            }
            catch (RequestTimeoutException ex)
            {
                _logger.LogError(ex, "⏰ [TIMEOUT] Document search timeout for user: {UserId}", userId);
                return CreateOptimizedEmptyResponse(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [ERROR] Document search error for user: {UserId}", userId);
                return CreateOptimizedEmptyResponse(query);
            }
        }

        /// <summary>
        /// ✅ UPDATED: Get raw content only
        /// </summary>
        public async Task<string> GetRawContentAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId, 3);
            return result?.Success == true && !string.IsNullOrEmpty(result.RawContent) ? result.RawContent : null;
        }

        /// <summary>
        /// ✅ UPDATED: Get raw content with sources
        /// </summary>
        public async Task<(string RawContent, List<DocumentInfo> Sources)> GetRawContentWithSourcesAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId, 3);

            if (result?.Success == true && !string.IsNullOrEmpty(result.RawContent))
            {
                var sources = result.Sources?.Select(s => new DocumentInfo
                {
                    DocumentId = s.DocumentId,
                    Title = s.Title,
                    VersionName = s.VersionName,
                    DepartmentName = s.DepartmentName,
                    RelevanceScore = s.RelevanceScore
                }).ToList() ?? new List<DocumentInfo>();

                return (result.RawContent, sources);
            }

            return (null, new List<DocumentInfo>());
        }

        #region Legacy Method Support (Deprecated)

        /// <summary>
        /// ❌ DEPRECATED: Use GetRawContentAsync instead
        /// </summary>
        [Obsolete("Use GetRawContentAsync instead. This method is deprecated.")]
        public async Task<ChatBoxDocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId)
        {
            return await SearchDocumentsWithRAGAsync(query, userId, 3);
        }

        /// <summary>
        /// ❌ DEPRECATED: Use GetRawContentAsync instead
        /// </summary>
        [Obsolete("Use GetRawContentAsync instead. This method returns raw content now.")]
        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            return await GetRawContentAsync(query, userId);
        }

        /// <summary>
        /// ❌ DEPRECATED: Use GetRawContentWithSourcesAsync instead
        /// </summary>
        [Obsolete("Use GetRawContentWithSourcesAsync instead. This method returns raw content now.")]
        public async Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId)
        {
            var (rawContent, sources) = await GetRawContentWithSourcesAsync(query, userId);

            if (!string.IsNullOrEmpty(rawContent))
            {
                var response = new StringBuilder();
                response.AppendLine("📄 **Nội dung tài liệu:**");
                response.AppendLine(rawContent);

                if (sources?.Any() == true)
                {
                    response.AppendLine();
                    response.AppendLine("📚 **Nguồn tài liệu:**");

                    for (int i = 0; i < sources.Count; i++)
                    {
                        var source = sources[i];
                        response.AppendLine($"• {source.Title} v{source.VersionName}");
                        if (!string.IsNullOrEmpty(source.DepartmentName))
                        {
                            response.AppendLine($"  📂 {source.DepartmentName}");
                        }
                    }
                }

                return response.ToString();
            }

            return null;
        }

        #endregion

        #region Private Helper Methods (Unchanged)

        private UserContextFromJWT GetUserContextFromJWT()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("User is not authenticated, using default context");
                    return GetDefaultUserContext();
                }

                var userContext = new UserContextFromJWT
                {
                    UserId = user.FindFirst("userId")?.Value ?? string.Empty,
                    Email = user.FindFirst("email")?.Value ?? string.Empty,
                    FullName = user.FindFirst("fullName")?.Value ?? string.Empty,
                    Phone = user.FindFirst("phone")?.Value ?? string.Empty,
                    Role = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? string.Empty,
                    DepartmentId = user.FindFirst("departmentId")?.Value ?? string.Empty,
                    DepartmentName = user.FindFirst("departmentName")?.Value ?? string.Empty,
                    Permissions = ParsePermissions(user.FindFirst("permissions")?.Value)
                };

                return userContext;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract user context from JWT, using default");
                return GetDefaultUserContext();
            }
        }

        private List<string> ParsePermissions(string permissionsString)
        {
            if (string.IsNullOrEmpty(permissionsString))
                return new List<string>();

            return permissionsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();
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

        private string TruncateQueryForOptimization(string query)
        {
            const int maxLength = 120;
            if (query.Length <= maxLength) return query;

            var truncated = query.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');
            return lastSpace > 90 ? truncated.Substring(0, lastSpace) : truncated;
        }

        private ChatBoxDocumentResponse CreateOptimizedEmptyResponse(string query)
        {
            return new ChatBoxDocumentResponse
            {
                Success = true,
                RawContent = null, // ✅ No raw content found
                QueryProcessed = query,
                Sources = new List<ChatBoxDocumentSource>(),
                ProcessingTimeMs = 0
            };
        }

        #endregion
    }

    /// <summary>
    /// User context extracted from JWT token
    /// </summary>
    public class UserContextFromJWT
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new List<string>();
    }
}
