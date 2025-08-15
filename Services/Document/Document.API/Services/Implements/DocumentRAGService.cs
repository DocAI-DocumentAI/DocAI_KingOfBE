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
            _maxSearchResults = _configuration.GetValue<int>("RAG:MaxSearchResults", 10);
            _baseMinRelevanceScore = _configuration.GetValue<double>("RAG:BaseMinRelevanceScore", 0.001);
        }
        #endregion

        #region Main Public Methods
        public async Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request)
        {
            var requestId = request.RequestId ?? Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("🔍 [RAG-{RequestId}] Starting search - DocumentId: {DocId}, Query: '{Query}', User: {FullName} ({Role}), Dept: {DeptName}",
                    requestId, request.DocumentId ?? "None", request.Query, request.FullName, request.Role, request.DepartmentName);

                // ✅ VALIDATE REQUEST
                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    return CreateEmptyResponse(request, startTime, "Empty query provided");
                }

                // ✅ PERFORM SEARCH
                var citations = await PerformOptimizedSearch(request, requestId);

                if (!citations.Any())
                {
                    _logger.LogInformation("❌ [RAG-{RequestId}] No citations found for query: '{Query}'", requestId, request.Query);
                    return CreateEmptyResponse(request, startTime, "No documents found");
                }

                _logger.LogInformation("📄 [RAG-{RequestId}] Found {Count} citations from KernelMemory", requestId, citations.Count);

                // ✅ CRITICAL: PERMISSION FILTERING WITH COMPLETE BLOCKING
                var validCitations = await FilterCitationsWithCompleteBlocking(citations, request, requestId);

                _logger.LogInformation("🔒 [RAG-{RequestId}] After permission filter: {Valid}/{Total} citations",
                    requestId, validCitations.Count, citations.Count);

                if (!validCitations.Any())
                {
                    return CreateEmptyResponse(request, startTime, "No accessible documents found");
                }

                // ✅ EXTRACT CONTENT AND SOURCES WITH COMPLETE METADATA
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
                MinRelevanceScore = 0.001
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

        #region ✅ OPTIMIZED SEARCH - Complete Implementation

        private async Task<List<Citation>> PerformOptimizedSearch(DocumentRAGRequest request, string requestId)
        {
            try
            {
                _logger.LogInformation("🔍 [SEARCH-{RequestId}] Starting - DocumentId: {DocId}, Query: '{Query}', User: {Role}",
                    requestId, request.DocumentId ?? "None", request.Query, request.Role);

                // ✅ CASE 1: SPECIFIC DOCUMENT - Load toàn bộ document
                if (!string.IsNullOrEmpty(request.DocumentId))
                {
                    return await SearchSpecificDocument(request, requestId);
                }

                // ✅ CASE 2: GENERAL SEARCH - Tìm kiếm thông minh
                return await SearchAllDocuments(request, requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SEARCH-{RequestId}] Error in search", requestId);
                return new List<Citation>();
            }
        }

