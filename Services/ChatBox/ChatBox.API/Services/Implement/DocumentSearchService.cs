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

        public async Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 15, string? documentId = null)
        {
            maxResults = Math.Min(Math.Max(maxResults, 1), 15);
            var userContext = GetUserContextFromJWT();

            var cacheKey = $"doc_raw_v3_{query.GetHashCode():X}_{userId}_{userContext.Role}_{userContext.DepartmentId}_{maxResults}_{documentId ?? "any"}";

            try
            {
                var cached = await _cacheService.GetAsync<ChatBoxDocumentResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("💾 [CACHE] Cache hit for user: {UserId} ({Role}), dept: {DeptId}",
                        userId, userContext.Role, userContext.DepartmentId);
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "💾 [CACHE] Cache read failed, proceeding with fresh search");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

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
                    MinRelevanceScore = DetermineMinRelevanceByRole(userContext.Role),
                    OnlyPublic = false,
                    OnlyOfficial = false,
                    RequestTime = DateTime.UtcNow
                };

                _logger.LogInformation("🔍 [SEARCH] Document search request - User: {FullName} ({Role}), Dept: {DeptName}, MaxResults: {MaxResults}, Query: '{Query}'",
                    request.FullName, request.Role, request.DepartmentName, request.MaxResults,
                    query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                var timeout = TimeSpan.FromSeconds(55);
                var response = await _requestClient.GetResponse<ChatBoxDocumentResponse>(
                    request,
                    timeout: timeout,
                    cancellationToken: cts.Token
                );
                var result = response.Message;

                if (!result.Success)
                {
                    _logger.LogError("❌ [SEARCH] Service error: {ErrorMessage}", result.ErrorMessage);
                    throw new InvalidOperationException($"Document service error: {result.ErrorMessage}");
                }

                if (!string.IsNullOrEmpty(result.RawContent))
                {
                    _logger.LogInformation("✅ [SEARCH] Documents found: {Length} chars, {SourceCount} sources, ProcessingTime: {ProcessingTime}ms",
                        result.RawContent.Length, result.Sources?.Count ?? 0, result.ProcessingTimeMs);

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
                else
                {
                    _logger.LogInformation("ℹ️ [SEARCH] No documents found for query: '{Query}'", query);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("⏰ [TIMEOUT] Search cancelled after 30 seconds for user: {UserId}", userId);
                throw new TimeoutException("Document search timeout after 30 seconds");
            }
            catch (RequestTimeoutException ex)
            {
                _logger.LogError(ex, "⏰ [TIMEOUT] Document search timeout ({TimeoutSeconds}s) for user: {UserId}",
                    25, userId);
                throw new TimeoutException("Document search timeout after 25 seconds");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [ERROR] Document search error for user: {UserId} - {ErrorType}: {ErrorMessage}",
                    userId, ex.GetType().Name, ex.Message);
                throw new InvalidOperationException($"Document search failed: {ex.Message}", ex);
            }
        }

        public async Task<string> GetRawContentAsync(string query, string userId, string? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("🔍 [RAW-CONTENT] Empty query provided for user: {UserId}", userId);
                return null;
            }

            try
            {
                var result = await SearchDocumentsWithRAGAsync(query, userId, 15, documentId);

                if (result?.Success == true && !string.IsNullOrEmpty(result.RawContent))
                {
                    _logger.LogDebug("✅ [RAW-CONTENT] Retrieved {Length} chars for user: {UserId}", result.RawContent.Length, userId);
                    return result.RawContent;
                }

                _logger.LogDebug("ℹ️ [RAW-CONTENT] No content found for user: {UserId}, query: '{Query}'", userId, query);
                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<(string RawContent, List<DocumentInfo> Sources)> GetRawContentWithSourcesAsync(string query, string userId, string? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("🔍 [RAW-CONTENT-SOURCES] Empty query provided for user: {UserId}", userId);
                return (null, new List<DocumentInfo>());
            }

            try
            {
                var result = await SearchDocumentsWithRAGAsync(query, userId, 15, documentId);

                if (result?.Success == true && !string.IsNullOrEmpty(result.RawContent))
                {
                    var sources = result.Sources?.Select(s => new DocumentInfo
                    {
                        DocumentId = s.DocumentId,
                        VersionId = s.VersionId,
                        Title = s.Title ?? "Unknown Document",
                        VersionName = s.VersionName ?? "1",
                        SignedBy = s.SignedBy,
                        OwnerName = s.OwnerName,
                        CreatedBy = s.CreatedBy,
                        ReviewerName = s.ReviewerName,
                        ApprovedBy = s.ApprovedBy,
                        DepartmentId = s.DepartmentId,
                        DepartmentName = s.DepartmentName,
                        EffectiveFrom = s.EffectiveFrom,
                        EffectiveUntil = s.EffectiveUntil,
                        ApprovalDate = s.ApprovalDate,
                        SignedDate = s.SignedDate,
                        ReviewDate = s.ReviewDate,
                        IsPublic = s.IsPublic,
                        Summary = s.Summary,
                        Description = s.Description,
                        Tags = s.Tags ?? new List<string>(),
                        FileType = s.FileType,
                        FileSize = s.FileSize,
                        FileName = s.FileName,
                        DocumentType = s.DocumentType,
                        Status = s.Status,
                        Category = s.Category,
                        Priority = s.Priority,
                        RelevanceScore = s.RelevanceScore,
                        IsLatestVersion = s.IsLatestVersion,
                        VersionNumber = s.VersionNumber,
                        Visibility = s.Visibility,
                        PermissionLevel = s.PermissionLevel,
                        ParentDocumentId = s.ParentDocumentId,
                        RelatedDocumentIds = s.RelatedDocumentIds ?? new List<string>()
                    }).ToList() ?? new List<DocumentInfo>();

                    _logger.LogInformation("✅ [RAW-CONTENT-SOURCES] Mapped {SourceCount} sources with ALL metadata fields", sources.Count);

                    return (result.RawContent, sources);
                }

                _logger.LogDebug("ℹ️ [RAW-CONTENT-SOURCES] No content found for user: {UserId}, query: '{Query}'", userId, query);
                return (null, new List<DocumentInfo>());
            }
            catch (Exception)
            {
                throw;
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

        private string OptimizeQueryForSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            var optimized = query.Trim()
                .Replace("?", "")
                .Replace("!", "")
                .Replace("  ", " ");

            const int maxLength = 150;
            if (optimized.Length <= maxLength)
                return optimized;

            var truncated = optimized.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');

            return lastSpace > 100 ? truncated.Substring(0, lastSpace) : truncated;
        }

        private double DetermineMinRelevanceByRole(string role)
        {
            return role?.ToUpper() switch
            {
                "ADMIN" => 0.01,
                "MANAGER" => 0.05,
                "EDITOR" => 0.08,
                "EMPLOYEE" => 0.1,
                "MEMBER" => 0.12,
                _ => 0.15
            };
        }

        private int DetermineCacheTimeByContentQuality(ChatBoxDocumentResponse result)
        {
            var baseCacheMinutes = 30;

            if (!string.IsNullOrEmpty(result.RawContent))
            {
                if (result.RawContent.Length > 2000) baseCacheMinutes += 30;
                if (result.RawContent.Length > 5000) baseCacheMinutes += 30;
            }

            if (result.Sources?.Count > 0)
            {
                baseCacheMinutes += result.Sources.Count * 5;
            }

            return Math.Min(baseCacheMinutes, 120);
        }

        public async Task<ChatBoxDocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId)
        {
            _logger.LogInformation("📋 [LEGACY] SearchOfficialDocumentsAsync called, redirecting to enhanced search");
            return await SearchDocumentsWithRAGAsync(query, userId, 15);
        }

        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            _logger.LogInformation("📋 [LEGACY] GetRAGAnswerAsync called, returning raw content");
            return await GetRawContentAsync(query, userId);
        }

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

                    for (int i = 0; i < Math.Min(sources.Count, 15); i++)
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
}

