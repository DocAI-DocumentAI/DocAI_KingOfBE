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

        // ✅ OPTIMIZED CONFIGURATION
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
            _maxSearchResults = _configuration.GetValue<int>("RAG:MaxSearchResults", 10); // ✅ Reduced from 50
            _baseMinRelevanceScore = _configuration.GetValue<double>("RAG:BaseMinRelevanceScore", 0.001); // ✅ Lower threshold
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

                // ✅ SINGLE OPTIMIZED SEARCH
                var citations = await PerformOptimizedSearch(request, requestId);

                if (!citations.Any())
                {
                    _logger.LogInformation("❌ [RAG-{RequestId}] No citations found for query: '{Query}'", requestId, request.Query);
                    return CreateEmptyResponse(request, startTime, "No documents found");
                }

                _logger.LogInformation("📄 [RAG-{RequestId}] Found {Count} citations from KernelMemory", requestId, citations.Count);

                // ✅ SMART PERMISSION FILTERING WITH DATE CHECK
                var validCitations = await FilterCitationsSmartly(citations, request, requestId);

                _logger.LogInformation("🔒 [RAG-{RequestId}] Smart filtered: {Valid}/{Total} citations",
                    requestId, validCitations.Count, citations.Count);

                if (!validCitations.Any())
                {
                    return CreateEmptyResponse(request, startTime, "No accessible documents after filtering");
                }

                // ✅ EXTRACT OPTIMIZED CONTENT AND SOURCES
                var rawContent = ExtractOptimizedContent(validCitations, request.Query);
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
                Role = "ADMIN",
                MaxResults = 5,
                MinRelevanceScore = 0.001 // ✅ Very low threshold for general queries
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
                Role = "ADMIN",
                MaxResults = 5,
                MinRelevanceScore = 0.001
            };

            var response = await SearchDocumentsWithRAGAsync(request);

            return response.Success
                ? (response.RawContent, response.Sources)
                : (null, new List<DocumentSourceResponse>());
        }
        #endregion

        #region ✅ OPTIMIZED SEARCH - Single Pass with Smart Filtering

        private async Task<List<Citation>> PerformOptimizedSearch(DocumentRAGRequest request, string requestId)
        {
            try
            {
                // ✅ DETECT QUERY TYPE AND ADJUST PARAMETERS
                var queryType = ClassifyQuery(request.Query);
                var searchParams = GetOptimizedSearchParameters(queryType, request);

                _logger.LogInformation("🔎 [SEARCH-{RequestId}] Query type: {QueryType}, Strategy: {Strategy}",
                    requestId, queryType, searchParams.Strategy);

                // ✅ BUILD SMART DYNAMIC FILTER
                var filter = BuildDynamicFilter(request, queryType, requestId);

                // ✅ SINGLE SEARCH WITH OPTIMIZED PARAMETERS
                var searchQuery = searchParams.ExpandQuery ?
                    ExpandQueryIntelligently(request.Query, queryType) :
                    request.Query;

                _logger.LogInformation("🔎 [SEARCH-{RequestId}] Searching with: '{Query}', Limit: {Limit}, MinRelevance: {MinRelevance}",
                    requestId, searchQuery, searchParams.Limit, searchParams.MinRelevance);

                var result = await _memory.SearchAsync(
                    searchQuery,
                    limit: searchParams.Limit,
                    filter: filter,
                    minRelevance: searchParams.MinRelevance);

                var citations = result.Results.ToList();

                // ✅ IF GENERAL QUERY AND LOW RESULTS, TRY BROADER SEARCH
                if (queryType == QueryType.General && citations.Count < 3)
                {
                    _logger.LogInformation("🔎 [SEARCH-{RequestId}] General query has few results, trying broader search", requestId);

                    var broaderResult = await _memory.SearchAsync(
                        request.Query.Split(' ').FirstOrDefault() ?? request.Query, // Use first keyword
                        limit: 20,
                        filter: new MemoryFilter().ByTag("status", "approved"), // Minimal filter
                        minRelevance: 0.0);

                    citations.AddRange(broaderResult.Results);
                    citations = DeduplicateCitations(citations);
                }

                // ✅ RANK BY RELEVANCE AND CONTEXT
                citations = RankCitationsByRelevance(citations, request, queryType);

                _logger.LogInformation("✅ [SEARCH-{RequestId}] Found {Count} results after optimization",
                    requestId, citations.Count);

                return citations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SEARCH-{RequestId}] Error in optimized search", requestId);
                return new List<Citation>();
            }
        }

        // ✅ CLASSIFY QUERY TYPE FOR BETTER HANDLING
        private QueryType ClassifyQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return QueryType.Invalid;

            var queryLower = query.ToLower();
            var wordCount = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            // Very short queries are general
            if (wordCount <= 2) return QueryType.General;

            // Check for general patterns
            var generalPatterns = new[] {
                "quy định", "chính sách", "thủ tục", "hướng dẫn",
                "quy trình", "nội quy", "tài liệu", "văn bản",
                "có gì", "những gì", "tất cả", "toàn bộ"
            };

            if (generalPatterns.Any(p => queryLower.Contains(p) && wordCount <= 4))
                return QueryType.General;

            // Check for question patterns
            var questionWords = new[] { "làm sao", "thế nào", "như thế nào", "khi nào", "bao giờ" };
            if (questionWords.Any(queryLower.StartsWith))
                return QueryType.Question;

            // Check for specific document references
            if (queryLower.Contains("số") || queryLower.Contains("quyết định") || queryLower.Contains("thông tư"))
                return QueryType.DocumentReference;

            return QueryType.Specific;
        }

        // ✅ GET OPTIMIZED SEARCH PARAMETERS BASED ON QUERY TYPE
        private (int Limit, double MinRelevance, bool ExpandQuery, string Strategy) GetOptimizedSearchParameters(
            QueryType queryType, DocumentRAGRequest request)
        {
            return queryType switch
            {
                QueryType.General => (30, 0.0, true, "Broad"),
                QueryType.Question => (15, 0.001, true, "Semantic"),
                QueryType.DocumentReference => (5, 0.01, false, "Exact"),
                QueryType.Specific => (10, 0.005, false, "Balanced"),
                _ => (request.MaxResults, request.MinRelevanceScore ?? 0.001, false, "Default")
            };
        }

        // ✅ BUILD DYNAMIC FILTER BASED ON CONTEXT
        private MemoryFilter BuildDynamicFilter(DocumentRAGRequest request, QueryType queryType, string requestId)
        {
            var filter = new MemoryFilter();

            if (!string.IsNullOrEmpty(request.DocumentId))
            {
                filter = filter.ByTag("documentId", request.DocumentId);
                _logger.LogInformation("🔎 [FILTER-{RequestId}] CONSTRAINED search to specific DocumentId: {DocumentId}",
                    requestId, request.DocumentId);
                return filter;
            }

            var role = request.Role?.ToUpper() ?? "NONE";

            // ✅ ALWAYS filter approved documents
            filter = filter.ByTag("status", "approved");

            // ❌ ADMIN - Should not search any documents
            if (role == "ADMIN")
            {
                // Add impossible filter to ensure no results
                filter = filter.ByTag("accessLevel", "SUPER_ADMIN_ONLY");
                _logger.LogDebug("🔎 [FILTER-{RequestId}] Admin filter applied - no documents accessible", requestId);
                return filter;
            }

            // ✅ FOR NONE role or OnlyPublic request - Only public documents
            if (role == "NONE" || request.OnlyPublic)
            {
                filter = filter.ByTag("isPublic", "True");
                _logger.LogDebug("🔎 [FILTER-{RequestId}] Public only filter applied for role: {Role}", requestId, role);
            }

            // ✅ FOR DOCUMENT REFERENCES - Add document type if detected
            if (queryType == QueryType.DocumentReference)
            {
                var docType = ExtractDocumentType(request.Query);
                if (!string.IsNullOrEmpty(docType))
                {
                    filter = filter.ByTag("documentType", docType);
                    _logger.LogDebug("🔎 [FILTER-{RequestId}] Document type filter: {DocType}", requestId, docType);
                }
            }

            // ✅ NOTE: Department filtering will be handled in post-processing
            // because MemoryFilter doesn't support OR conditions effectively

            _logger.LogInformation("🔎 [FILTER-{RequestId}] Built filter for {Role}, QueryType: {QueryType}",
                requestId, role, queryType);

            return filter;
        }

        // ✅ INTELLIGENT QUERY EXPANSION
        private string ExpandQueryIntelligently(string originalQuery, QueryType queryType)
        {
            var queryLower = originalQuery.ToLower();

            // Expand based on query type
            return queryType switch
            {
                QueryType.General when queryLower.Contains("nghỉ phép") =>
                    "nghỉ phép annual leave vacation holiday phép năm",

                QueryType.General when queryLower.Contains("lương") =>
                    "lương salary thưởng bonus thu nhập income",

                QueryType.Question when queryLower.Contains("làm sao") =>
                    originalQuery.Replace("làm sao", "cách thức quy trình thủ tục"),

                _ => originalQuery
            };
        }

        #endregion

        #region ✅ SMART FILTERING WITH PARALLEL PROCESSING

        private async Task<List<Citation>> FilterCitationsSmartly(
            List<Citation> citations,
            DocumentRAGRequest request,
            string requestId)
        {
            var today = DateTime.UtcNow.Date;

            // ✅ PARALLEL FILTERING FOR PERFORMANCE
            var filterTasks = citations.Select(async citation =>
            {
                // Check date effectiveness
                if (!IsDocumentCurrentlyEffective(citation, today, requestId))
                    return (citation, false, 0.0);

                // Check access permissions
                var hasAccess = await IsDocumentAccessibleToUser(citation, request, requestId);
                if (!hasAccess)
                    return (citation, false, 0.0);

                // Calculate relevance boost
                var relevanceScore = CalculateEnhancedRelevance(citation, request);

                return (citation, true, relevanceScore);
            });

            var results = await Task.WhenAll(filterTasks);

            // ✅ RETURN TOP RESULTS SORTED BY ENHANCED RELEVANCE
            return results
                .Where(r => r.Item2) // Has access
                .OrderByDescending(r => r.Item3) // By relevance
                .Take(request.MaxResults)
                .Select(r => r.citation)
                .ToList();
        }

        // ✅ CALCULATE ENHANCED RELEVANCE WITH MULTIPLE FACTORS
        private double CalculateEnhancedRelevance(Citation citation, DocumentRAGRequest request)
        {
            var baseRelevance = citation.Partitions.Any() ?
                citation.Partitions.Max(p => p.Relevance) : 0.0;

            var boost = 1.0;

            // Boost same department
            var deptId = GetTagValueFromCitation(citation, "departmentId");
            if (deptId == request.DepartmentId)
                boost *= 1.2;

            // Boost recent documents
            var approvalDateStr = GetTagValueFromCitation(citation, "approvalDate");
            if (DateTime.TryParse(approvalDateStr, out var approvalDate))
            {
                var daysSinceApproval = (DateTime.UtcNow - approvalDate).TotalDays;
                if (daysSinceApproval < 30) boost *= 1.3;
                else if (daysSinceApproval < 90) boost *= 1.1;
            }

            // Boost official documents
            var isOfficial = GetTagValueFromCitation(citation, "isOfficial");
            if (isOfficial?.ToLower() == "true")
                boost *= 1.15;

            return baseRelevance * boost;
        }

        #endregion

        #region ✅ OPTIMIZED CONTENT EXTRACTION

        private string ExtractOptimizedContent(List<Citation> citations, string query)
        {
            if (!citations.Any()) return null;

            var contentBuilder = new StringBuilder();
            var processedContent = new HashSet<int>(); // Use hash for deduplication
            var maxContentLength = 8000;
            var currentLength = 0;

            // ✅ Get multiple partitions per document (up to 3 best partitions per document)
            var allPartitions = citations
                .SelectMany(c => {
                    var title = GetTagValueFromCitation(c, "title") ?? GetTagValueFromCitation(c, "documentTitle") ?? "Document";

                    // ✅ Take top 3 partitions from each document (instead of just 1)
                    return c.Partitions
                        .Where(p => !string.IsNullOrWhiteSpace(p.Text) && p.Text.Length > 50 && p.Relevance > 0.01)
                        .OrderByDescending(p => p.Relevance)
                        .Take(3) // ✅ Up to 3 partitions per document
                        .Select(p => new {
                            Citation = c,
                            Partition = p,
                            Relevance = p.Relevance,
                            Title = title
                        });
                })
                .OrderByDescending(x => x.Relevance) // ✅ Then sort all partitions by relevance
                .ToList();

            _logger.LogInformation("🔍 [CONTENT] Processing {Count} partitions from {DocumentCount} documents, highest relevance: {MaxRelevance:F3}, query: '{QueryPreview}'",
                allPartitions.Count,
                citations.Count,
                allPartitions.FirstOrDefault()?.Relevance ?? 0,
                query.Length > 50 ? query.Substring(0, 50) + "..." : query);

            foreach (var item in allPartitions)
            {
                if (currentLength >= maxContentLength) break;

                var text = item.Partition.Text;

                // ✅ Take content as-is if short, or extract relevant snippet if long
                var snippet = text.Length <= 800 ? text : ExtractRelevantSnippet(text, query, 600);

                // Check for duplicate using better content matching
                var contentKey = snippet.Length < 100 ? snippet : snippet.Substring(0, 100);
                var contentHash = contentKey.Trim().GetHashCode();
                if (processedContent.Contains(contentHash))
                {
                    _logger.LogDebug("🔍 [CONTENT] Skipping duplicate content from: {Title}", item.Title);
                    continue;
                }
                processedContent.Add(contentHash);

                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine("\n---\n");
                }

                // Add title header
                contentBuilder.AppendLine($"📄 **{item.Title}**:");
                contentBuilder.AppendLine();
                contentBuilder.AppendLine(snippet);

                currentLength += snippet.Length;

                _logger.LogDebug("🔍 [CONTENT] Added content (Relevance: {Relevance:F3}, Length: {Length}) from: {Title}",
                    item.Relevance, snippet.Length, item.Title);
            }

            var result = contentBuilder.ToString().Trim();

            if (string.IsNullOrEmpty(result))
            {
                _logger.LogWarning("🔍 [CONTENT] No content extracted for query: '{Query}'", query);
                return null;
            }

            _logger.LogInformation("✅ [CONTENT] Final content: {Length} chars from {Count} partitions",
                result.Length, allPartitions.Count);

            return result;
        }

        // ✅ EXTRACT MOST RELEVANT SNIPPET FROM TEXT
        private string ExtractRelevantSnippet(string text, string query, int maxLength)
        {
            if (text.Length <= maxLength) return text;

            // Try to find query terms in text
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var textLower = text.ToLower();

            int bestStart = 0;
            int bestScore = 0;

            // Find position with most query word matches
            for (int i = 0; i < text.Length - maxLength; i += 50)
            {
                var segment = textLower.Substring(i, Math.Min(maxLength, text.Length - i));
                var score = queryWords.Count(word => segment.Contains(word));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestStart = i;
                }
            }

            // Extract snippet from best position
            var snippet = text.Substring(bestStart, Math.Min(maxLength, text.Length - bestStart));

            // Clean up edges
            if (bestStart > 0)
            {
                var firstSpace = snippet.IndexOf(' ');
                if (firstSpace > 0 && firstSpace < 50)
                    snippet = "..." + snippet.Substring(firstSpace);
            }

            if (bestStart + maxLength < text.Length)
            {
                var lastSpace = snippet.LastIndexOf(' ');
                if (lastSpace > snippet.Length - 50)
                    snippet = snippet.Substring(0, lastSpace) + "...";
            }

            return snippet.Trim();
        }

        #endregion

        #region Helper Methods (Optimized)

        // ✅ DEDUPLICATE CITATIONS
        private List<Citation> DeduplicateCitations(List<Citation> citations)
        {
            var seen = new HashSet<string>();
            var deduplicated = new List<Citation>();

            foreach (var citation in citations)
            {
                var docId = GetDocumentIdFromCitation(citation);
                var versionId = GetVersionIdFromCitation(citation);
                var key = $"{docId}_{versionId}";

                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    deduplicated.Add(citation);
                }
            }

            return deduplicated;
        }

        // ✅ RANK CITATIONS BY MULTIPLE FACTORS
        private List<Citation> RankCitationsByRelevance(
            List<Citation> citations,
            DocumentRAGRequest request,
            QueryType queryType)
        {
            return citations
                .Select(c => new {
                    Citation = c,
                    Score = CalculateEnhancedRelevance(c, request)
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Citation)
                .ToList();
        }

        // ✅ EXTRACT DOCUMENT TYPE FROM QUERY
        private string ExtractDocumentType(string query)
        {
            var queryLower = query.ToLower();

            if (queryLower.Contains("quyết định")) return "QD";
            if (queryLower.Contains("thông tư")) return "TT";
            if (queryLower.Contains("nghị định")) return "ND";
            if (queryLower.Contains("quy chế")) return "QC";
            if (queryLower.Contains("quy định")) return "QD";

            return null;
        }

        // ✅ QUERY TYPE ENUM
        private enum QueryType
        {
            General,
            Specific,
            Question,
            DocumentReference,
            Invalid
        }

        #endregion

        #region Existing Helper Methods (Keep unchanged)

        private bool IsDocumentCurrentlyEffective(Citation citation, DateTime today, string requestId)
        {
            try
            {
                var effectiveFromStr = GetTagValueFromCitation(citation, "effectiveFrom");
                var effectiveUntilStr = GetTagValueFromCitation(citation, "effectiveUntil");

                // ✅ CHECK EFFECTIVE FROM - document có hiệu lực từ ngày (including today)
                if (!string.IsNullOrEmpty(effectiveFromStr))
                {
                    if (DateTime.TryParse(effectiveFromStr, out var effectiveFrom))
                    {
                        // Document phải có hiệu lực từ hôm nay hoặc trước đó
                        if (today < effectiveFrom.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document not yet effective: EffectiveFrom {EffFrom} > Today {Today}",
                                requestId, effectiveFrom.Date, today);
                            return false;
                        }
                    }
                }

                // ✅ CHECK EFFECTIVE UNTIL - document có hiệu lực đến ngày (including today)
                if (!string.IsNullOrEmpty(effectiveUntilStr))
                {
                    if (DateTime.TryParse(effectiveUntilStr, out var effectiveUntil))
                    {
                        // Document phải còn hiệu lực đến hôm nay hoặc sau đó
                        if (today > effectiveUntil.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document expired: EffectiveUntil {EffUntil} < Today {Today}",
                                requestId, effectiveUntil.Date, today);
                            return false;
                        }
                    }
                }

                // ✅ Document is currently effective (including documents uploaded today)
                _logger.LogDebug("✅ [EFFECTIVE-{RequestId}] Document is effective today", requestId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⏰ [EFFECTIVE-{RequestId}] Error checking effectiveness - denying access", requestId);
                return false; // ✅ Deny by default for security
            }
        }

        private async Task<bool> IsDocumentAccessibleToUser(Citation citation, DocumentRAGRequest userContext, string requestId)
        {
            try
            {
                var role = userContext.Role?.ToUpper() ?? "NONE";

                // ❌ ADMIN - NO ACCESS TO DOCUMENTS (per business requirement)
                if (role == "ADMIN")
                {
                    _logger.LogDebug("🔒 [ACCESS-{RequestId}] Admin access DENIED - Admins cannot search documents", requestId);
                    return false;
                }

                // ✅ GET DOCUMENT METADATA
                var departmentId = GetTagValueFromCitation(citation, "departmentId");
                var ownerId = GetTagValueFromCitation(citation, "ownerId");
                var isPublicStr = GetTagValueFromCitation(citation, "isPublic");
                bool.TryParse(isPublicStr, out bool isPublic);

                // ✅ OWNER ACCESS - Document owner always has access (except Admin)
                if (!string.IsNullOrEmpty(ownerId) && ownerId == userContext.UserId && role != "ADMIN")
                {
                    _logger.LogDebug("🔓 [ACCESS-{RequestId}] Owner access granted", requestId);
                    return true;
                }

                // ✅ PUBLIC DOCUMENTS - Everyone except Admin can access
                if (isPublic && role != "ADMIN")
                {
                    _logger.LogDebug("🔓 [ACCESS-{RequestId}] Public document access granted", requestId);
                    return true;
                }

                // ✅ DEPARTMENT ACCESS - Users can access their department's documents
                if (!string.IsNullOrEmpty(userContext.DepartmentId))
                {
                    // User có department có thể xem tài liệu của department mình
                    if (departmentId == userContext.DepartmentId)
                    {
                        // Check role permissions within department
                        switch (role)
                        {
                            case "MANAGER":
                                _logger.LogDebug("🔓 [ACCESS-{RequestId}] Manager access granted for dept: {DeptId}",
                                    requestId, departmentId);
                                return true;

                            case "EDITOR":
                                _logger.LogDebug("🔓 [ACCESS-{RequestId}] Editor access granted for dept: {DeptId}",
                                    requestId, departmentId);
                                return true;

                            case "MEMBER":
                                _logger.LogDebug("🔓 [ACCESS-{RequestId}] Member access granted for dept: {DeptId}",
                                    requestId, departmentId);
                                return true;

                            case "NONE":
                                _logger.LogDebug("🔒 [ACCESS-{RequestId}] Role NONE - no department access", requestId);
                                return false;

                            case "ADMIN":
                                _logger.LogDebug("🔒 [ACCESS-{RequestId}] Admin role - no access allowed", requestId);
                                return false;

                            default:
                                _logger.LogDebug("🔒 [ACCESS-{RequestId}] Unknown role {Role} - access denied",
                                    requestId, role);
                                return false;
                        }
                    }
                }

                // ✅ SPECIAL PERMISSIONS (but not for Admin)
                if (role != "ADMIN" && userContext.Permissions?.Any(p => new[] {
                    "VIEW_ANY_DOCUMENT",
                    "VIEW_DEPARTMENT_DOCUMENT"
                }.Contains(p)) == true)
                {
                    _logger.LogDebug("🔓 [ACCESS-{RequestId}] Special permission access granted", requestId);
                    return true;
                }

                // ✅ NONE role or NO DEPARTMENT - No access to any documents
                if (role == "NONE" || string.IsNullOrEmpty(userContext.DepartmentId))
                {
                    _logger.LogDebug("🔒 [ACCESS-{RequestId}] No role/No department - access denied", requestId);
                    return false;
                }

                _logger.LogDebug("🔒 [ACCESS-{RequestId}] Access denied - no matching criteria", requestId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔒 [ACCESS-{RequestId}] Error checking access - denying by default", requestId);
                return false; // ✅ Deny by default for security
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
                var possibleTags = new[] { "versionId", "version_id", "__version_id"};
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

    private async Task<List<DocumentSourceResponse>> ExtractDocumentSources(List<Citation> citations, string requestId)
        {
            try
            {
                var sources = new List<DocumentSourceResponse>();

                foreach (var citation in citations.Take(10))
                {
                    try
                    {
                        // Extract IDs from citation
                        var documentId = GetDocumentIdFromCitation(citation);
                        var versionId = GetVersionIdFromCitation(citation);

                        if (string.IsNullOrEmpty(versionId) && !string.IsNullOrEmpty(documentId) && Guid.TryParse(documentId, out _))
                        {
                            versionId = documentId;
                        }

                        var source = new DocumentSourceResponse
                        {
                            DocumentId = documentId ?? versionId ?? Guid.NewGuid().ToString(),
                            RelevanceScore = citation.Partitions.Any() ? citation.Partitions.Max(p => p.Relevance) : 0,
                            DepartmentId = GetTagValueFromCitation(citation, "departmentId"),
                            VersionName = GetTagValueFromCitation(citation, "version") ?? GetTagValueFromCitation(citation, "versionName") ?? "1"
                        };

                        // ✅ NEW: Get title from tags FIRST (no database calls)
                        source.Title = GetTagValueFromCitation(citation, "title");
                        if (string.IsNullOrWhiteSpace(source.Title))
                            source.Title = GetTagValueFromCitation(citation, "documentTitle");
                        if (string.IsNullOrWhiteSpace(source.Title))
                            source.Title = GetTagValueFromCitation(citation, "name");
                        if (string.IsNullOrWhiteSpace(source.Title))
                        {
                            var firstPartition = citation.Partitions.FirstOrDefault();
                            source.Title = firstPartition?.Text != null ?
                                          ExtractTitleFromContent(firstPartition.Text) : "Document";
                        }

                        // Get other metadata from tags
                        source.Summary = GetTagValueFromCitation(citation, "summary") ?? "";
                        source.FileType = GetTagValueFromCitation(citation, "fileType") ?? "";

                        // Parse dates from tags
                        if (DateTime.TryParse(GetTagValueFromCitation(citation, "effectiveFrom"), out var effectiveFrom))
                            source.EffectiveFrom = effectiveFrom;

                        if (DateTime.TryParse(GetTagValueFromCitation(citation, "effectiveUntil"), out var effectiveUntil))
                            source.EffectiveUntil = effectiveUntil;

                        sources.Add(source);

                        _logger.LogDebug("📋 [SOURCE-{RequestId}] Got title from tags: '{Title}' for doc: {DocId}",
                            requestId, source.Title, source.DocumentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "📋 [SOURCE-{RequestId}] Error processing citation for doc",
                            requestId);

                        // ✅ Add minimal source on error to avoid losing data
                        sources.Add(new DocumentSourceResponse
                        {
                            DocumentId = Guid.NewGuid().ToString(),
                            Title = "Error Loading Document",
                            VersionName = "1.0",
                            RelevanceScore = 0,
                            DepartmentId = "",
                            Summary = "",
                            FileType = ""
                        });
                    }
                }

                var result = sources.OrderByDescending(s => s.RelevanceScore).ToList();
                _logger.LogInformation("📋 [SOURCE-{RequestId}] Extracted {Count} sources from tags (no DB calls)", requestId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📋 [SOURCE-{RequestId}] Error extracting sources", requestId);
                return new List<DocumentSourceResponse>();
            }
        }

        // ✅ NEW METHOD: Smart title extraction from content
        private string ExtractTitleFromContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "Document";

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Look for potential title patterns
            foreach (var line in lines.Take(5)) // Check first 5 lines
            {
                var trimmedLine = line.Trim();

                // Skip very short lines
                if (trimmedLine.Length < 10) continue;

                // Skip lines that look like metadata or form fields
                if (trimmedLine.Contains("...") ||
                    trimmedLine.Contains("___") ||
                    trimmedLine.StartsWith("(") ||
                    trimmedLine.EndsWith(":"))
                    continue;

                // Look for lines that might be titles (capitalized, no punctuation at end except "?")
                if (trimmedLine.Length > 10 && trimmedLine.Length < 200)
                {
                    // Check if it looks like a title (has some uppercase, not all lowercase)
                    bool hasUpperCase = trimmedLine.Any(char.IsUpper);
                    bool notAllLower = trimmedLine != trimmedLine.ToLower();

                    if (hasUpperCase && notAllLower)
                    {
                        // This might be a title, clean it up
                        var title = trimmedLine;

                        // Truncate if too long
                        if (title.Length > 100)
                        {
                            title = title.Substring(0, 97) + "...";
                        }

                        return title;
                    }
                }
            }

            // Fallback: Use first meaningful line
            var firstMeaningfulLine = lines.FirstOrDefault(l =>
                l.Trim().Length > 10 &&
                !l.Contains("...") &&
                !l.Contains("___"));

            if (!string.IsNullOrEmpty(firstMeaningfulLine))
            {
                var title = firstMeaningfulLine.Trim();
                if (title.Length > 100)
                {
                    title = title.Substring(0, 97) + "...";
                }
                return title;
            }

            // Last resort: Use beginning of content
            var fallbackTitle = content.Length > 100
                ? content.Substring(0, 97) + "..."
                : content;

            // Clean up whitespace
            fallbackTitle = System.Text.RegularExpressions.Regex.Replace(fallbackTitle, @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(fallbackTitle) ? "Document" : fallbackTitle;
        }

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
