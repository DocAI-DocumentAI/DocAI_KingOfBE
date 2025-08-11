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

        // ✅ PRODUCTION CONFIGURATION
        private readonly bool _enableDebugLogging;
        private readonly int _maxSearchResults;
        private readonly double _baseMinRelevanceScore;
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

            _enableDebugLogging = _configuration.GetValue<bool>("RAG:EnableDebugLogging", true);
            _maxSearchResults = _configuration.GetValue<int>("RAG:MaxSearchResults", 50);
            _baseMinRelevanceScore = _configuration.GetValue<double>("RAG:BaseMinRelevanceScore", 0.01);
        }
        #endregion

        #region Public Methods
        public async Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request)
        {
            var requestId = request.RequestId ?? Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔍 [RAG-{RequestId}] Starting search - Query: '{Query}', User: {FullName} ({Role}), Dept: {DeptName}",
                    requestId, request.Query, request.FullName, request.Role, request.DepartmentName);

                // ✅ VALIDATE REQUEST
                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    return CreateEmptyResponse(request, startTime, "Empty query provided");
                }

                // ✅ SMART SEARCH WITH PROPER FILTERS
                var citations = await PerformSmartSearch(request, requestId);

                if (!citations.Any())
                {
                    _logger.LogInformation("❌ [RAG-{RequestId}] No citations found for query: '{Query}'", requestId, request.Query);
                    return CreateEmptyResponse(request, startTime, "No documents found");
                }

                _logger.LogInformation("📄 [RAG-{RequestId}] Found {Count} citations from KernelMemory", requestId, citations.Count);

                // ✅ PERMISSION FILTERING
                var validCitations = await FilterCitationsByPermissions(citations, request, requestId);

                _logger.LogInformation("🔒 [RAG-{RequestId}] Permission filtered: {Valid}/{Total} citations for role: {Role}",
                    requestId, validCitations.Count, citations.Count, request.Role);

                if (!validCitations.Any())
                {
                    return CreateEmptyResponse(request, startTime, "No accessible documents after permission filtering");
                }

                // ✅ EXTRACT RAW CONTENT AND SOURCES
                var rawContent = ExtractRawContentFromCitations(validCitations);
                var sources = await ExtractDocumentSources(validCitations, requestId);

                var response = new DocumentRAGResponse
                {
                    RequestId = requestId,
                    Success = true,
                    RawContent = rawContent,
                    Sources = sources,
                    QueryProcessed = request.Query,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };

                _logger.LogInformation("✅ [RAG-{RequestId}] Success: {ProcessingTime}ms, Content: {ContentLength} chars, Sources: {SourceCount}",
                    requestId, response.ProcessingTimeMs, rawContent?.Length ?? 0, sources.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [RAG-{RequestId}] Error processing request for user: {FullName} ({Role})",
                    requestId, request.FullName, request.Role);
                return CreateErrorResponse(request, ex, requestId);
            }
        }

        public async Task<string> GetRawContentAsync(string query, string userId)
        {
            var request = new DocumentRAGRequest
            {
                Query = query,
                UserId = userId,
                Role = "ADMIN", // Default for simple calls
                MaxResults = 5,
                MinRelevanceScore = _baseMinRelevanceScore
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
                Role = "ADMIN", // Default for simple calls
                MaxResults = 5,
                MinRelevanceScore = _baseMinRelevanceScore
            };

            var response = await SearchDocumentsWithRAGAsync(request);

            return response.Success
                ? (response.RawContent, response.Sources)
                : (null, new List<DocumentSourceResponse>());
        }
        #endregion

        #region Smart Search Logic

        private async Task<List<Citation>> PerformSmartSearch(DocumentRAGRequest request, string requestId)
        {
            try
            {
                _logger.LogInformation("🔎 [SEARCH-{RequestId}] Smart search - Query: '{Query}', Role: {Role}", requestId, request.Query, request.Role);

                // ✅ BUILD COMPREHENSIVE FILTER
                var filter = BuildSmartFilter(request, requestId);

                // ✅ DETERMINE SEARCH PARAMETERS
                var searchLimit = DetermineSearchLimit(request.Role, request.MaxResults);
                var minRelevance = Math.Max(request.MinRelevanceScore ?? _baseMinRelevanceScore, 0.01);

                // ✅ PRIMARY SEARCH WITH FILTERS
                try
                {
                    _logger.LogInformation("🔎 [SEARCH-{RequestId}] Primary search with filters - Limit: {Limit}, MinRelevance: {MinRelevance}",
                        requestId, searchLimit, minRelevance);

                    var primaryResult = await _memory.SearchAsync(
                        request.Query,
                        limit: searchLimit,
                        filter: filter,
                        minRelevance: minRelevance);

                    var primaryCitations = primaryResult.Results.ToList();

                    if (primaryCitations.Any())
                    {
                        _logger.LogInformation("✅ [SEARCH-{RequestId}] Primary search found {Count} results", requestId, primaryCitations.Count);
                        LogSampleResults(primaryCitations, requestId, "Primary");
                        return primaryCitations;
                    }

                    _logger.LogInformation("🔎 [SEARCH-{RequestId}] Primary search found no results, trying fallback", requestId);
                }
                catch (Exception primaryEx)
                {
                    _logger.LogWarning(primaryEx, "🔎 [SEARCH-{RequestId}] Primary search failed, trying fallback", requestId);
                }

                // ✅ FALLBACK: RELAXED SEARCH
                try
                {
                    _logger.LogInformation("🔎 [SEARCH-{RequestId}] Fallback search with relaxed filters", requestId);

                    var fallbackFilter = BuildFallbackFilter(request, requestId);
                    var fallbackResult = await _memory.SearchAsync(
                        request.Query,
                        limit: searchLimit,
                        filter: fallbackFilter,
                        minRelevance: 0.01);

                    var fallbackCitations = fallbackResult.Results.ToList();

                    if (fallbackCitations.Any())
                    {
                        _logger.LogInformation("✅ [SEARCH-{RequestId}] Fallback search found {Count} results", requestId, fallbackCitations.Count);
                        LogSampleResults(fallbackCitations, requestId, "Fallback");
                        return fallbackCitations;
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "🔎 [SEARCH-{RequestId}] Fallback search failed", requestId);
                }

                // ✅ LAST RESORT: NO FILTERS
                try
                {
                    _logger.LogInformation("🔎 [SEARCH-{RequestId}] Last resort search without filters", requestId);

                    var lastResortResult = await _memory.SearchAsync(
                        request.Query,
                        limit: Math.Min(searchLimit, 20),
                        minRelevance: 0.0);

                    var lastResortCitations = lastResortResult.Results.ToList();

                    if (lastResortCitations.Any())
                    {
                        _logger.LogInformation("✅ [SEARCH-{RequestId}] Last resort found {Count} results", requestId, lastResortCitations.Count);
                        return lastResortCitations;
                    }
                }
                catch (Exception lastResortEx)
                {
                    _logger.LogError(lastResortEx, "❌ [SEARCH-{RequestId}] Last resort search failed", requestId);
                }

                return new List<Citation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SEARCH-{RequestId}] Error in smart search", requestId);
                return new List<Citation>();
            }
        }

        /// <summary>
        /// ✅ BUILD SMART FILTER with proper date handling
        /// </summary>
        private MemoryFilter BuildSmartFilter(DocumentRAGRequest request, string requestId)
        {
            var filter = new MemoryFilter();
            var today = DateTime.UtcNow.Date;

            try
            {
                // ✅ ALWAYS FILTER BY APPROVED STATUS
                filter = filter.ByTag("status", "approved");

                // ✅ DEPARTMENT FILTER - if specified
                if (!string.IsNullOrEmpty(request.DepartmentId))
                {
                    filter = filter.ByTag("departmentId", request.DepartmentId);
                    _logger.LogDebug("🔎 [FILTER-{RequestId}] Added department filter: {DeptId}", requestId, request.DepartmentId);
                }

                // ✅ EFFECTIVE DATE FILTER - documents effective TODAY or in range
                // Logic: effectiveFrom <= TODAY <= effectiveUntil (hoặc null)

                // Skip date filtering for now since it's complex with MemoryFilter
                // We'll handle date filtering in permission check instead

                // ✅ PUBLIC/PERMISSION BASED FILTER
                var role = request.Role?.ToUpper() ?? "GUEST";

                if (role == "GUEST" || request.OnlyPublic)
                {
                    filter = filter.ByTag("isPublic", "True");
                    _logger.LogDebug("🔎 [FILTER-{RequestId}] Added public filter for role: {Role}", requestId, role);
                }

                _logger.LogInformation("🔎 [FILTER-{RequestId}] Built smart filter for role: {Role}, dept: {DeptId}",
                    requestId, role, request.DepartmentId);

                return filter;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔎 [FILTER-{RequestId}] Error building smart filter, using basic filter", requestId);
                return new MemoryFilter().ByTag("status", "approved");
            }
        }

        /// <summary>
        /// ✅ BUILD FALLBACK FILTER - more permissive
        /// </summary>
        private MemoryFilter BuildFallbackFilter(DocumentRAGRequest request, string requestId)
        {
            try
            {
                var filter = new MemoryFilter().ByTag("status", "approved");

                // Only add department filter if specified and not admin
                if (!string.IsNullOrEmpty(request.DepartmentId) && request.Role?.ToUpper() != "ADMIN")
                {
                    filter = filter.ByTag("departmentId", request.DepartmentId);
                }

                _logger.LogDebug("🔎 [FILTER-{RequestId}] Built fallback filter", requestId);
                return filter;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔎 [FILTER-{RequestId}] Error building fallback filter", requestId);
                return new MemoryFilter().ByTag("status", "approved");
            }
        }

        /// <summary>
        /// ✅ PERMISSION FILTERING with proper date checks
        /// </summary>
        private async Task<List<Citation>> FilterCitationsByPermissions(List<Citation> citations, DocumentRAGRequest userContext, string requestId)
        {
            var validCitations = new List<Citation>();
            var today = DateTime.UtcNow.Date;

            foreach (var citation in citations)
            {
                try
                {
                    var documentId = GetDocumentIdFromCitation(citation);
                    var versionId = GetVersionIdFromCitation(citation);

                    // ✅ CHECK DATE EFFECTIVENESS (including today)
                    if (!IsDocumentCurrentlyEffective(citation, today, requestId))
                    {
                        _logger.LogDebug("⏰ [ACCESS-{RequestId}] Document {DocId}/{VersionId} not currently effective",
                            requestId, documentId, versionId);
                        continue;
                    }

                    // ✅ CHECK ACCESS PERMISSIONS
                    var hasAccess = await IsDocumentAccessibleToUser(citation, userContext, requestId);

                    if (hasAccess)
                    {
                        validCitations.Add(citation);
                        _logger.LogDebug("✅ [ACCESS-{RequestId}] Document {DocId}/{VersionId} accessible",
                            requestId, documentId, versionId);
                    }
                    else
                    {
                        _logger.LogDebug("❌ [ACCESS-{RequestId}] Document {DocId}/{VersionId} NOT accessible",
                            requestId, documentId, versionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔒 [ACCESS-{RequestId}] Error checking permission for citation - skipping", requestId);
                }
            }

            var result = validCitations
                .OrderByDescending(c => c.Partitions.Any() ? c.Partitions.Max(p => p.Relevance) : 0)
                .Take(userContext.MaxResults)
                .ToList();

            return result;
        }

        /// <summary>
        /// ✅ CHECK DOCUMENT EFFECTIVENESS - includes TODAY
        /// </summary>
        private bool IsDocumentCurrentlyEffective(Citation citation, DateTime today, string requestId)
        {
            try
            {
                var effectiveFromStr = GetTagValueFromCitation(citation, "effectiveFrom");
                var effectiveUntilStr = GetTagValueFromCitation(citation, "effectiveUntil");

                // ✅ CHECK EFFECTIVE FROM - document should be effective from today or earlier
                if (!string.IsNullOrEmpty(effectiveFromStr))
                {
                    if (DateTime.TryParse(effectiveFromStr, out var effectiveFrom))
                    {
                        if (today < effectiveFrom.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document not yet effective: {EffectiveFrom} > {Today}",
                                requestId, effectiveFrom.Date, today);
                            return false; // Not yet effective
                        }
                    }
                }

                // ✅ CHECK EFFECTIVE UNTIL - document should be effective until today or later
                if (!string.IsNullOrEmpty(effectiveUntilStr))
                {
                    if (DateTime.TryParse(effectiveUntilStr, out var effectiveUntil))
                    {
                        if (today > effectiveUntil.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document already expired: {EffectiveUntil} < {Today}",
                                requestId, effectiveUntil.Date, today);
                            return false; // Already expired
                        }
                    }
                }

                // ✅ Document is currently effective (including today)
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⏰ [EFFECTIVE-{RequestId}] Error checking document effectiveness - allowing access", requestId);
                return true; // Allow access on error
            }
        }

        /// <summary>
        /// ✅ CHECK DOCUMENT ACCESS PERMISSIONS
        /// </summary>
        private async Task<bool> IsDocumentAccessibleToUser(Citation citation, DocumentRAGRequest userContext, string requestId)
        {
            try
            {
                var role = userContext.Role?.ToUpper() ?? "GUEST";

                // ✅ ADMIN USERS HAVE FULL ACCESS
                if (role == "ADMIN")
                {
                    return true;
                }

                // ✅ GET DOCUMENT PROPERTIES
                var departmentId = GetTagValueFromCitation(citation, "departmentId");
                var ownerId = GetTagValueFromCitation(citation, "ownerId");
                var isPublicStr = GetTagValueFromCitation(citation, "isPublic");
                bool.TryParse(isPublicStr, out bool isPublic);

                // ✅ OWNER ACCESS
                if (!string.IsNullOrEmpty(ownerId) && ownerId == userContext.UserId)
                {
                    return true;
                }

                // ✅ PUBLIC DOCUMENTS
                if (isPublic)
                {
                    return true;
                }

                // ✅ DEPARTMENT ACCESS
                if (!string.IsNullOrEmpty(userContext.DepartmentId) && departmentId == userContext.DepartmentId)
                {
                    switch (role)
                    {
                        case "MANAGER":
                        case "EDITOR":
                        case "EMPLOYEE":
                        case "MEMBER":
                            return true;
                    }
                }

                // ✅ PERMISSION-BASED ACCESS
                if (userContext.Permissions?.Any(p => new[] { "VIEW_ANY_DOCUMENT", "VIEW_DEPARTMENT_DOCUMENT" }.Contains(p)) == true)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔒 [ACCESS-{RequestId}] Error checking document access - denying access", requestId);
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private int DetermineSearchLimit(string role, int requestedMaxResults)
        {
            var multiplier = role?.ToUpper() switch
            {
                "ADMIN" => 2,
                "MANAGER" => 3,
                _ => 4
            };

            var searchLimit = requestedMaxResults * multiplier;
            return Math.Min(searchLimit, 50);
        }

        private void LogSampleResults(List<Citation> citations, string requestId, string searchType)
        {
            if (!_enableDebugLogging) return;

            for (int i = 0; i < Math.Min(citations.Count, 2); i++)
            {
                var citation = citations[i];
                var documentId = GetDocumentIdFromCitation(citation);
                var title = GetTagValueFromCitation(citation, "title");
                var status = GetTagValueFromCitation(citation, "status");
                var relevance = citation.Partitions.Any() ? citation.Partitions.Max(p => p.Relevance) : 0;

                _logger.LogInformation("📄 [SEARCH-{RequestId}] {SearchType} Result {Index}: Title='{Title}', DocId={DocId}, Status={Status}, Relevance={Relevance:F3}",
                    requestId, searchType, i + 1, title, documentId, status, relevance);
            }
        }

        private string GetDocumentIdFromCitation(Citation citation)
        {
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags != null)
            {
                var possibleTags = new[] { "documentId", "__document_id", "docId", "document_id" };
                foreach (var tag in possibleTags)
                {
                    if (firstPartition.Tags.TryGetValue(tag, out var values))
                    {
                        var value = values.FirstOrDefault();
                        if (!string.IsNullOrEmpty(value))
                            return value;
                    }
                }
            }
            return string.Empty;
        }

        private string GetVersionIdFromCitation(Citation citation)
        {
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags != null)
            {
                var possibleTags = new[] { "versionId", "version_id", "__version_id" };
                foreach (var tag in possibleTags)
                {
                    if (firstPartition.Tags.TryGetValue(tag, out var values))
                    {
                        var value = values.FirstOrDefault();
                        if (!string.IsNullOrEmpty(value))
                            return value;
                    }
                }
            }
            return string.Empty;
        }

        private string GetTagValueFromCitation(Citation citation, string tagKey)
        {
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags != null && firstPartition.Tags.TryGetValue(tagKey, out var values))
            {
                return values.FirstOrDefault() ?? string.Empty;
            }
            return string.Empty;
        }

        #endregion

        #region Source Extraction

        private async Task<List<DocumentSourceResponse>> ExtractDocumentSources(List<Citation> citations, string requestId)
        {
            try
            {
                var sources = new List<DocumentSourceResponse>();

                foreach (var citation in citations.Take(10))
                {
                    try
                    {
                        var documentId = GetDocumentIdFromCitation(citation);
                        var versionId = GetVersionIdFromCitation(citation);

                        var source = new DocumentSourceResponse
                        {
                            DocumentId = !string.IsNullOrEmpty(documentId) ? documentId : versionId ?? Guid.NewGuid().ToString(),
                            RelevanceScore = citation.Partitions.Any() ? citation.Partitions.Max(p => p.Relevance) : 0,
                            Title = GetTagValueFromCitation(citation, "title"),
                            VersionName = GetTagValueFromCitation(citation, "versionName") ?? "1",
                            DepartmentId = GetTagValueFromCitation(citation, "departmentId"),
                            Summary = GetTagValueFromCitation(citation, "summary")
                        };

                        // ✅ If no title from tags, extract from content
                        if (string.IsNullOrEmpty(source.Title))
                        {
                            var firstPartition = citation.Partitions.FirstOrDefault();
                            if (firstPartition?.Text != null)
                            {
                                var lines = firstPartition.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                source.Title = lines.Length > 0 ? lines[0].Trim() : "Document";
                                if (source.Title.Length > 100)
                                {
                                    source.Title = source.Title.Substring(0, 97) + "...";
                                }
                            }
                            else
                            {
                                source.Title = "Document";
                            }
                        }

                        // ✅ Enhance with database info
                        try
                        {
                            DocumentVersion documentVersion = null;

                            if (!string.IsNullOrEmpty(versionId))
                            {
                                documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                                    .SingleOrDefaultAsync(
                                        predicate: dv => dv.Id == versionId,
                                        include: i => i.Include(dv => dv.DocumentFile));
                                                      //.Include(dv => dv.DocumentFile.Department));
                            }
                            else if (!string.IsNullOrEmpty(documentId))
                            {
                                documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                                    .SingleOrDefaultAsync(
                                        predicate: dv => dv.DocumentFile.Id == documentId,
                                        include: i => i.Include(dv => dv.DocumentFile));
                                                      //.Include(dv => dv.DocumentFile.Department));
                            }

                            if (documentVersion != null)
                            {
                                source.Title = documentVersion.Title ?? source.Title;
                                source.Summary = documentVersion.Summary ?? source.Summary;
                                source.VersionName = documentVersion.VersionName ?? source.VersionName;
                                source.EffectiveFrom = documentVersion.EffectiveFrom;
                                source.EffectiveUntil = documentVersion.EffectiveUntil;
                                source.DepartmentId = documentVersion.DocumentFile.DepartmentId ?? source.DepartmentId;
                            }
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogDebug("📋 [SOURCE-{RequestId}] Database enhancement failed: {Error}", requestId, dbEx.Message);
                        }

                        sources.Add(source);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "📋 [SOURCE-{RequestId}] Error processing citation", requestId);
                    }
                }

                var result = sources.OrderByDescending(s => s.RelevanceScore).ToList();
                _logger.LogInformation("📋 [SOURCE-{RequestId}] Extracted {Count} document sources", requestId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📋 [SOURCE-{RequestId}] Error extracting document sources", requestId);
                return new List<DocumentSourceResponse>();
            }
        }

        #endregion

        #region Raw Content Extraction

        private string ExtractRawContentFromCitations(List<Citation> citations)
        {
            if (!citations.Any())
            {
                return null;
            }

            var contentBuilder = new StringBuilder();
            var addedContent = new HashSet<string>();

            foreach (var citation in citations.Take(5))
            {
                try
                {
                    foreach (var partition in citation.Partitions.OrderByDescending(p => p.Relevance))
                    {
                        if (!string.IsNullOrWhiteSpace(partition.Text))
                        {
                            var cleanText = CleanRawText(partition.Text);

                            var contentHash = cleanText.GetHashCode().ToString();
                            if (!addedContent.Contains(contentHash) && cleanText.Length > 20)
                            {
                                if (contentBuilder.Length > 0)
                                {
                                    contentBuilder.AppendLine();
                                    contentBuilder.AppendLine("---");
                                    contentBuilder.AppendLine();
                                }

                                contentBuilder.Append(cleanText);
                                addedContent.Add(contentHash);
                                break;
                            }
                        }
                    }

                    if (contentBuilder.Length > 8000)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing citation for raw content");
                }
            }

            var result = contentBuilder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }

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

        #endregion

        #region Response Creation

        private DocumentRAGResponse CreateEmptyResponse(DocumentRAGRequest request, DateTime startTime, string reason = null)
        {
            return new DocumentRAGResponse
            {
                RequestId = request.RequestId ?? Guid.NewGuid().ToString(),
                Success = true,
                RawContent = null,
                Sources = new List<DocumentSourceResponse>(),
                QueryProcessed = request.Query,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                ErrorMessage = reason
            };
        }

        private DocumentRAGResponse CreateErrorResponse(DocumentRAGRequest request, Exception ex, string requestId)
        {
            return new DocumentRAGResponse
            {
                RequestId = requestId,
                Success = false,
                RawContent = null,
                Sources = new List<DocumentSourceResponse>(),
                QueryProcessed = request.Query,
                ErrorMessage = $"RAG processing error: {ex.Message}",
                ProcessingTimeMs = 0
            };
        }

        #endregion
    }
}