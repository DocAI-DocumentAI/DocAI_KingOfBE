using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.API.Constants;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;

using System.Text.Json;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for performing document search using Kernel Memory with natural language queries
    /// Provides AI-powered answers with source document citations
    /// </summary>
    public class SearchService : Document.API.Services.BaseService<SearchService>, ISearchService
    {
        private readonly IKernelMemoryConfigurationService _kernelMemoryConfigService;
        private readonly INameLookupService _nameLookupService;

        public SearchService(
            IKernelMemoryConfigurationService kernelMemoryConfigService,
            IUnitOfWork unitOfWork,
            ILogger<SearchService> logger,
            IConfiguration configuration,
            INameLookupService nameLookupService,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor)
            : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
        {
            _kernelMemoryConfigService = kernelMemoryConfigService;
            _nameLookupService = nameLookupService;
            _logger = logger;
        }

        /// <summary>
        /// Performs a natural language search using Kernel Memory's AskAsync functionality
        /// Returns AI-generated answers with source document citations
        /// </summary>
        public async Task<EnhancedSemanticSearchResponse> SearchWithKernelMemoryAsync(
            SemanticSearchRequest request,
            KernelMemorySearchFilter filter)
        {
            var startTime = DateTime.UtcNow;
            var userId = GetUserIdFromJwt();
            var userDepartmentId = GetDepartmentFromJwt();
            var userRole = GetRoleFromJwt();
            var requestId = Guid.NewGuid().ToString();

            _logger.LogInformation("🔍 [SEARCH-KM] Starting Kernel Memory search - User: {UserId}, Department: {DepartmentId}, Query: '{Query}', RequestId: {RequestId}",
                userId, userDepartmentId, request.Query.Substring(0, Math.Min(50, request.Query.Length)), requestId);

            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    throw new ArgumentException("Search query is required", nameof(request.Query));
                }

                // Get Kernel Memory instance
                var memory = await _kernelMemoryConfigService.GetConfiguredKernelMemoryAsync();

                // Build memory filter based on user access control
                var memoryFilter = BuildMemoryFilter(request, filter, userId, userDepartmentId, userRole);

                _logger.LogInformation("🔍 [SEARCH-KM] Executing AskAsync with filter - RequestId: {RequestId}", requestId);

                // Create context for custom prompt configuration
        var searchContext = new Microsoft.KernelMemory.Context.RequestContext();
        
        // Set custom system prompt for search
        var customSystemPrompt = @"Facts:
{{$facts}}
======
You are a document search assistant. Provide DIRECT answers based only on the provided facts.

Guidelines:
- Maximum 2-3 sentences
- Only use information from the provided facts
- Be extremely concise
- Cite sources briefly
- If no relevant information found, state '{{$notFound}}'

Question: {{$input}}
Answer:";
        
        searchContext.SetArg("custom_rag_prompt_str", customSystemPrompt);
        
        // Optionally customize the fact template for better source attribution
        var customFactTemplate = @"[Source: {{$source}} | Relevance: {{$relevance}}]
{{$content}}
---";
        searchContext.SetArg("custom_rag_fact_template_str", customFactTemplate);

        // Use Kernel Memory's AskAsync to get AI-generated answer with sources
        var askResult = await memory.AskAsync(
            question: request.Query,
            filter: memoryFilter,
            minRelevance: request.MinRelevance,
            context: searchContext);

                var answer = askResult.Result;
                if (askResult.RelevantSources.Count == 0)
                {
                    answer = string.Format(AiPromptConstant.SemanticSearch.NoResultsPrompt, request.Query);
                }

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("🔍 [SEARCH-KM] AskAsync completed - RequestId: {RequestId}, HasAnswer: {HasAnswer}, SourceCount: {SourceCount}, ProcessingTime: {ProcessingTime}ms",
                    requestId, !string.IsNullOrEmpty(answer), askResult.RelevantSources?.Count ?? 0, processingTime);

                // Build response
                var response = new EnhancedSemanticSearchResponse
                {
                    RequestId = requestId,
                    Query = request.Query,
                    Answer = answer ?? string.Empty,
                    HasAnswer = !string.IsNullOrEmpty(answer),
                    Success = true,
                    ProcessingTimeMs = (long)processingTime,
                    TotalDocuments = askResult.RelevantSources?.Count ?? 0,
                    Metadata = new SearchMetadata
                    {
                        MinRelevance = request.MinRelevance,
                        MaxResults = request.MaxResults,
                        HybridScoringEnabled = request.EnableHybridScoring,
                        Scope = request.Scope.ToString(),
                        DepartmentFilter = filter.DepartmentId,
                        DocumentTypeFilter = filter.DocumentTypeId,
                        DateRange = new DateRangeFilter
                        {
                            FromDate = filter.FromDate,
                            ToDate = filter.ToDate,
                            EffectiveFrom = filter.EffectiveFrom,
                            EffectiveUntil = filter.EffectiveUntil
                        }
                    }
                };

                // Convert relevant sources to document responses
                if (askResult.RelevantSources?.Any() == true)
                {
                    response.RelevantDocuments = await ConvertSourcesToDocumentResponses(
                        askResult.RelevantSources, requestId);
                }

                _logger.LogInformation("✅ [SEARCH-KM] Search completed successfully - RequestId: {RequestId}, DocumentCount: {DocumentCount}",
                    requestId, response.RelevantDocuments.Count);

                return response;
            }
            catch (ArgumentException ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogWarning(ex, "❌ [SEARCH-KM] Invalid request - User: {UserId}, Query: '{Query}', Error: {Error}, ProcessingTime: {ProcessingTime}ms",
                    userId, request.Query, ex.Message, processingTime);

                return new EnhancedSemanticSearchResponse
                {
                    RequestId = requestId,
                    Query = request.Query,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTimeMs = (long)processingTime
                };
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "❌ [SEARCH-KM] Error during search - User: {UserId}, Query: '{Query}', Error: {Error}, ProcessingTime: {ProcessingTime}ms",
                    userId, request.Query, ex.Message, processingTime);

                return new EnhancedSemanticSearchResponse
                {
                    RequestId = requestId,
                    Query = request.Query,
                    Success = false,
                    ErrorMessage = "An error occurred while performing the search. Please try again.",
                    ProcessingTimeMs = (long)processingTime
                };
            }
        }

        /// <summary>
        /// Builds Kernel Memory filter based on user access control and search parameters
        /// </summary>
        private MemoryFilter BuildMemoryFilter(
            SemanticSearchRequest request,
            KernelMemorySearchFilter filter,
            string userId,
            string? userDepartmentId,
            string? userRole)
        {
            var memoryFilter = new MemoryFilter();

            // Always filter for approved documents only
            memoryFilter.ByTag("status", "approved");

            // Apply access control based on user's department and role
            if (userRole?.ToUpper() != "ADMIN")
            {
                // Users can access:
                // 1. Documents from their own department (if they have a department)
                // 2. Public documents from any department
                // 3. Documents they own

                var accessFilters = new List<MemoryFilter>();

                // Add department access if user has a department
                if (!string.IsNullOrEmpty(userDepartmentId))
                {
                    var deptFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("departmentId", userDepartmentId);
                    accessFilters.Add(deptFilter);
                }

                // Add public documents access
                var publicFilter = new MemoryFilter()
                    .ByTag("status", "approved")
                    .ByTag("isPublic", "True");
                accessFilters.Add(publicFilter);

                // Add owned documents access
                if (!string.IsNullOrEmpty(userId))
                {
                    var ownerFilter = new MemoryFilter()
                        .ByTag("status", "approved")
                        .ByTag("ownerId", userId);
                    accessFilters.Add(ownerFilter);
                }

                // For now, we'll use the department filter as primary and rely on post-processing
                // This is a limitation of current Kernel Memory filter capabilities
                if (!string.IsNullOrEmpty(userDepartmentId))
                {
                    memoryFilter.ByTag("departmentId", userDepartmentId);
                }
                else
                {
                    // If no department, only show public documents
                    memoryFilter.ByTag("isPublic", "True");
                }
            }

            // Apply additional filters
            if (!string.IsNullOrEmpty(filter.DepartmentId))
            {
                memoryFilter.ByTag("departmentId", filter.DepartmentId);
            }

            if (!string.IsNullOrEmpty(filter.DocumentTypeId))
            {
                memoryFilter.ByTag("documentType", filter.DocumentTypeId);
            }

            // Apply date filters if specified
            if (filter.EffectiveFrom.HasValue)
            {
                memoryFilter.ByTag("effectiveFrom", filter.EffectiveFrom.Value.ToString("yyyy-MM-dd"));
            }

            if (filter.EffectiveUntil.HasValue)
            {
                memoryFilter.ByTag("effectiveUntil", filter.EffectiveUntil.Value.ToString("yyyy-MM-dd"));
            }

            // Apply scope-based filtering
            switch (request.Scope)
            {
                case SearchScope.PublicOnly:
                    memoryFilter.ByTag("isPublic", "True");
                    break;
                case SearchScope.DepartmentOnly:
                    if (!string.IsNullOrEmpty(userDepartmentId))
                    {
                        memoryFilter.ByTag("departmentId", userDepartmentId);
                        memoryFilter.ByTag("isPublic", "False");
                    }
                    break;
                // SearchScope.All is default - no additional filtering
            }

            return memoryFilter;
        }

        /// <summary>
        /// Converts Kernel Memory sources to SemanticSearchResponse objects
        /// </summary>
        private Task<List<SemanticSearchResponse>> ConvertSourcesToDocumentResponses(
            IEnumerable<Citation> sources,
            string requestId)
        {
            var responses = new List<SemanticSearchResponse>();
            var rank = 1;

            foreach (var source in sources)
            {
                try
                {
                    var response = new SemanticSearchResponse
                    {
                        Id = GetTagValue(source, "documentId") ?? Guid.NewGuid().ToString(),
                        Title = GetTagValue(source, "title") ?? "Unknown Document",
                        DocumentName = GetTagValue(source, "title") ?? "Unknown Document",
                        Description = GetTagValue(source, "description") ?? string.Empty,
                        Status = GetTagValue(source, "status") ?? "approved",
                        DepartmentId = GetTagValue(source, "departmentId"),
                        DepartmentName = GetTagValue(source, "departmentName"),
                        CreatedBy = GetTagValue(source, "createdBy") ?? string.Empty,
                        CreatedByName = GetTagValue(source, "ownerName"),
                        FileType = GetTagValue(source, "fileType") ?? string.Empty,
                        Version = GetTagValue(source, "version") ?? "1.0",
                        DocumentTypeId = GetTagValue(source, "documentType") ?? string.Empty,
                        DocumentTypeName = GetTagValue(source, "documentTypeName"),
                        IsPublic = ParseBooleanTag(source, "isPublic"),
                        SignedBy = GetTagValue(source, "signedBy"),
                        Relevance = source.Partitions?.FirstOrDefault()?.Relevance ?? 0.0,
                        Rank = rank++
                    };

                    // Parse dates
                    if (DateTime.TryParse(GetTagValue(source, "createdTime"), out var createdTime))
                        response.CreatedTime = createdTime;

                    if (DateTime.TryParse(GetTagValue(source, "effectiveFrom"), out var effectiveFrom))
                        response.EffectiveFrom = effectiveFrom;

                    if (DateTime.TryParse(GetTagValue(source, "effectiveUntil"), out var effectiveUntil))
                        response.EffectiveUntil = effectiveUntil;

                    // Parse file size
                    if (long.TryParse(GetTagValue(source, "fileSize"), out var fileSize))
                        response.FileSize = fileSize;

                    // Parse tags - get all tag values
                    response.Tags = GetAllTagValues(source, "tags");

                    responses.Add(response);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [SEARCH-KM] Error converting source to response - RequestId: {RequestId}, SourceId: {SourceId}",
                        requestId, source.Link);
                }
            }

            return Task.FromResult(responses);
        }

        /// <summary>
        /// Gets a tag value from a Kernel Memory citation
        /// </summary>
        private string? GetTagValue(Citation citation, string tagKey)
        {
            var firstPartition = citation.Partitions?.FirstOrDefault();
            if (firstPartition?.Tags != null && firstPartition.Tags.TryGetValue(tagKey, out var values))
            {
                return values?.FirstOrDefault() ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets all tag values from a Kernel Memory citation for tags
        /// </summary>
        private List<string> GetAllTagValues(Citation citation, string tagKey)
        {
            var allTags = new List<string>();
            
            // Check all partitions for tags
            if (citation.Partitions != null)
            {
                foreach (var partition in citation.Partitions)
                {
                    if (partition.Tags != null && partition.Tags.TryGetValue(tagKey, out var values))
                    {
                        if (values != null)
                        {
                            foreach (var value in values)
                            {
                                if (!string.IsNullOrEmpty(value))
                                {
                                    // Try to parse as JSON array first
                                    try
                                    {
                                        var parsedTags = JsonSerializer.Deserialize<List<string>>(value);
                                        if (parsedTags != null)
                                        {
                                            allTags.AddRange(parsedTags);
                                        }
                                    }
                                    catch
                                    {
                                        // If not JSON, treat as single tag
                                        allTags.Add(value);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Remove duplicates and return
            return allTags.Distinct().ToList();
        }

        /// <summary>
        /// Parses a boolean tag value from a Kernel Memory citation
        /// </summary>
        private bool ParseBooleanTag(Citation citation, string tagKey)
        {
            var value = GetTagValue(citation, tagKey);
            return bool.TryParse(value, out var result) && result;
        }
    }
}
