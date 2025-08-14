using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
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
        /// ✅ ENHANCED: Get raw document content from Document microservice
        /// </summary>
        public async Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5, string? documentId = null) // ✅ THÊM documentId
        {
            // ✅ ENHANCED: Better parameter validation and optimization
            maxResults = Math.Min(Math.Max(maxResults, 1), 10); // Ensure 1-10 range
            var userContext = GetUserContextFromJWT();

            // ✅ ENHANCED: Better cache key with more context
            var cacheKey = $"doc_raw_v2_{query.GetHashCode():X}_{userId}_{userContext.Role}_{userContext.DepartmentId}_{maxResults}_{documentId ?? "any"}"; // ✅ THÊM documentId vào cache key

            try
            {
                var cached = await _cacheService.GetAsync<ChatBoxDocumentResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("💾 [CACHE] Raw content cache hit for user: {UserId} ({Role}), dept: {DeptId}",
                        userId, userContext.Role, userContext.DepartmentId);
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "💾 [CACHE] Cache read failed, proceeding with fresh search");
            }

            try
            {
                var request = new ChatBoxDocumentRequest
                {
                    DocumentId = documentId,
                    RequestId = Guid.NewGuid().ToString(),
                    Query = OptimizeQueryForSearch(query),
                    UserId = userId,
                    Email = userContext.Email,
                    FullName = userContext.FullName,
                    Phone = userContext.Phone,
                    Role = userContext.Role,
                    DepartmentId = userContext.DepartmentId,
                    DepartmentName = userContext.DepartmentName,
                    Permissions = userContext.Permissions,
                    MaxResults = maxResults,
                    MinRelevanceScore = DetermineMinRelevanceByRole(userContext.Role), // ✅ Role-based relevance
                    OnlyPublic = false,
                    OnlyOfficial = false, // ✅ REMOVED: No longer filtering by official status
                    RequestTime = DateTime.UtcNow

                };

                _logger.LogInformation("🔍 [SEARCH] Raw content request - User: {FullName} ({Role}), Dept: {DeptName}, MaxResults: {MaxResults}, Query: '{Query}'",
                    request.FullName, request.Role, request.DepartmentName, request.MaxResults,
                    query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                var timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 60));
                var response = await _requestClient.GetResponse<ChatBoxDocumentResponse>(request, timeout: timeout);
                var result = response.Message;

                // ✅ ENHANCED: Better result validation and logging
                if (result.Success && !string.IsNullOrEmpty(result.RawContent))
                {
                    _logger.LogInformation("✅ [SEARCH] Raw content received: {Length} chars, {SourceCount} sources, ProcessingTime: {ProcessingTime}ms",
                        result.RawContent.Length, result.Sources?.Count ?? 0, result.ProcessingTimeMs);

                    // ✅ ENHANCED: Intelligent caching based on content quality
                    var cacheMinutes = DetermineCacheTimeByContentQuality(result);
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(cacheMinutes));
                        _logger.LogDebug("💾 [CACHE] Stored result for {CacheMinutes} minutes", cacheMinutes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "💾 [CACHE] Failed to cache search result");
                    }
                }
                else if (result.Success)
                {
                    _logger.LogInformation("ℹ️ [SEARCH] Search completed successfully but no content found for query: '{Query}'", query);
                }
                else
                {
                    _logger.LogWarning("⚠️ [SEARCH] Search failed: {ErrorMessage}", result.ErrorMessage);
                }

                return result;
            }
            catch (RequestTimeoutException ex)
            {
                _logger.LogError(ex, "⏰ [TIMEOUT] Document search timeout ({TimeoutSeconds}s) for user: {UserId}",
                    _configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 60), userId);
                return CreateOptimizedEmptyResponse(query, "Search timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [ERROR] Document search error for user: {UserId} - {ErrorType}: {ErrorMessage}",
                    userId, ex.GetType().Name, ex.Message);
                return CreateOptimizedEmptyResponse(query, $"Search error: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ ENHANCED: Get raw content only with better error handling
        /// </summary>
        public async Task<string> GetRawContentAsync(string query, string userId, string? documentId = null) // ✅ THÊM documentId
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("🔍 [RAW-CONTENT] Empty query provided for user: {UserId}", userId);
                return null;
            }

            var result = await SearchDocumentsWithRAGAsync(query, userId, 5, documentId); // ✅ Truyền documentId

            if (result?.Success == true && !string.IsNullOrEmpty(result.RawContent))
            {
                _logger.LogDebug("✅ [RAW-CONTENT] Retrieved {Length} chars for user: {UserId}", result.RawContent.Length, userId);
                return result.RawContent;
            }

            _logger.LogDebug("❌ [RAW-CONTENT] No content found for user: {UserId}, query: '{Query}'", userId, query);
            return null;
        }

        /// <summary>
        /// ✅ ENHANCED: Get raw content with sources
        /// </summary>
        public async Task<(string RawContent, List<DocumentInfo> Sources)> GetRawContentWithSourcesAsync(string query, string userId, string? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("🔍 [RAW-CONTENT-SOURCES] Empty query provided for user: {UserId}", userId);
                return (null, new List<DocumentInfo>());
            }

            var result = await SearchDocumentsWithRAGAsync(query, userId, 5, documentId);

            if (result?.Success == true && !string.IsNullOrEmpty(result.RawContent))
            {
                // ✅ COMPLETE MAPPING từ ChatBoxDocumentSource → DocumentInfo
                var sources = result.Sources?.Select(s => new DocumentInfo
                {
                    // Core Identity
                    DocumentId = s.DocumentId,
                    VersionId = s.VersionId,
                    Title = s.Title ?? "Unknown Document",
                    VersionName = s.VersionName ?? "1",

                    // Legal & Approval Info  
                    SignedBy = s.SignedBy,
                    OwnerName = s.OwnerName,
                    CreatedBy = s.CreatedBy,
                    ReviewerName = s.ReviewerName,
                    ApprovedBy = s.ApprovedBy,

                    // Organizational Info
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.DepartmentName,

                    // Temporal Info
                    EffectiveFrom = s.EffectiveFrom,
                    EffectiveUntil = s.EffectiveUntil,
                    ApprovalDate = s.ApprovalDate,
                    SignedDate = s.SignedDate,
                    ReviewDate = s.ReviewDate,

                    // Content Info
                    Summary = s.Summary,
                    Description = s.Description,
                    Tags = s.Tags ?? new List<string>(),

                    // File Info
                    FileType = s.FileType,
                    FileSize = s.FileSize,
                    FileName = s.FileName,

                    // Document Classification
                    DocumentType = s.DocumentType,
                    Status = s.Status,
                    Category = s.Category,
                    Priority = s.Priority,

                    // Search & Relevance
                    RelevanceScore = s.RelevanceScore,

                    // Version Info
                    IsLatestVersion = s.IsLatestVersion,
                    VersionNumber = s.VersionNumber,

                    // Access Control
                    Visibility = s.Visibility,
                    PermissionLevel = s.PermissionLevel,

                    // Relationships
                    ParentDocumentId = s.ParentDocumentId,
                    RelatedDocumentIds = s.RelatedDocumentIds ?? new List<string>()

                }).ToList() ?? new List<DocumentInfo>();

                _logger.LogInformation("✅ [COMPLETE-MAPPING] Mapped {SourceCount} sources with ALL metadata fields", sources.Count);

                return (result.RawContent, sources);
            }

            _logger.LogDebug("❌ [RAW-CONTENT-SOURCES] No content found for user: {UserId}, query: '{Query}'", userId, query);
            return (null, new List<DocumentInfo>());
        }

        #region Helper Methods

        /// <summary>
        /// ✅ ENHANCED: Extract user context from JWT with better error handling
        /// </summary>
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

                // ✅ VALIDATE EXTRACTED CONTEXT
                if (string.IsNullOrEmpty(userContext.UserId))
                {
                    _logger.LogWarning("🔐 [JWT] No UserId found in JWT claims");
                }

                return userContext;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔐 [JWT] Failed to extract user context from JWT, using default");
                return GetDefaultUserContext();
            }
        }
        /// <summary>
        /// ✅ Parse permissions from JWT claim
        /// </summary>
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

        /// <summary>
        /// ✅ Default user context for unauthenticated users
        /// </summary>
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
        /// <summary>
        /// ✅ ENHANCED: Optimize query for better search results
        /// </summary>
        private string OptimizeQueryForSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            // ✅ ENHANCED: Better query optimization
            var optimized = query.Trim()
                .Replace("?", "")
                .Replace("!", "")
                .Replace("  ", " ");

            // ✅ Truncate if too long, but preserve important words
            const int maxLength = 150;
            if (optimized.Length <= maxLength)
                return optimized;

            var truncated = optimized.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');

            return lastSpace > 100 ? truncated.Substring(0, lastSpace) : truncated;
        }

        /// <summary>
        /// ✅ ENHANCED: Determine minimum relevance score based on user role
        /// </summary>
        private double DetermineMinRelevanceByRole(string role)
        {
            return role?.ToUpper() switch
            {
                "ADMIN" => 0.01,     // Admin sees everything
                "MANAGER" => 0.05,   // Manager gets broader results
                "EDITOR" => 0.08,    // Editor gets good quality results
                "EMPLOYEE" => 0.1,   // Employee gets higher quality
                "MEMBER" => 0.12,    // Member gets highest quality
                _ => 0.15            // Default/Guest gets very high quality only
            };
        }

        /// <summary>
        /// ✅ ENHANCED: Determine cache time based on content quality
        /// </summary>
        private int DetermineCacheTimeByContentQuality(ChatBoxDocumentResponse result)
        {
            // ✅ Base cache time
            var baseCacheMinutes = 30;

            // ✅ Adjust based on content length and source count
            if (!string.IsNullOrEmpty(result.RawContent))
            {
                // Longer content = cache longer
                if (result.RawContent.Length > 2000) baseCacheMinutes += 30;
                if (result.RawContent.Length > 5000) baseCacheMinutes += 30;
            }

            // ✅ Adjust based on number of sources
            if (result.Sources?.Count > 0)
            {
                baseCacheMinutes += result.Sources.Count * 5; // +5 minutes per source
            }

            // ✅ Cap at reasonable maximum
            return Math.Min(baseCacheMinutes, 120); // Max 2 hours
        }

        /// <summary>
        /// ✅ ENHANCED: Create optimized empty response
        /// </summary>
        private ChatBoxDocumentResponse CreateOptimizedEmptyResponse(string query, string reason = null)
        {
            return new ChatBoxDocumentResponse
            {
                RequestId = Guid.NewGuid().ToString(),
                Success = true, // ✅ Still success, just no content found
                RawContent = string.Empty,
                QueryProcessed = query ?? string.Empty,
                Sources = new List<ChatBoxDocumentSource>(),
                ProcessingTimeMs = 0,
                ErrorMessage = reason
            };
        }

        #endregion

        #region Legacy Method Support (Backwards Compatibility)

        /// <summary>
        /// ✅ LEGACY SUPPORT: Maintain backwards compatibility
        /// </summary>
        public async Task<ChatBoxDocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId)
        {
            _logger.LogInformation("📋 [LEGACY] SearchOfficialDocumentsAsync called, redirecting to enhanced search");
            return await SearchDocumentsWithRAGAsync(query, userId, 5);
        }

        /// <summary>
        /// ✅ LEGACY SUPPORT: For existing callers expecting processed answers
        /// </summary>
        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            _logger.LogInformation("📋 [LEGACY] GetRAGAnswerAsync called, returning raw content");
            return await GetRawContentAsync(query, userId);
        }

        /// <summary>
        /// ✅ LEGACY SUPPORT: For existing callers expecting formatted answers with sources
        /// </summary>
        public async Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId)
        {
            _logger.LogInformation("📋 [LEGACY] GetRAGAnswerWithSourcesAsync called, formatting raw content with sources");

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

                    for (int i = 0; i < Math.Min(sources.Count, 5); i++)
                    {
                        var source = sources[i];
                        response.AppendLine($"• {source.Title} v{source.VersionName}");
                        if (!string.IsNullOrEmpty(source.DepartmentId))
                        {
                            response.AppendLine($"  📂 {source.DepartmentId}");
                        }
                        if (source.RelevanceScore > 0)
                        {
                            response.AppendLine($"  📊 Độ liên quan: {source.RelevanceScore:P1}");
                        }
                    }
                }

                return response.ToString();
            }

            return null;
        }

    }
    #endregion
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

