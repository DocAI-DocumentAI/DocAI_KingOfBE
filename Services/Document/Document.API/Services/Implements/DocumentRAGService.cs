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
        private readonly IKernelMemory _memory;
        private readonly INameLookupService _nameLookupService;
        private readonly IDocumentEnrichmentService _enrichmentService;

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
        }

        public async Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                // ✅ Get user permissions from JWT token using BaseService
                var userId = GetUserIdFromJwt();
                var userDepartmentId = GetDepartmentFromJwt();
                var userRole = GetRoleFromJwt();
                var isAdmin = IsAdminUser();

                Console.WriteLine($"🔍 [RAG] UserId: {userId}");
                Console.WriteLine($"🔍 [RAG] User Department: {userDepartmentId}");
                Console.WriteLine($"🔍 [RAG] User Role: {userRole}");
                Console.WriteLine($"🔍 [RAG] Is Admin: {isAdmin}");

                // ✅ Perform multiple searches based on permissions
                var allCitations = new List<Citation>();

                // Search 1: Department documents
                if (!string.IsNullOrEmpty(userDepartmentId))
                {
                    Console.WriteLine($"🔍 [RAG] Searching department documents...");
                    var deptResults = await SearchWithFilter(request,
                        filter => filter
                            .ByTag("status", "approved")
                            .ByTag("isOfficial", "true")
                            .ByTag("departmentId", userDepartmentId));

                    allCitations.AddRange(deptResults.Results);
                    Console.WriteLine($"🔍 [RAG] Department search: {deptResults.Results.Count()} results");
                }

                // Search 2: Public documents
                Console.WriteLine($"🔍 [RAG] Searching public documents...");
                var publicResults = await SearchWithFilter(request,
                    filter => filter
                        .ByTag("status", "approved")
                        .ByTag("isOfficial", "true")
                        .ByTag("isPublic", "true"));

                allCitations.AddRange(publicResults.Results);
                Console.WriteLine($"🔍 [RAG] Public search: {publicResults.Results.Count()} results");

                // Search 3: User's own documents
                Console.WriteLine($"🔍 [RAG] Searching user's own documents...");
                var ownResults = await SearchWithFilter(request,
                    filter => filter
                        .ByTag("status", "approved")
                        .ByTag("isOfficial", "true")
                        .ByTag("ownerId", userId));

                allCitations.AddRange(ownResults.Results);
                Console.WriteLine($"🔍 [RAG] Own documents search: {ownResults.Results.Count()} results");

                // Search 4: Admin access (if enabled)
                if (isAdmin)
                {
                    Console.WriteLine($"🔍 [RAG] Searching all documents (admin access)...");
                    var adminResults = await SearchWithFilter(request,
                        filter => filter
                            .ByTag("status", "approved")
                            .ByTag("isOfficial", "true"));

                    allCitations.AddRange(adminResults.Results);
                    Console.WriteLine($"🔍 [RAG] Admin search: {adminResults.Results.Count()} results");
                }

                Console.WriteLine($"🔍 [RAG] Total citations before filtering: {allCitations.Count}");

                // ✅ Filter by effective dates
                var effectiveCitations = FilterByEffectiveDates(allCitations);
                Console.WriteLine($"🔍 [RAG] After effective date filter: {effectiveCitations.Count}");

                // ✅ Filter by permissions
                var allowedCitations = FilterByPermissions(effectiveCitations, userId, userDepartmentId, isAdmin);
                Console.WriteLine($"🔍 [RAG] After permission filter: {allowedCitations.Count}");

                // ✅ Deduplicate and take top results
                var finalCitations = DeduplicateAndRank(allowedCitations, request.MaxResults);
                Console.WriteLine($"🔍 [RAG] Final citations: {finalCitations.Count}");

                if (!finalCitations.Any())
                {
                    return CreateEmptyResponse(request, startTime);
                }

                // ✅ Generate RAG answer
                Console.WriteLine($"🔍 [RAG] Generating RAG answer...");
                var ragAnswer = await GenerateRAGAnswer(request.Query, userId, userDepartmentId, isAdmin);
                Console.WriteLine($"🔍 [RAG] Generated answer length: {ragAnswer?.Length ?? 0}");

                // ✅ Extract document sources
                var sources = await ExtractDocumentSources(finalCitations, request.MaxResults);
                Console.WriteLine($"🔍 [RAG] Extracted {sources.Count} sources");

                var response = new DocumentRAGResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Answer = ragAnswer,
                    Sources = sources,
                    QueryProcessed = request.Query,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };

                _logger.LogInformation("✅ [RAG] RAG processing completed: {RequestId} - Sources: {SourceCount}",
                    request.RequestId, sources.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [RAG] Error processing RAG request: {RequestId}", request.RequestId);
                return CreateErrorResponse(request, ex);
            }
        }

        /// <summary>
        /// Helper method để search với filter
        /// </summary>
        private async Task<SearchResult> SearchWithFilter(DocumentRAGRequest request, Func<MemoryFilter, MemoryFilter> filterBuilder)
        {
            var filter = filterBuilder(new MemoryFilter());

            return await _memory.SearchAsync(
                request.Query,
                limit: request.MaxResults,
                filter: filter,
                minRelevance: request.MinRelevanceScore ?? 0.7);
        }

        /// <summary>
        /// Filter citations by effective dates
        /// </summary>
        private List<Citation> FilterByEffectiveDates(List<Citation> citations)
        {
            var today = DateTime.UtcNow.Date;
            var effectiveCitations = new List<Citation>();

            foreach (var citation in citations)
            {
                try
                {
                    var firstPartition = citation.Partitions.FirstOrDefault();
                    if (firstPartition?.Tags == null)
                    {
                        effectiveCitations.Add(citation);
                        continue;
                    }

                    var tags = firstPartition.Tags;
                    var isEffective = true;

                    // Check effectiveFrom
                    if (tags.TryGetValue("effectiveFrom", out var effectiveFromValues))
                    {
                        var effectiveFromStr = effectiveFromValues.FirstOrDefault();
                        if (!string.IsNullOrEmpty(effectiveFromStr) &&
                            DateTime.TryParse(effectiveFromStr, out var effectiveFrom))
                        {
                            if (today < effectiveFrom.Date)
                            {
                                isEffective = false;
                                Console.WriteLine($"📅 [RAG] Document not yet effective: {effectiveFromStr}");
                            }
                        }
                    }

                    // Check effectiveUntil
                    if (isEffective && tags.TryGetValue("effectiveUntil", out var effectiveUntilValues))
                    {
                        var effectiveUntilStr = effectiveUntilValues.FirstOrDefault();
                        if (!string.IsNullOrEmpty(effectiveUntilStr) &&
                            DateTime.TryParse(effectiveUntilStr, out var effectiveUntil))
                        {
                            if (today > effectiveUntil.Date)
                            {
                                isEffective = false;
                                Console.WriteLine($"📅 [RAG] Document expired: {effectiveUntilStr}");
                            }
                        }
                    }

                    if (isEffective)
                    {
                        effectiveCitations.Add(citation);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking effective dates, including citation");
                    effectiveCitations.Add(citation);
                }
            }

            return effectiveCitations;
        }

        /// <summary>
        /// Filter citations by user permissions
        /// </summary>
        private List<Citation> FilterByPermissions(List<Citation> citations, string userId, string userDepartmentId, bool isAdmin)
        {
            var allowedCitations = new List<Citation>();

            foreach (var citation in citations)
            {
                try
                {
                    var firstPartition = citation.Partitions.FirstOrDefault();
                    if (firstPartition?.Tags == null)
                    {
                        continue;
                    }

                    var tags = firstPartition.Tags;

                    // Extract document info
                    var documentDepartmentId = tags.ContainsKey("departmentId") ? tags["departmentId"].FirstOrDefault() : null ?? "";
                    var isPublicStr = tags.ContainsKey("isPublic") ? tags["isPublic"].FirstOrDefault() : null ?? "false";
                    var ownerId = tags.ContainsKey("ownerId") ? tags["ownerId"].FirstOrDefault() : null ?? "";

                    bool.TryParse(isPublicStr, out bool isPublic);

                    // Permission check logic
                    var canAccess = false;

                    // Admin can access everything (if enabled)
                    if (isAdmin)
                    {
                        canAccess = true;
                        Console.WriteLine($"👑 [RAG] Admin access granted");
                    }
                    // Owner can access their documents
                    else if (ownerId == userId)
                    {
                        canAccess = true;
                        Console.WriteLine($"👤 [RAG] Owner access granted");
                    }
                    // Public documents accessible to everyone
                    else if (isPublic)
                    {
                        canAccess = true;
                        Console.WriteLine($"🌐 [RAG] Public document access granted");
                    }
                    // Department documents accessible to department members
                    else if (!string.IsNullOrEmpty(userDepartmentId) && documentDepartmentId == userDepartmentId)
                    {
                        canAccess = true;
                        Console.WriteLine($"🏢 [RAG] Department access granted");
                    }

                    if (canAccess)
                    {
                        allowedCitations.Add(citation);
                    }
                    else
                    {
                        Console.WriteLine($"❌ [RAG] Access denied - DocDept: {documentDepartmentId}, UserDept: {userDepartmentId}, Public: {isPublic}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking permissions, including citation");
                    allowedCitations.Add(citation);
                }
            }

            return allowedCitations;
        }

        /// <summary>
        /// Deduplicate and rank citations
        /// </summary>
        private List<Citation> DeduplicateAndRank(List<Citation> citations, int maxResults)
        {
            return citations
                .GroupBy(c => GetDocumentIdFromCitation(c))
                .Select(g => g.OrderByDescending(c => c.Partitions.Max(p => p.Relevance)).First())
                .OrderByDescending(c => c.Partitions.Max(p => p.Relevance))
                .Take(maxResults)
                .ToList();
        }

        private async Task<string> GenerateRAGAnswer(string query, string userId, string userDepartmentId, bool isAdmin)
        {
            try
            {
                var filter = new MemoryFilter()
                    .ByTag("status", "approved")
                    .ByTag("isOfficial", "true");

                // Don't add more specific filters for RAG generation
                // Let it use all accessible content for better answers

                var ragAnswer = await _memory.AskAsync(
                    question: query,
                    filter: filter);

                if (string.IsNullOrEmpty(ragAnswer.Result) ||
                    ragAnswer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
                    ragAnswer.Result.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase))
                {
                    return "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.";
                }

                return ragAnswer.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG answer");
                return "Xin lỗi, đã xảy ra lỗi khi tạo câu trả lời từ tài liệu.";
            }
        }
        private async Task<List<DocumentSourceResponse>> ExtractDocumentSources(List<Citation> citations, int maxResults)
        {
            try
            {
                var documentGroups = citations
                    .SelectMany(citation => citation.Partitions.Select(partition => new
                    {
                        DocumentId = partition.Tags.ContainsKey("documentId") ?
                            partition.Tags["documentId"].FirstOrDefault() : null,
                        Relevance = partition.Relevance,
                        Tags = partition.Tags
                    }))
                    .Where(x => !string.IsNullOrEmpty(x.DocumentId))
                    .GroupBy(x => x.DocumentId)
                    .Select(g => new
                    {
                        DocumentId = g.Key,
                        MaxRelevance = g.Max(x => x.Relevance),
                        Tags = g.First().Tags
                    })
                    .OrderByDescending(x => x.MaxRelevance)
                    .Take(maxResults)
                    .ToList();

                if (!documentGroups.Any())
                    return new List<DocumentSourceResponse>();

                // Bulk query for better performance
                var documentIds = documentGroups.Select(g => g.DocumentId).ToList();

                var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(
                        predicate: dv => documentIds.Contains(dv.DocumentFile.Id) && dv.IsOfficial,
                        include: i => i.Include(dv => dv.DocumentFile)
                                      .Include(dv => dv.DocumentTags)
                                      .ThenInclude(dt => dt.Tag));

                var documentLookup = documentVersions.ToDictionary(dv => dv.DocumentFile.Id);

                var sources = new List<DocumentSourceResponse>();

                foreach (var docGroup in documentGroups)
                {
                    var source = new DocumentSourceResponse
                    {
                        DocumentId = docGroup.DocumentId,
                        RelevanceScore = docGroup.MaxRelevance
                    };

                    // Extract from tags
                    if (docGroup.Tags.TryGetValue("versionName", out var versionNames))
                        source.VersionName = versionNames.FirstOrDefault() ?? "";

                    if (docGroup.Tags.TryGetValue("departmentId", out var deptIds))
                        source.DepartmentId = deptIds.FirstOrDefault() ?? "";

                    if (docGroup.Tags.TryGetValue("approvalDate", out var approvalDates))
                        if (DateTime.TryParse(approvalDates.FirstOrDefault(), out var approvalDate))
                            source.ApprovalDate = approvalDate;

                    if (docGroup.Tags.TryGetValue("signedBy", out var signedByValues))
                        source.SignedBy = signedByValues.FirstOrDefault();

                    if (docGroup.Tags.TryGetValue("effectiveFrom", out var effectiveFromValues))
                        if (DateTime.TryParse(effectiveFromValues.FirstOrDefault(), out var effectiveFrom))
                            source.EffectiveFrom = effectiveFrom;

                    if (docGroup.Tags.TryGetValue("effectiveUntil", out var effectiveUntilValues))
                        if (DateTime.TryParse(effectiveUntilValues.FirstOrDefault(), out var effectiveUntil))
                            source.EffectiveUntil = effectiveUntil;

                    // Get from database
                    if (documentLookup.TryGetValue(docGroup.DocumentId, out var documentVersion))
                    {
                        source.Title = documentVersion.Title;
                        source.Summary = documentVersion.Summary;
                        source.FileType = documentVersion.FileType;
                        source.DepartmentId = documentVersion.DocumentFile.DepartmentId;
                        source.SignedBy = documentVersion.SignedBy;
                        source.EffectiveFrom = documentVersion.EffectiveFrom;
                        source.EffectiveUntil = documentVersion.EffectiveUntil;
                    }

                    sources.Add(source);
                }

                return await EnrichSourcesWithNames(sources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting document sources");
                return new List<DocumentSourceResponse>();
            }
        }

        private async Task<List<DocumentSourceResponse>> EnrichSourcesWithNames(List<DocumentSourceResponse> sources)
        {
            try
            {
                // Use the centralized enrichment service
                return await _enrichmentService.EnrichDocumentSourceResponsesAsync(sources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching sources with names");
                return sources;
            }
        }

        private string GetDocumentIdFromCitation(Citation citation)
        {
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition?.Tags != null && firstPartition.Tags.ContainsKey("documentId"))
            {
                return firstPartition.Tags["documentId"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            }
            return Guid.NewGuid().ToString();
        }

        private DocumentRAGResponse CreateEmptyResponse(DocumentRAGRequest request, DateTime startTime)
        {
            return new DocumentRAGResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Answer = "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.",
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

        public async Task<DocumentRAGResponse> SearchOfficialDocumentsAsync(DocumentRAGRequest request)
        {
            var officialRequest = request with
            {
                MinRelevanceScore = 0.8
            };

            var response = await SearchDocumentsWithRAGAsync(officialRequest);

            if (response.Success && !string.IsNullOrEmpty(response.Answer))
            {
                response.Answer = $"**Thông tin chính thức:**\n{response.Answer}";
            }

            return response;
        }

        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            var request = new DocumentRAGRequest
            {
                Query = query,
                UserId = userId,
                MaxResults = 5,
                MinRelevanceScore = 0.7
            };

            var response = await SearchDocumentsWithRAGAsync(request);
            return response.Success ? response.Answer : "Xin lỗi, tôi không tìm thấy thông tin phù hợp.";
        }

        public async Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId)
        {
            var request = new DocumentRAGRequest
            {
                Query = query,
                UserId = userId,
                MaxResults = 3,
                MinRelevanceScore = 0.7
            };

            var response = await SearchDocumentsWithRAGAsync(request);

            if (!response.Success || string.IsNullOrEmpty(response.Answer))
                return "Xin lỗi, tôi không tìm thấy thông tin phù hợp.";

            var answer = new StringBuilder();
            answer.AppendLine(response.Answer);

            if (response.Sources.Any())
            {
                answer.AppendLine();
                answer.AppendLine("**Nguồn tham khảo:**");

                foreach (var source in response.Sources.Take(3))
                {
                    answer.AppendLine($"• {source.Title} v{source.VersionName} (Score: {source.RelevanceScore:F2})");
                }
            }

            return answer.ToString();
        }

    }
}
