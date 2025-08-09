using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using System.Text;

namespace Document.API.Services.Implements
    {
        public class DocumentRAGService : BaseService<DocumentRAGService>, IDocumentRAGService
        {
            #region Fields
            private readonly IKernelMemory _memory;
            private readonly INameLookupService _nameLookupService;
            private readonly IDocumentEnrichmentService _enrichmentService;

            // ✅ EXTRACTION SETTINGS - Focus on raw content extraction
            private readonly bool _enableCostOptimization;
            private readonly int _maxSearchResults;
            private readonly double _optimizedMinRelevanceScore;
            private readonly TimeSpan _cacheExpiration;
            private readonly int _maxRawContentLength;
            private readonly bool _combineMultipleSources;
            #endregion

            #region Constructor
            public DocumentRAGService(
                IKernelMemory memory,
                IUnitOfWork unitOfWork,
                ILogger<DocumentRAGService> logger,
                IConfiguration configuration,
                INameLookupService nameLookupService,
                IDocumentEnrichmentService enrichmentService,
                IMapper mapper,
                IHttpContextAccessor httpContextAccessor)
                : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
            {
                _memory = memory;
                _nameLookupService = nameLookupService;
                _enrichmentService = enrichmentService;

                // ✅ CONFIGURATION FOR RAW CONTENT EXTRACTION
                _enableCostOptimization = _configuration.GetValue<bool>("RAG:EnableCostOptimization", true);
                _maxSearchResults = _configuration.GetValue<int>("RAG:MaxSearchResults", 3);
                _optimizedMinRelevanceScore = _configuration.GetValue<double>("RAG:OptimizedMinRelevanceScore", 0.28);
                _cacheExpiration = TimeSpan.FromHours(_configuration.GetValue<int>("RAG:CacheExpirationHours", 1));
                _maxRawContentLength = _configuration.GetValue<int>("RAG:MaxRawContentLength", 2000);
                _combineMultipleSources = _configuration.GetValue<bool>("RAG:CombineMultipleSources", true);
            }
            #endregion

            #region Public Methods
            public async Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request)
            {
                try
                {
                    var startTime = DateTime.UtcNow;

                    _logger.LogInformation("🔍 [RAG] Processing RAW CONTENT search for: {FullName} ({Role}) from {DeptName}",
                        request.FullName, request.Role, request.DepartmentName);

                    if (_enableCostOptimization)
                    {
                        request = ApplyOptimizedSettings(request);
                    }

                    var citations = await PerformAdvancedSearch(request);

                    if (!citations.Any())
                    {
                        _logger.LogInformation("❌ [RAG] No citations found for query: {Query}", request.Query);
                        return CreateEmptyResponse(request, startTime);
                    }

                    var validCitations = citations
                        .Where(c => IsDocumentAccessibleToUser(c, request))
                        .OrderByDescending(c => c.Partitions.Max(p => p.Relevance))
                        .Take(3)
                        .ToList();

                    _logger.LogInformation("✅ [RAG] Permission filtered: {Valid}/{Total} citations for role: {Role}",
                        validCitations.Count, citations.Count, request.Role);

                    if (!validCitations.Any())
                    {
                        _logger.LogInformation("❌ [RAG] No accessible documents after permission filtering for user: {FullName}", request.FullName);
                        return CreateEmptyResponse(request, startTime);
                    }

                    // ✅ NEW: EXTRACT RAW CONTENT ONLY - NO AI PROCESSING
                    var rawContent = ExtractRawContentFromCitations(validCitations);
                    var sources = await ExtractDocumentSources(validCitations);

                    var response = new DocumentRAGResponse
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        RawContent = rawContent, // ✅ Raw content instead of processed answer
                        Sources = sources,
                        QueryProcessed = request.Query,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                    };

                    _logger.LogInformation("✅ [RAG] RAW CONTENT extracted: {RequestId} - {ProcessingTime}ms - {ContentLength} chars - {SourceCount} sources",
                        request.RequestId, response.ProcessingTimeMs, rawContent?.Length ?? 0, sources.Count);

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [RAG] Error processing request for user: {FullName} ({Role})", request.FullName, request.Role);
                    return CreateErrorResponse(request, ex);
                }
            }

            public async Task<DocumentRAGResponse> SearchOfficialDocumentsAsync(DocumentRAGRequest request)
            {
                var officialRequest = request with
                {
                    MinRelevanceScore = Math.Max(request.MinRelevanceScore ?? _optimizedMinRelevanceScore, 0.4),
                    OnlyOfficial = true
                };

                return await SearchDocumentsWithRAGAsync(officialRequest);
            }

            public async Task<string> GetRawContentAsync(string query, string userId)
            {
                var request = new DocumentRAGRequest
                {
                    Query = query,
                    UserId = userId,
                    MaxResults = 3,
                    MinRelevanceScore = _optimizedMinRelevanceScore
                };

                var response = await SearchDocumentsWithRAGAsync(request);
                return response.Success ? response.RawContent : null;
            }

            public async Task<(string RawContent, List<DocumentSourceResponse> Sources)> GetRawContentWithSourcesAsync(string query, string userId)
            {
                var request = new DocumentRAGRequest
                {
                    Query = query,
                    UserId = userId,
                    MaxResults = 3,
                    MinRelevanceScore = _optimizedMinRelevanceScore
                };

                var response = await SearchDocumentsWithRAGAsync(request);

                return response.Success
                    ? (response.RawContent, response.Sources)
                    : (null, new List<DocumentSourceResponse>());
            }
            #endregion

            #region Private Methods - RAW CONTENT EXTRACTION

            /// <summary>
            /// ✅ NEW: Extract raw content from citations without AI processing
            /// </summary>
            private string ExtractRawContentFromCitations(List<Citation> citations)
            {
                if (!citations.Any())
                {
                    _logger.LogWarning("📄 [RAW] No citations provided for content extraction");
                    return null;
                }

                var contentParts = new List<RawContentPart>();

                _logger.LogDebug("📄 [RAW] Processing {Count} citations for raw content extraction", citations.Count);

                foreach (var citation in citations.Take(3))
                {
                    var bestPartition = citation.Partitions
                        .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                        .OrderByDescending(p => p.Relevance)
                        .FirstOrDefault();

                    if (bestPartition?.Text != null)
                    {
                        var rawPart = new RawContentPart
                        {
                            Content = CleanRawText(bestPartition.Text),
                            Relevance = bestPartition.Relevance,
                            DocumentId = GetDocumentIdFromCitation(citation)
                        };

                        if (!string.IsNullOrEmpty(rawPart.Content))
                        {
                            contentParts.Add(rawPart);
                            _logger.LogDebug("📄 [RAW] Extracted from document {DocId}: {Length} chars, relevance: {Relevance:F3}",
                                rawPart.DocumentId, rawPart.Content.Length, bestPartition.Relevance);
                        }
                    }
                }

                if (!contentParts.Any())
                {
                    _logger.LogWarning("📄 [RAW] No valid raw content extracted from citations");
                    return null;
                }

                return CombineRawContent(contentParts);
            }

            /// <summary>
            /// Clean raw text but preserve original meaning
            /// </summary>
            private string CleanRawText(string rawText)
            {
                if (string.IsNullOrWhiteSpace(rawText))
                    return string.Empty;

                return rawText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\t", " ")
                    .Replace("  ", " ")
                    .Trim();
            }

            /// <summary>
            /// Combine multiple raw content parts
            /// </summary>
            private string CombineRawContent(List<RawContentPart> contentParts)
            {
                if (!contentParts.Any())
                    return null;

                if (!_combineMultipleSources)
                {
                    // Return only the most relevant content
                    var bestContent = contentParts.OrderByDescending(p => p.Relevance).First();
                    return TruncateIfNeeded(bestContent.Content);
                }

                var combinedContent = new StringBuilder();

                for (int i = 0; i < contentParts.Count; i++)
                {
                    var part = contentParts[i];

                    if (i > 0)
                    {
                        combinedContent.AppendLine();
                        combinedContent.AppendLine("---");
                        combinedContent.AppendLine();
                    }

                    combinedContent.Append(part.Content);

                    // Check length limit
                    if (combinedContent.Length > _maxRawContentLength)
                    {
                        _logger.LogDebug("📄 [RAW] Content truncated at {Length} chars due to length limit", _maxRawContentLength);
                        break;
                    }
                }

                return TruncateIfNeeded(combinedContent.ToString());
            }

            /// <summary>
            /// Truncate content if it exceeds maximum length
            /// </summary>
            private string TruncateIfNeeded(string content)
            {
                if (string.IsNullOrEmpty(content) || content.Length <= _maxRawContentLength)
                    return content;

                var truncated = content.Substring(0, _maxRawContentLength);

                // Try to truncate at sentence boundary
                var lastPeriod = truncated.LastIndexOf('.');
                if (lastPeriod > _maxRawContentLength * 0.8) // If period is near the end
                {
                    return truncated.Substring(0, lastPeriod + 1);
                }

                // Try to truncate at word boundary
                var lastSpace = truncated.LastIndexOf(' ');
                if (lastSpace > _maxRawContentLength * 0.9) // If space is near the end
                {
                    return truncated.Substring(0, lastSpace);
                }

                return truncated + "...";
            }

            /// <summary>
            /// Supporting class for raw content processing
            /// </summary>
            private class RawContentPart
            {
                public string Content { get; set; } = string.Empty;
                public double Relevance { get; set; }
                public string DocumentId { get; set; } = string.Empty;
            }

            #endregion

            #region Private Methods - Request Processing (Unchanged)
            private DocumentRAGRequest ApplyOptimizedSettings(DocumentRAGRequest request)
            {
                return new DocumentRAGRequest
                {
                    RequestId = request.RequestId,
                    Query = TruncateQueryForCostOptimization(request.Query),
                    UserId = request.UserId,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Role = request.Role,
                    DepartmentId = request.DepartmentId,
                    DepartmentName = request.DepartmentName,
                    Permissions = request.Permissions,
                    MaxResults = Math.Min(request.MaxResults, _maxSearchResults),
                    MinRelevanceScore = Math.Max(request.MinRelevanceScore ?? _optimizedMinRelevanceScore, _optimizedMinRelevanceScore),
                    OnlyPublic = request.OnlyPublic,
                    OnlyOfficial = true,
                    Tags = request.Tags,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil,
                    RequestTime = request.RequestTime
                };
            }

            private string TruncateQueryForCostOptimization(string query)
            {
                const int maxQueryLength = 120;
                if (string.IsNullOrEmpty(query) || query.Length <= maxQueryLength)
                    return query;

                var truncated = query.Substring(0, maxQueryLength);
                var lastSpace = truncated.LastIndexOf(' ');
                return lastSpace > 90 ? truncated.Substring(0, lastSpace) : truncated;
            }
            #endregion

            #region Private Methods - Advanced Search (Unchanged)
            private async Task<List<Citation>> PerformAdvancedSearch(DocumentRAGRequest request)
            {
                try
                {
                    var baseFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("isOfficial", "true");

                    var searchLimit = DetermineSearchLimitByRole(request.Role, request.MaxResults);

                    _logger.LogInformation("🔍 [SEARCH] Searching with limit: {Limit}, minRelevance: {MinRelevance}",
                        searchLimit, request.MinRelevanceScore);

                    var searchResult = await _memory.SearchAsync(
                        request.Query,
                        limit: searchLimit,
                        filter: baseFilter,
                        minRelevance: request.MinRelevanceScore ?? _optimizedMinRelevanceScore);

                    _logger.LogInformation("🔍 [SEARCH] KernelMemory returned {Count} results", searchResult.Results.Count());

                    return searchResult.Results.ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in KernelMemory search for user: {FullName}", request.FullName);
                    return new List<Citation>();
                }
            }

            private int DetermineSearchLimitByRole(string role, int requestedMaxResults)
            {
                var multiplier = role.ToUpper() switch
                {
                    "ADMIN" => 2,
                    "MANAGER" => 3,
                    "EDITOR" => 4,
                    "MEMBER" => 4,
                    _ => 5
                };

                var searchLimit = requestedMaxResults * multiplier;
                return Math.Min(searchLimit, 15);
            }
            #endregion

            #region Private Methods - Access Control (Unchanged)
            private bool IsDocumentAccessibleToUser(Citation citation, DocumentRAGRequest userContext)
            {
                try
                {
                    var firstPartition = citation.Partitions.FirstOrDefault();
                    if (firstPartition?.Tags == null) return false;

                    var tags = firstPartition.Tags;

                    if (!IsDocumentCurrentlyEffective(tags))
                        return false;

                    var departmentId = firstPartition.Tags
                        .FirstOrDefault(t => t.Key == "departmentId")
                        .Value?.FirstOrDefault() ?? "";

                    var isPublicStr = firstPartition.Tags
                        .FirstOrDefault(t => t.Key == "isPublic")
                        .Value?.FirstOrDefault() ?? "false";

                    var ownerId = firstPartition.Tags
                        .FirstOrDefault(t => t.Key == "ownerId")
                        .Value?.FirstOrDefault() ?? "";

                    bool.TryParse(isPublicStr, out bool isPublic);

                    var accessResult = CheckRoleBasedDocumentAccess(
                        departmentId,
                        isPublic,
                        ownerId,
                        userContext);

                    return accessResult.HasAccess;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in permission check for user: {FullName} - denying access", userContext.FullName);
                    return false;
                }
            }

            private (bool HasAccess, string Reason) CheckRoleBasedDocumentAccess(
                string documentDepartmentId,
                bool isPublic,
                string ownerId,
                DocumentRAGRequest userContext)
            {
                var role = userContext.Role.ToUpper();

                if (role == "ADMIN")
                    return (true, "Admin access");

                if (ownerId == userContext.UserId)
                    return (true, "Owner access");

                if (isPublic)
                    return (true, "Public document");

                if (!string.IsNullOrEmpty(userContext.DepartmentId) && documentDepartmentId == userContext.DepartmentId)
                {
                    switch (role)
                    {
                        case "MANAGER":
                            return (true, $"Manager access - department documents");

                        case "EDITOR":
                        case "MEMBER":
                            if (userContext.Permissions.Contains("VIEW_DEPARTMENT_DOCUMENT") ||
                                userContext.Permissions.Contains("VIEW_OWN_DEPARTMENT_DOCUMENT"))
                            {
                                return (true, $"{role} access with permission");
                            }
                            return (false, $"{role} missing VIEW_DEPARTMENT_DOCUMENT permission");

                        case "NONE":
                            return (false, "Role 'None' - no access");

                        default:
                            return (false, $"Unknown role '{userContext.Role}'");
                    }
                }

                return (false, $"No access - Role: {userContext.Role}, UserDept: {userContext.DepartmentName}, DocDept: {documentDepartmentId}");
            }

            private bool IsDocumentCurrentlyEffective(IDictionary<string, List<string>> tags)
            {
                var today = DateTime.UtcNow.Date;

                if (tags.TryGetValue("effectiveFrom", out var effectiveFromValues))
                {
                    var effectiveFromStr = effectiveFromValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(effectiveFromStr) &&
                        DateTime.TryParse(effectiveFromStr, out var effectiveFrom) &&
                        today < effectiveFrom.Date)
                    {
                        return false;
                    }
                }

                if (tags.TryGetValue("effectiveUntil", out var effectiveUntilValues))
                {
                    var effectiveUntilStr = effectiveUntilValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(effectiveUntilStr) &&
                        DateTime.TryParse(effectiveUntilStr, out var effectiveUntil) &&
                        today > effectiveUntil.Date)
                    {
                        return false;
                    }
                }

                return true;
            }
            #endregion

            #region Private Methods - Source Extraction (Unchanged)
            private async Task<List<DocumentSourceResponse>> ExtractDocumentSources(List<Citation> citations)
            {
                try
                {
                    var sources = new List<DocumentSourceResponse>();

                    for (int i = 0; i < citations.Count && i < 3; i++)
                    {
                        var citation = citations[i];
                        var documentId = GetDocumentIdFromCitation(citation);

                        if (string.IsNullOrEmpty(documentId)) continue;

                        var source = new DocumentSourceResponse
                        {
                            DocumentId = documentId,
                            RelevanceScore = citation.Partitions.Max(p => p.Relevance)
                        };

                        var firstPartition = citation.Partitions.FirstOrDefault();
                        if (firstPartition?.Tags != null)
                        {
                            var tags = firstPartition.Tags;
                            source.VersionName = tags.FirstOrDefault(t => t.Key == "versionName").Value?.FirstOrDefault() ?? "";
                            source.DepartmentId = tags.FirstOrDefault(t => t.Key == "departmentId").Value?.FirstOrDefault() ?? "";
                        }

                        try
                        {
                            var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                                .SingleOrDefaultAsync(
                                    predicate: dv => dv.DocumentFile.Id == documentId && dv.IsOfficial,
                                    include: i => i.Include(dv => dv.DocumentFile));

                            if (documentVersion != null)
                            {
                                source.Title = documentVersion.Title;
                                source.DepartmentId = documentVersion.DocumentFile.DepartmentId;
                            }
                            else
                            {
                                source.Title = $"Tài liệu nội bộ {i + 1}";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get document details for {DocumentId}", documentId);
                            source.Title = $"Tài liệu nội bộ {i + 1}";
                        }

                        sources.Add(source);
                    }

                    return sources.OrderByDescending(s => s.RelevanceScore).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting document sources");
                    return new List<DocumentSourceResponse>();
                }
            }

            private string GetDocumentIdFromCitation(Citation citation)
            {
                var firstPartition = citation.Partitions.FirstOrDefault();
                if (firstPartition?.Tags != null && firstPartition.Tags.ContainsKey("documentId"))
                {
                    return firstPartition.Tags["documentId"].FirstOrDefault() ?? "";
                }
                return "";
            }
            #endregion

            #region Private Methods - Response Creation
            private DocumentRAGResponse CreateEmptyResponse(DocumentRAGRequest request, DateTime startTime)
            {
                return new DocumentRAGResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    RawContent = null, // ✅ No raw content found
                    Sources = new List<DocumentSourceResponse>(),
                    QueryProcessed = request.Query,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }

            private DocumentRAGResponse CreateErrorResponse(DocumentRAGRequest request, Exception ex)
            {
                return new DocumentRAGResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorMessage = $"Lỗi xử lý RAG: {ex.Message}",
                    Sources = new List<DocumentSourceResponse>(),
                    QueryProcessed = request.Query
                };
            }
            #endregion
        }
    }