private async Task<List<Citation>> SearchSpecificDocument(DocumentRAGRequest request, string requestId)
        {
            _logger.LogInformation("🎯 [SPECIFIC-{RequestId}] Loading ONLY document: {DocId}", requestId, request.DocumentId);

            var citations = new List<Citation>();

            // ✅ STRATEGY 1: Primary search by documentId tag
            try
            {
                var primaryResult = await _memory.SearchAsync(
                    string.IsNullOrEmpty(request.Query) ? "*" : request.Query,
                    limit: 300, // Load nhiều partitions của document này
                    filter: new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("documentId", request.DocumentId),
                    minRelevance: 0.0 // Load tất cả content của document này
                );

                citations = primaryResult.Results.ToList();
                    _logger.LogInformation("🎯 [DEBUG-SEARCH] Found {Count} raw citations", citations.Count);

                if (citations.Any())
                {
                    _logger.LogInformation("✅ [SPECIFIC-{RequestId}] Found {Count} partitions using documentId tag",
                        requestId, citations.Count);

                    // ✅ VERIFY tất cả citations đều thuộc về cùng 1 document
                    var uniqueDocIds = citations.Select(c => GetDocumentIdFromCitation(c)).Distinct().ToList();
                    if (uniqueDocIds.Count > 1)
                    {
                        _logger.LogWarning("⚠️ [SPECIFIC-{RequestId}] Multiple documents found ({DocCount}), filtering to target document only",
                            requestId, uniqueDocIds.Count);

                        // ✅ FILTER chỉ giữ lại citations của document target
                        citations = citations.Where(c => GetDocumentIdFromCitation(c) == request.DocumentId).ToList();
                    }

                    return citations;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [SPECIFIC-{RequestId}] Primary search failed, trying alternatives", requestId);
            }

            // ✅ STRATEGY 2: Alternative field searches - nhưng vẫn verify document match
            var alternativeFields = new[] { "__document_id", "docId", "document_id", "versionId" };

            foreach (var field in alternativeFields)
            {
                try
                {
                    _logger.LogDebug("🔍 [SPECIFIC-{RequestId}] Trying alternative field: {Field}", requestId, field);

                    var altResult = await _memory.SearchAsync(
                        string.IsNullOrEmpty(request.Query) ? "*" : request.Query,
                        limit: 300,
                        filter: new MemoryFilter()
                            .ByTag("status", "approved")
                            .ByTag(field, request.DocumentId),
                        minRelevance: 0.0
                    );

                    if (altResult.Results.Any())
                    {
                        var altCitations = altResult.Results.ToList();

                        // ✅ STRICT VERIFICATION: Chỉ giữ citations của document target
                        var filteredCitations = altCitations.Where(c => IsFromTargetDocument(c, request.DocumentId)).ToList();

                        if (filteredCitations.Any())
                        {
                            _logger.LogInformation("✅ [SPECIFIC-{RequestId}] Found {Count} partitions using field: {Field}",
                                requestId, filteredCitations.Count, field);
                            return filteredCitations;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [SPECIFIC-{RequestId}] Alternative field {Field} search failed", requestId, field);
                }
            }

            // ✅ STRATEGY 3: Last resort - exact title match but verify document
            //try
            //{
            //    _logger.LogDebug("🔍 [SPECIFIC-{RequestId}] Trying title-based search as last resort", requestId);

            //    var titleResult = await _memory.SearchAsync(
            //        request.DocumentId, // Use documentId as search term
            //        limit: 100,
            //        filter: new MemoryFilter().ByTag("status", "approved"),
            //        minRelevance: 0.1
            //    );

            //    if (titleResult.Results.Any())
            //    {
            //        var titleCitations = titleResult.Results
            //            .Where(c => IsFromTargetDocument(c, request.DocumentId))
            //            .ToList();

            //        if (titleCitations.Any())
            //        {
            //            _logger.LogInformation("✅ [SPECIFIC-{RequestId}] Found {Count} partitions using title search",
            //                requestId, titleCitations.Count);
            //            return titleCitations;
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogWarning(ex, "⚠️ [SPECIFIC-{RequestId}] Title search failed", requestId);
            //}

            _logger.LogWarning("❌ [SPECIFIC-{RequestId}] No partitions found for document: {DocId}", requestId, request.DocumentId);
            return new List<Citation>();
        }
        /// <summary>
        /// ✅ STRICT VERIFICATION: Check if citation belongs to target document
        /// </summary>
        private bool IsFromTargetDocument(Citation citation, string targetDocumentId)
        {
            if (citation?.Partitions == null || !citation.Partitions.Any())
                return false;

            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags == null)
                return false;

            // ✅ Check all possible document ID fields
            var possibleDocIdFields = new[] { "documentId", "__document_id", "docId", "document_id", "versionId" };

            foreach (var field in possibleDocIdFields)
            {
                if (firstPartition.Tags.TryGetValue(field, out var values) && values?.Any() == true)
                {
                    var documentId = values.FirstOrDefault();
                    if (!string.IsNullOrEmpty(documentId) && documentId == targetDocumentId)
                    {
                        return true;
                    }
                }
            }

            // ✅ Also check title match as fallback
            if (firstPartition.Tags.TryGetValue("title", out var titleValues) && titleValues?.Any() == true)
            {
                var title = titleValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(title) && title.Contains(targetDocumentId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // ✅ ENHANCED GetDocumentIdFromCitation với better fallback
        private string GetDocumentIdFromCitation(Citation citation)
        {
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags != null)
            {
                var possibleTags = new[] { "documentId", "__document_id", "docId", "document_id", "versionId" };
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

        // ✅ ENHANCED FilterCitationsWithCompleteBlocking để đảm bảo single document
        private async Task<List<Citation>> FilterCitationsWithCompleteBlocking(
            List<Citation> citations,
            DocumentRAGRequest request,
            string requestId)
        {
            var today = DateTime.UtcNow.Date;
            var accessibleCitations = new List<Citation>();

            // ✅ SPECIAL HANDLING for specific document requests
            if (!string.IsNullOrEmpty(request.DocumentId))
            {
                _logger.LogInformation("🔒 [PERMISSION-{RequestId}] Specific document mode: filtering for DocumentId: {DocId}",
                    requestId, request.DocumentId);

                // ✅ FIRST: Filter only citations from target document
                var targetDocCitations = citations.Where(c => IsFromTargetDocument(c, request.DocumentId)).ToList();

                if (!targetDocCitations.Any())
                {
                    _logger.LogWarning("❌ [PERMISSION-{RequestId}] No citations found for target document: {DocId}",
                        requestId, request.DocumentId);
                    return new List<Citation>();
                }

                citations = targetDocCitations;
                _logger.LogInformation("🎯 [PERMISSION-{RequestId}] Filtered to {Count} citations from target document",
                    requestId, citations.Count);
            }

            // ✅ THEN: Apply permission filtering (existing logic)
            var citationsByDoc = citations
                .GroupBy(c => GetDocumentIdFromCitation(c))
                .ToList();

            _logger.LogInformation("🔒 [PERMISSION-{RequestId}] Checking {DocCount} documents for user {Role}",
                requestId, citationsByDoc.Count, request.Role);

            foreach (var docGroup in citationsByDoc)
            {
                var docId = docGroup.Key;
                var firstCitation = docGroup.First();

                try
                {
                    // ✅ Check document-level permissions
                    var hasAccess = await IsDocumentAccessibleToUser(firstCitation, request, requestId);
                    var isEffective = IsDocumentCurrentlyEffective(firstCitation, today, requestId);

                    if (hasAccess && isEffective)
                    {
                        accessibleCitations.AddRange(docGroup);
                        _logger.LogDebug("✅ [PERMISSION-{RequestId}] Document {DocId} ACCESSIBLE - added {Count} citations",
                            requestId, docId, docGroup.Count());
                    }
                    else
                    {
                        _logger.LogDebug("❌ [PERMISSION-{RequestId}] Document {DocId} BLOCKED - HasAccess: {Access}, IsEffective: {Effective}",
                            requestId, docId, hasAccess, isEffective);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [PERMISSION-{RequestId}] Error checking document {DocId} - BLOCKING by default",
                        requestId, docId);
                }
            }

            // ✅ Sort accessible citations by relevance
            var sortedResults = accessibleCitations
                .OrderByDescending(c => CalculateEnhancedRelevance(c, request))
                .ToList(); // No limit for specific document - load all content

            _logger.LogInformation("🔒 [PERMISSION-{RequestId}] Final result: {Accessible} citations from {Total} documents",
                requestId, sortedResults.Count, citationsByDoc.Count);

            return sortedResults;
        }
        // ✅ SEARCH ALL DOCUMENTS - Tìm kiếm thông minh
        private async Task<List<Citation>> SearchAllDocuments(DocumentRAGRequest request, string requestId)
        {
            _logger.LogInformation("🌐 [GENERAL-{RequestId}] Searching with department pre-filtering", requestId);

            var queryType = ClassifyQuerySmart(request.Query);
            var (limit, minRelevance) = GetSearchParams(queryType);

            _logger.LogInformation("🔎 [GENERAL-{RequestId}] QueryType: {Type}, Limit: {Limit}, MinRelevance: {MinRel}",
                requestId, queryType, limit, minRelevance);

            var citations = new List<Citation>();

            // ✅ STRATEGY 1: Search department documents first
            if (!string.IsNullOrEmpty(request.DepartmentId) && request.Role?.ToUpper() != "ADMIN")
            {
                try
                {
                    var departmentFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("departmentId", request.DepartmentId);

                    var deptResult = await _memory.SearchAsync(
                        request.Query,
                        limit: limit,
                        filter: departmentFilter,
                        minRelevance: minRelevance);

                    citations.AddRange(deptResult.Results);

                    _logger.LogInformation("🏢 [GENERAL-{RequestId}] Found {Count} citations from department: {DeptId}",
                        requestId, deptResult.Results.Count(), request.DepartmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [GENERAL-{RequestId}] Department search failed", requestId);
                }
            }

            // ✅ STRATEGY 2: Search public documents if needed (to fill up results)
            if (citations.Count < limit && request.Role?.ToUpper() != "ADMIN")
            {
                try
                {
                    var publicFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("isPublic", "True");

                    var publicResult = await _memory.SearchAsync(
                        request.Query,
                        limit: limit - citations.Count,
                        filter: publicFilter,
                        minRelevance: minRelevance);

                    // ✅ Avoid duplicates
                    var newPublicCitations = publicResult.Results
                        .Where(c => !citations.Any(existing =>
                            GetDocumentIdFromCitation(existing) == GetDocumentIdFromCitation(c)))
                        .ToList();

                    citations.AddRange(newPublicCitations);

                    _logger.LogInformation("🌍 [GENERAL-{RequestId}] Added {Count} public citations (total: {Total})",
                        requestId, newPublicCitations.Count, citations.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [GENERAL-{RequestId}] Public search failed", requestId);
                }
            }

            // ✅ STRATEGY 3: Owner documents (if user is document owner)
            if (citations.Count < limit && !string.IsNullOrEmpty(request.UserId) && request.Role?.ToUpper() != "ADMIN")
            {
                try
                {
                    var ownerFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("ownerId", request.UserId);

                    var ownerResult = await _memory.SearchAsync(
                        request.Query,
                        limit: limit - citations.Count,
                        filter: ownerFilter,
                        minRelevance: minRelevance);

                    var newOwnerCitations = ownerResult.Results
                        .Where(c => !citations.Any(existing =>
                            GetDocumentIdFromCitation(existing) == GetDocumentIdFromCitation(c)))
                        .ToList();

                    citations.AddRange(newOwnerCitations);

                    _logger.LogInformation("👤 [GENERAL-{RequestId}] Added {Count} owner citations (total: {Total})",
                        requestId, newOwnerCitations.Count, citations.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [GENERAL-{RequestId}] Owner search failed", requestId);
                }
            }

            _logger.LogInformation("📊 [GENERAL-{RequestId}] Total found: {Count} citations", requestId, citations.Count);
            return citations;
        }

        // ✅ CLASSIFY QUERY - Đơn giản và hiệu quả
        private string ClassifyQuerySmart(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return "general";

            var queryLower = query.ToLower();

            // Full document requests
            if (queryLower.Contains("tóm tắt") || queryLower.Contains("toàn bộ") ||
                queryLower.Contains("phân tích") || queryLower.Contains("đánh giá"))
                return "full_document";

            // Specific section requests
            if (queryLower.Contains("điều") || queryLower.Contains("khoản") ||
                queryLower.Contains("mục") || queryLower.Contains("phần"))
                return "specific_section";

            // Questions
            if (queryLower.Contains("làm sao") || queryLower.Contains("thế nào") ||
                queryLower.Contains("khi nào") || queryLower.Contains("có phải"))
                return "question";

            return "general";
        }

        // ✅ GET SEARCH PARAMS - Dựa trên query type
        private (int limit, double minRelevance) GetSearchParams(string queryType)
        {
            return queryType switch
            {
                "full_document" => (100, 0.0),     // Load nhiều để có context đầy đủ
                "specific_section" => (50, 0.01),  // Tìm section cụ thể
                "question" => (75, 0.005),         // Câu hỏi cần context vừa phải
                _ => (50, 0.01)                    // Default
            };
        }

        // ✅ BUILD SMART FILTER - Giữ nguyên logic phân quyền hiện tại
        private MemoryFilter BuildSmartFilter(DocumentRAGRequest request, string requestId)
        {
            var filter = new MemoryFilter();
            var role = request.Role?.ToUpper() ?? "NONE";

            // ✅ Base filter - tài liệu đã approved
            filter = filter.ByTag("status", "approved");

            // ❌ ADMIN - Không được search documents
            if (role == "ADMIN")
            {
                filter = filter.ByTag("accessLevel", "SUPER_ADMIN_ONLY");
                return filter;
            }

            // ✅ NONE role hoặc OnlyPublic - chỉ public docs
            if (role == "NONE" || request.OnlyPublic)
            {
                filter = filter.ByTag("isPublic", "True");
                _logger.LogDebug("🔒 [FILTER-{RequestId}] Public-only filter applied for role: {Role}",
                    requestId, role);
                return filter;
            }

            // ✅ CẢI THIỆN: Pre-filter theo department để tối ưu performance
            if (!string.IsNullOrEmpty(request.DepartmentId))
            {
                // ✅ Tạo OR condition: documents thuộc department HOẶC public documents
                var departmentFilter = new MemoryFilter()
                    .ByTag("status", "approved");

                // Option 1: Documents của department này
                var ownDeptFilter = departmentFilter.ByTag("departmentId", request.DepartmentId);

                // Option 2: Public documents (accessible to all departments)
                var publicFilter = new MemoryFilter()
                    .ByTag("status", "approved")
                    .ByTag("isPublic", "True");

                // ✅ QUAN TRỌNG: Combine filters cho department access
                // KernelMemory không hỗ trợ OR trực tiếp, nên ta sẽ search 2 lần và merge
                // Hoặc chỉ filter department trước, check public access sau

                filter = filter.ByTag("departmentId", request.DepartmentId);

                _logger.LogInformation("🔒 [FILTER-{RequestId}] Department filter applied: {DeptId} for role: {Role}",
                    requestId, request.DepartmentId, role);
            }
            else
            {
                // ✅ Nếu không có departmentId, chỉ cho phép public documents
                filter = filter.ByTag("isPublic", "True");
                _logger.LogWarning("🔒 [FILTER-{RequestId}] No department - restricting to public only for role: {Role}",
                    requestId, role);
            }

            return filter;
        }

        #endregion

        #region ✅ CRITICAL: COMPLETE PERMISSION BLOCKING

    

        #endregion

        #region ✅ OPTIMIZED CONTENT EXTRACTION

        private string ExtractOptimizedContent(List<Citation> citations, string query)
        {
            if (!citations.Any()) return null;

            var contentBuilder = new StringBuilder();
            var maxContentLength = 25000; // Tăng limit để có đủ content

            // ✅ Kiểm tra xem có phải 1 document duy nhất không
            var uniqueDocIds = citations.Select(c => GetDocumentIdFromCitation(c)).Distinct().ToList();
            var isSingleDocument = uniqueDocIds.Count == 1;

            _logger.LogInformation("📝 [CONTENT] Extracting from {DocCount} documents, {CitationCount} citations",
                uniqueDocIds.Count, citations.Count);

            if (isSingleDocument)
            {
                // ✅ SINGLE DOCUMENT - Load toàn bộ content theo thứ tự
                return ExtractSingleDocumentContent(citations, query, maxContentLength);
            }
            else
            {
                // ✅ MULTIPLE DOCUMENTS - Group theo document
                return ExtractMultipleDocumentsContent(citations, query, maxContentLength);
            }
        }

        // ✅ EXTRACT SINGLE DOCUMENT - Load toàn bộ document
        private string ExtractSingleDocumentContent(List<Citation> citations, string query, int maxLength)
        {
            var contentBuilder = new StringBuilder();
            var docTitle = GetTagValueFromCitation(citations.First(), "title") ??
                           GetTagValueFromCitation(citations.First(), "documentTitle") ?? "Document";

            contentBuilder.AppendLine($"📄 **{docTitle}**");
            contentBuilder.AppendLine();

            // ✅ Get ALL partitions và sort theo thứ tự logic
            var allPartitions = citations
                .SelectMany(c => c.Partitions)
                .Where(p => !string.IsNullOrWhiteSpace(p.Text) && p.Text.Length > 20)
                .OrderByDescending(p => p.Relevance) // Partition relevance cao nhất trước
                .ToList();

            var addedContent = new HashSet<string>();

            foreach (var partition in allPartitions)
            {
                if (contentBuilder.Length >= maxLength) break;

                var text = partition.Text.Trim();

                // ✅ Simple duplicate check - chỉ check exact duplicates
                var contentHash = text.Length > 100 ? text.Substring(0, 100) : text;
                if (addedContent.Contains(contentHash)) continue;

                addedContent.Add(contentHash);

                // ✅ Add content with spacing
                if (contentBuilder.Length > 100) // Không phải content đầu tiên
                {
                    contentBuilder.AppendLine("\n");
                }

                contentBuilder.AppendLine(text);
            }

            var result = contentBuilder.ToString().Trim();
            _logger.LogInformation("📝 [SINGLE-DOC] Extracted {Length} chars from {Partitions} partitions",
                result.Length, allPartitions.Count);

            return result;
        }

        // ✅ EXTRACT MULTIPLE DOCUMENTS - Group theo document
        private string ExtractMultipleDocumentsContent(List<Citation> citations, string query, int maxLength)
        {
            var contentBuilder = new StringBuilder();

            // ✅ Group by document và sort by relevance
            var citationsByDoc = citations
                .GroupBy(c => GetDocumentIdFromCitation(c))
                .OrderByDescending(g => g.Max(c => c.Partitions.Max(p => p.Relevance)))
                .Take(5) // Top 5 documents
                .ToList();

            foreach (var docGroup in citationsByDoc)
            {
                if (contentBuilder.Length >= maxLength) break;

                var firstCitation = docGroup.First();
                var docTitle = GetTagValueFromCitation(firstCitation, "title") ??
                               GetTagValueFromCitation(firstCitation, "documentTitle") ?? "Document";

                // ✅ Add document separator
                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine("\n" + new string('=', 50) + "\n");
                }

                contentBuilder.AppendLine($"📄 **{docTitle}**:");
                contentBuilder.AppendLine();

                // ✅ Get best partitions from this document
                var bestPartitions = docGroup
                    .SelectMany(c => c.Partitions)
                    .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                    .OrderByDescending(p => p.Relevance)
                    .Take(5) // Top 5 partitions per document
                    .ToList();

                foreach (var partition in bestPartitions)
                {
                    contentBuilder.AppendLine(partition.Text);
                    contentBuilder.AppendLine();

                    if (contentBuilder.Length >= maxLength) break;
                }
            }

            var result = contentBuilder.ToString().Trim();
            _logger.LogInformation("📝 [MULTI-DOC] Extracted {Length} chars from {DocCount} documents",
                result.Length, citationsByDoc.Count);

            return result;
        }

        #endregion

        #region ✅ COMPLETE METADATA EXTRACTION

        /// <summary>
        /// Extract COMPLETE document sources with ALL metadata available
        /// </summary>
        private async Task<List<DocumentSourceResponse>> ExtractDocumentSources(List<Citation> citations, string requestId)
        {
            try
            {
                var sources = new List<DocumentSourceResponse>();
                var processedDocuments = new HashSet<string>(); // Avoid duplicate documents

                _logger.LogInformation("📋 [SOURCES-{RequestId}] Extracting COMPLETE metadata from {Count} citations",
                    requestId, citations.Count);

                foreach (var citation in citations.Take(15)) // Increase limit for more sources
                {
                    try
                    {
                        var documentId = GetDocumentIdFromCitation(citation);

                        // Skip if already processed this document
                        if (processedDocuments.Contains(documentId))
                            continue;

                        processedDocuments.Add(documentId);

                        var source = ExtractCompleteDocumentMetadata(citation, requestId);
                        if (source != null)
                        {
                            sources.Add(source);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "📋 [SOURCES-{RequestId}] Error processing citation", requestId);
                    }
                }

                // Sort by relevance and return
                var sortedSources = sources
                    .OrderByDescending(s => s.RelevanceScore)
                    .ToList();

                _logger.LogInformation("📋 [SOURCES-{RequestId}] Extracted {Count} complete document sources",
                    requestId, sortedSources.Count);

                return sortedSources;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📋 [SOURCES-{RequestId}] Error extracting document sources", requestId);
                return new List<DocumentSourceResponse>();
            }
        }

        /// <summary>
        /// Extract COMPLETE metadata for a single document - ALL FIELDS POSSIBLE
        /// </summary>
        private DocumentSourceResponse ExtractCompleteDocumentMetadata(Citation citation, string requestId)
        {
            var source = new DocumentSourceResponse();

            try
            {
                // ✅ CORE IDENTIFIERS - Tất cả IDs quan trọng
                source.DocumentId = GetDocumentIdFromCitation(citation) ?? Guid.NewGuid().ToString();
                source.VersionId = GetTagValueFromCitation(citation, "versionId");
                source.DepartmentId = GetTagValueFromCitation(citation, "departmentId");
                source.OwnerId = GetTagValueFromCitation(citation, "ownerId");
                source.Status = GetTagValueFromCitation(citation, "status");
                source.VersionName = GetTagValueFromCitation(citation, "version") ?? "1.0";
                source.CreatedBy = GetTagValueFromCitation(citation, "createdBy");
                source.SubmittedBy = GetTagValueFromCitation(citation, "submittedBy");

                // ✅ BOOLEAN VALUES - EXISTING
                source.IsOfficial = ParseBooleanTag(citation, "isOfficial");
                source.IsPublic = ParseBooleanTag(citation, "isPublic");

                // ✅ NEW BOOLEAN VALUES
                source.IsLatestVersion = ParseBooleanTag(citation, "isLatestVersion");
                source.IsArchived = ParseBooleanTag(citation, "isArchived");

                // ✅ DOCUMENT CORE METADATA - EXISTING + NEW
                source.Title = GetTagValueFromCitation(citation, "title") ??
                              GetTagValueFromCitation(citation, "documentTitle") ?? "Document";
                source.Description = GetTagValueFromCitation(citation, "description");
                source.Summary = GetTagValueFromCitation(citation, "summary");
                source.VersionTitle = GetTagValueFromCitation(citation, "versionTitle");
                source.DocumentType = GetTagValueFromCitation(citation, "documentType");
                source.DocumentTypeName = GetTagValueFromCitation(citation, "documentTypeName");
                source.DocumentTypeDescription = GetTagValueFromCitation(citation, "documentTypeDescription");
                source.SignedBy = GetTagValueFromCitation(citation, "signedBy"); // ✅ QUAN TRỌNG: Ai ký

                // ✅ NEW DOCUMENT METADATA
                source.ApprovedBy = GetTagValueFromCitation(citation, "approvedBy");
                source.Category = GetTagValueFromCitation(citation, "category");
                source.Priority = GetTagValueFromCitation(citation, "priority");
                source.DocumentLanguage = GetTagValueFromCitation(citation, "language");
                source.AccessLevel = GetTagValueFromCitation(citation, "accessLevel");
                source.ConfidentialityLevel = GetTagValueFromCitation(citation, "confidentialityLevel");

                // ✅ DATES - EXISTING + NEW
                source.ApprovalDate = ParseDateTag(citation, "approvalDate");
                source.LastSubmitted = ParseDateTag(citation, "lastSubmitted");
                source.EffectiveFrom = ParseDateTag(citation, "effectiveFrom");
                source.EffectiveUntil = ParseDateTag(citation, "effectiveUntil");
                source.ReviewDate = ParseDateTag(citation, "reviewDate");

                // ✅ NEW DATE FIELDS
                source.SignedDate = ParseDateTag(citation, "signedDate");
                source.ExpiryDate = ParseDateTag(citation, "expiryDate");
                source.PreviousApprovedAt = ParseDateTag(citation, "previousApprovedAt");

                // ✅ NUMERIC VALUES - EXISTING + NEW  
                source.FileSize = ParseLongTag(citation, "fileSize");

                // ✅ NEW NUMERIC FIELDS
                source.VersionNumber = ParseIntTag(citation, "versionNumber");
                source.PageCount = ParseIntTag(citation, "pageCount");
                source.WordCount = ParseIntTag(citation, "wordCount");

                // ✅ CLASSIFICATION & TAGS - EXISTING
                source.Tags = ParseTagsFromCitation(citation);

                // ✅ ORGANIZATIONAL METADATA - EXISTING + NEW
                source.DepartmentName = GetTagValueFromCitation(citation, "departmentName");
                source.OwnerName = GetTagValueFromCitation(citation, "ownerName");
                source.OwnerEmail = GetTagValueFromCitation(citation, "ownerEmail");
                source.ReviewerId = GetTagValueFromCitation(citation, "reviewerId");
                source.ReviewerName = GetTagValueFromCitation(citation, "reviewerName");
                source.ReviewComments = GetTagValueFromCitation(citation, "reviewComments");
                source.ReviewAction = GetTagValueFromCitation(citation, "reviewAction");

                // ✅ FILE SYSTEM METADATA - EXISTING
                source.FileName = GetTagValueFromCitation(citation, "fileName");
                source.FileType = GetTagValueFromCitation(citation, "fileType");
                source.FileHash = GetTagValueFromCitation(citation, "fileHash");
                source.GoogleDriveFileId = GetTagValueFromCitation(citation, "googleDriveFileId");
                source.FolderPath = GetTagValueFromCitation(citation, "folderPath");
                source.StorageLocation = GetTagValueFromCitation(citation, "storageLocation");

                // ✅ RELATIONSHIP METADATA - EXISTING + NEW
                source.ReplacementOfDocumentId = GetTagValueFromCitation(citation, "replacementOfDocumentId");
                source.ReplacedDocumentId = GetTagValueFromCitation(citation, "replacedDocumentId");
                source.PreviousApprovedVersionId = GetTagValueFromCitation(citation, "previousApprovedVersionId");
                source.PreviousApprovedVersionName = GetTagValueFromCitation(citation, "previousApprovedVersionName");

                // ✅ NEW RELATIONSHIP FIELDS
                source.ParentDocumentId = GetTagValueFromCitation(citation, "parentDocumentId");
                source.RelatedDocumentIds = ParseRelatedDocumentIds(citation);

                // ✅ ACCESS CONTROL METADATA - EXISTING
                source.Visibility = GetTagValueFromCitation(citation, "visibility");
                source.PermissionLevel = GetTagValueFromCitation(citation, "permissionLevel");
                source.DepartmentRestriction = GetTagValueFromCitation(citation, "departmentRestriction");

                // ✅ SEARCH-SPECIFIC METADATA - EXISTING
                source.RelevanceScore = citation.Partitions.Any() ?
                    citation.Partitions.Max(p => p.Relevance) : 0.0;

                source.SearchSnippet = ExtractSearchSnippet(citation);
                source.ContentPreview = ExtractContentPreview(citation);

                // ✅ SEARCH KEYWORDS - Enhance existing
                source.MatchedKeywords = ExtractMatchedKeywords(citation);

                _logger.LogDebug("📋 [SOURCES-{RequestId}] Extracted COMPLETE metadata: {DocId} - {Title} - SignedBy: {SignedBy} - ApprovedBy: {ApprovedBy}",
                    requestId, source.DocumentId, source.Title, source.SignedBy ?? "Unknown", source.ApprovedBy ?? "Unknown");

                return source;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📋 [SOURCES-{RequestId}] Error extracting complete metadata", requestId);
                return null;
            }
        }

        #endregion

        #region Enhanced Helper Methods cho Complete Metadata Extraction

        private List<string> ExtractMatchedKeywords(Citation citation)
        {
            var keywords = new HashSet<string>();

            // Extract from tags if available
            var keywordsValue = GetTagValueFromCitation(citation, "keywords");
            if (!string.IsNullOrEmpty(keywordsValue))
            {
                try
                {
                    if (keywordsValue.StartsWith("[") && keywordsValue.EndsWith("]"))
                    {
                        var jsonKeywords = System.Text.Json.JsonSerializer.Deserialize<string[]>(keywordsValue);
                        if (jsonKeywords != null)
                        {
                            foreach (var keyword in jsonKeywords)
                            {
                                keywords.Add(keyword);
                            }
                        }
                    }
                    else if (keywordsValue.Contains(","))
                    {
                        var splitKeywords = keywordsValue.Split(',')
                            .Select(k => k.Trim())
                            .Where(k => !string.IsNullOrEmpty(k));

                        foreach (var keyword in splitKeywords)
                        {
                            keywords.Add(keyword);
                        }
                    }
                    else
                    {
                        keywords.Add(keywordsValue.Trim());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "📋 Error parsing keywords: {Keywords}", keywordsValue);
                }
            }

            // Extract from tags as fallback
            var tags = ParseTagsFromCitation(citation);
            foreach (var tag in tags.Take(10)) // Limit to avoid too many keywords
            {
                keywords.Add(tag);
            }

            return keywords.Take(20).ToList(); // Limit to 20 keywords max
        }
        private List<string> ParseRelatedDocumentIds(Citation citation)
        {
            var relatedValue = GetTagValueFromCitation(citation, "relatedDocumentIds");
            if (string.IsNullOrEmpty(relatedValue))
                return new List<string>();

            try
            {
                // Try JSON array format first
                if (relatedValue.StartsWith("[") && relatedValue.EndsWith("]"))
                {
                    var jsonIds = System.Text.Json.JsonSerializer.Deserialize<string[]>(relatedValue);
                    return jsonIds?.ToList() ?? new List<string>();
                }

                // Try comma-separated format
                if (relatedValue.Contains(","))
                {
                    return relatedValue.Split(',')
                        .Select(id => id.Trim())
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();
                }

                // Single ID
                return new List<string> { relatedValue.Trim() };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "📋 Error parsing related document IDs: {RelatedIds}", relatedValue);
                return new List<string>();
            }
        }
        private int ParseIntTag(Citation citation, string tagKey)
        {
            var value = GetTagValueFromCitation(citation, tagKey);
            if (string.IsNullOrEmpty(value)) return 0;

            if (int.TryParse(value, out var result))
                return result;

            return 0;
        }
        /// <summary>
        /// Parse boolean values from citation tags
        /// </summary>
        private bool ParseBooleanTag(Citation citation, string tagKey)
        {
            var value = GetTagValueFromCitation(citation, tagKey);
            if (string.IsNullOrEmpty(value)) return false;

            return value.ToLower() switch
            {
                "true" => true,
                "1" => true,
                "yes" => true,
                "on" => true,
                _ => false
            };
        }

        /// <summary>
        /// Parse date values from citation tags với multiple formats
        /// </summary>
        private DateTime? ParseDateTag(Citation citation, string tagKey)
        {
            var value = GetTagValueFromCitation(citation, tagKey);
            if (string.IsNullOrEmpty(value)) return null;

            // Try multiple date formats
            var formats = new[]
            {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            "o", // ISO 8601
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy",
            "dd/MM/yyyy"
        };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }

            // Fallback to generic parse
            if (DateTime.TryParse(value, out var genericDate))
            {
                return genericDate;
            }

            return null;
        }

        /// <summary>
        /// Parse long values from citation tags
        /// </summary>
        private long? ParseLongTag(Citation citation, string tagKey)
        {
            var value = GetTagValueFromCitation(citation, tagKey);
            if (string.IsNullOrEmpty(value)) return null;

            if (long.TryParse(value, out var result))
                return result;

            return null;
        }

        /// <summary>
        /// Parse tags from citation - handle multiple formats (JSON array, comma-separated)
        /// </summary>
        private List<string> ParseTagsFromCitation(Citation citation)
        {
            var tagsValue = GetTagValueFromCitation(citation, "tags");
            if (string.IsNullOrEmpty(tagsValue))
                return new List<string>();

            try
            {
                // Try JSON array format first
                if (tagsValue.StartsWith("[") && tagsValue.EndsWith("]"))
                {
                    var jsonTags = System.Text.Json.JsonSerializer.Deserialize<string[]>(tagsValue);
                    return jsonTags?.ToList() ?? new List<string>();
                }

                // Try comma-separated format
                if (tagsValue.Contains(","))
                {
                    return tagsValue.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }

                // Single tag
                return new List<string> { tagsValue.Trim() };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "📋 Error parsing tags: {Tags}", tagsValue);
                return new List<string>();
            }
        }

        /// <summary>
        /// Extract search snippet from citation content
        /// </summary>
        private string ExtractSearchSnippet(Citation citation)
        {
            var bestPartition = citation.Partitions
                .OrderByDescending(p => p.Relevance)
                .FirstOrDefault();

            if (bestPartition?.Text == null) return "";

            var text = bestPartition.Text.Trim();

            // Return first 200 characters with ellipsis if longer
            if (text.Length <= 200) return text;

            var snippet = text.Substring(0, 200);
            var lastSpace = snippet.LastIndexOf(' ');

            if (lastSpace > 150) // Avoid cutting mid-word
            {
                snippet = snippet.Substring(0, lastSpace);
            }

            return snippet + "...";
        }

        /// <summary>
        /// Extract content preview (first meaningful paragraph)
        /// </summary>
        private string ExtractContentPreview(Citation citation)
        {
            var allText = string.Join(" ", citation.Partitions
                .OrderByDescending(p => p.Relevance)
                .Take(3)
                .Select(p => p.Text?.Trim())
                .Where(t => !string.IsNullOrEmpty(t)));

            if (string.IsNullOrEmpty(allText)) return "";

            // Find first complete sentence or paragraph
            var sentences = allText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var preview = sentences.Take(2).FirstOrDefault()?.Trim();

            if (string.IsNullOrEmpty(preview)) return allText.Length > 300 ? allText.Substring(0, 300) + "..." : allText;

            return preview.Length > 300 ? preview.Substring(0, 300) + "..." : preview + ".";
        }

        #endregion

        #region Helper Methods (Keep existing implementation)

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

        private bool IsDocumentCurrentlyEffective(Citation citation, DateTime today, string requestId)
        {
            try
            {
                var effectiveFromStr = GetTagValueFromCitation(citation, "effectiveFrom");
                var effectiveUntilStr = GetTagValueFromCitation(citation, "effectiveUntil");

                // Check effective from
                if (!string.IsNullOrEmpty(effectiveFromStr))
                {
                    if (DateTime.TryParse(effectiveFromStr, out var effectiveFrom))
                    {
                        if (today < effectiveFrom.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document not yet effective: EffectiveFrom {EffFrom} > Today {Today}",
                                requestId, effectiveFrom.Date, today);
                            return false;
                        }
                    }
                }

                // Check effective until
                if (!string.IsNullOrEmpty(effectiveUntilStr))
                {
                    if (DateTime.TryParse(effectiveUntilStr, out var effectiveUntil))
                    {
                        if (today > effectiveUntil.Date)
                        {
                            _logger.LogDebug("⏰ [EFFECTIVE-{RequestId}] Document expired: EffectiveUntil {EffUntil} < Today {Today}",
                                requestId, effectiveUntil.Date, today);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⏰ [EFFECTIVE-{RequestId}] Error checking effectiveness - denying access", requestId);
                return false;
            }
        }

        private async Task<bool> IsDocumentAccessibleToUser(Citation citation, DocumentRAGRequest userContext, string requestId)
        {
            try
            {
                var role = userContext.Role?.ToUpper() ?? "NONE";
                var documentId = GetDocumentIdFromCitation(citation);

                // ❌ ADMIN - NO ACCESS TO DOCUMENTS
                if (role == "ADMIN")
                {
                    _logger.LogDebug("🔒 [ACCESS-{RequestId}] DENIED - Admin role cannot access documents: {DocId}",
                        requestId, documentId);
                    return false;
                }

                // ✅ GET DOCUMENT METADATA
                var docDepartmentId = GetTagValueFromCitation(citation, "departmentId");
                var ownerId = GetTagValueFromCitation(citation, "ownerId");
                var isPublic = ParseBooleanTag(citation, "isPublic");

                _logger.LogDebug("🔍 [ACCESS-{RequestId}] Checking access for {DocId}: DocDept={DocDept}, Owner={Owner}, Public={Public}, UserDept={UserDept}, UserId={UserId}",
                    requestId, documentId, docDepartmentId ?? "None", ownerId ?? "None", isPublic,
                    userContext.DepartmentId ?? "None", userContext.UserId ?? "None");

                // ✅ OWNER ACCESS
                if (!string.IsNullOrEmpty(ownerId) && ownerId == userContext.UserId)
                {
                    _logger.LogDebug("✅ [ACCESS-{RequestId}] GRANTED - Owner access: {UserId} owns {DocId}",
                        requestId, userContext.UserId, documentId);
                    return true;
                }

                // ✅ PUBLIC DOCUMENTS
                if (isPublic)
                {
                    _logger.LogDebug("✅ [ACCESS-{RequestId}] GRANTED - Public document: {DocId}",
                        requestId, documentId);
                    return true;
                }

                // ✅ DEPARTMENT ACCESS
                if (!string.IsNullOrEmpty(userContext.DepartmentId) &&
                    !string.IsNullOrEmpty(docDepartmentId) &&
                    docDepartmentId == userContext.DepartmentId)
                {
                    switch (role)
                    {
                        case "MANAGER":
                        case "EDITOR":
                        case "MEMBER":
                        case "EMPLOYEE":
                            _logger.LogDebug("✅ [ACCESS-{RequestId}] GRANTED - Department access: {Role} in {DeptId} can access {DocId}",
                                requestId, role, userContext.DepartmentId, documentId);
                            return true;

                        default:
                            _logger.LogDebug("🔒 [ACCESS-{RequestId}] DENIED - Invalid role {Role} for department access: {DocId}",
                                requestId, role, documentId);
                            return false;
                    }
                }

                // ✅ SPECIAL PERMISSIONS
                if (userContext.Permissions?.Any(p => new[] {
            "VIEW_ANY_DOCUMENT",
            "VIEW_DEPARTMENT_DOCUMENT"
        }.Contains(p)) == true)
                {
                    _logger.LogDebug("✅ [ACCESS-{RequestId}] GRANTED - Special permission access: {DocId}",
                        requestId, documentId);
                    return true;
                }

                _logger.LogDebug("🔒 [ACCESS-{RequestId}] DENIED - No matching access criteria: {DocId} (UserDept: {UserDept}, DocDept: {DocDept})",
                    requestId, documentId, userContext.DepartmentId ?? "None", docDepartmentId ?? "None");

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔒 [ACCESS-{RequestId}] ERROR - Denying access by default for safety", requestId);
                return false;
            }
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