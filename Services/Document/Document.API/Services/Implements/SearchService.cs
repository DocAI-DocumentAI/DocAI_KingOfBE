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
        
        // Set custom system prompt for search with markdown formatting
        var customSystemPrompt = @"Facts:
{{$facts}}
======
You are a document search assistant. Provide DIRECT answers based only on the provided facts in **MARKDOWN FORMAT**.

Guidelines:
- Format response in clean markdown
- Use headers (##) for main topics
- Use bullet points (-) for lists
- Use **bold** for key information
- Use *italics* for emphasis
- Maximum 2-3 sentences per section
- Only use information from the provided facts
- If no relevant information found, state '{{$notFound}}'

Question: {{$input}}

Answer (in markdown format):";
        
        searchContext.SetArg("custom_rag_prompt_str", customSystemPrompt);
        
        // Optionally customize the fact template for better markdown source attribution
        var customFactTemplate = @"**[Source: {{$source}}]** *(Relevance: {{$relevance}})*
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
                    var allDocuments = await ConvertSourcesToDocumentResponses(
                        askResult.RelevantSources, requestId);
                    
                    // Apply post-processing access control filter
                    response.RelevantDocuments = FilterDocumentsByAccess(
                        allDocuments, userId, userDepartmentId, userRole);
                }
                else
                {
                    response.RelevantDocuments = new List<SemanticSearchResponse>();
                }

                // Update total documents count after filtering
                response.TotalDocuments = response.RelevantDocuments.Count;

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
            memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.Status, "approved");

            // Apply access control based on user's department and role
            if (userRole?.ToUpper() != "ADMIN")
            {
                // For non-admin users, we need to create an OR logic:
                // 1. Documents from their own department (if they have a department)
                // 2. Public documents from ANY department
                // 3. Documents they own
                
                // Since Kernel Memory doesn't support OR logic in filters,
                // we'll use a more permissive approach and filter by:
                // - Public documents from all departments OR
                // - All documents from user's department
                
                // Apply scope-based filtering first
                switch (request.Scope)
                {
                    case SearchScope.PublicOnly:
                        // Only public documents from all departments
                        memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.IsPublic, "True");
                        break;
                    case SearchScope.DepartmentOnly:
                        // Only documents from user's department (both public and private)
                        if (!string.IsNullOrEmpty(userDepartmentId))
                        {
                            memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DepartmentId, userDepartmentId);
                        }
                        break;
                    case SearchScope.All:
                    default:
                        // For "All" scope, we don't add additional department restrictions
                        // Let the search find documents from all departments
                        // The post-processing will handle access control
                        break;
                }
            }

            // Apply additional filters
            if (!string.IsNullOrEmpty(filter.DepartmentId))
            {
                memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DepartmentId, filter.DepartmentId);
            }

            if (!string.IsNullOrEmpty(filter.DocumentTypeId))
            {
                memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DocumentType, filter.DocumentTypeId);
            }

            // Apply date filters if specified
            if (filter.EffectiveFrom.HasValue)
            {
                memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.EffectiveFrom, filter.EffectiveFrom.Value.ToString("yyyy-MM-dd"));
            }

            if (filter.EffectiveUntil.HasValue)
            {
                memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.EffectiveUntil, filter.EffectiveUntil.Value.ToString("yyyy-MM-dd"));
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
                        Id = GetTagValue(source, SemanticSearchConstant.MemoryTags.DocumentId) ?? Guid.NewGuid().ToString(),
                        Title = GetTagValue(source, SemanticSearchConstant.MemoryTags.Title) ?? "Unknown Document",
                        DocumentName = GetTagValue(source, SemanticSearchConstant.MemoryTags.Title) ?? "Unknown Document",
                        Description = GetTagValue(source, SemanticSearchConstant.MemoryTags.Description) ?? string.Empty,
                        Status = GetTagValue(source, SemanticSearchConstant.MemoryTags.Status) ?? "approved",
                        DepartmentId = GetTagValue(source, SemanticSearchConstant.MemoryTags.DepartmentId),
                        DepartmentName = GetTagValue(source, SemanticSearchConstant.MemoryTags.DepartmentName),
                        CreatedBy = GetTagValue(source, SemanticSearchConstant.MemoryTags.CreatedBy) ?? string.Empty,
                        CreatedByName = GetTagValue(source, SemanticSearchConstant.MemoryTags.OwnerName),
                        FileType = GetTagValue(source, SemanticSearchConstant.MemoryTags.FileType) ?? string.Empty,
                        Version = GetTagValue(source, SemanticSearchConstant.MemoryTags.Version) ?? "1.0",
                        DocumentTypeId = GetTagValue(source, SemanticSearchConstant.MemoryTags.DocumentType) ?? string.Empty,
                        DocumentTypeName = GetTagValue(source, SemanticSearchConstant.MemoryTags.DocumentTypeName),
                        IsPublic = ParseBooleanTag(source, SemanticSearchConstant.MemoryTags.IsPublic),
                        SignedBy = GetTagValue(source, SemanticSearchConstant.MemoryTags.SignedBy),
                        Relevance = source.Partitions?.FirstOrDefault()?.Relevance ?? 0.0,
                        Rank = rank++
                    };

                    // Parse dates
                    // Note: For now, we don't have a specific CreatedTime tag, so we skip this parsing
                    // if (DateTime.TryParse(GetTagValue(source, "createdTime"), out var createdTime))
                    //     response.CreatedTime = createdTime;

                    if (DateTime.TryParse(GetTagValue(source, SemanticSearchConstant.MemoryTags.EffectiveFrom), out var effectiveFrom))
                        response.EffectiveFrom = effectiveFrom;

                    if (DateTime.TryParse(GetTagValue(source, SemanticSearchConstant.MemoryTags.EffectiveUntil), out var effectiveUntil))
                        response.EffectiveUntil = effectiveUntil;

                    // Parse file size
                    if (long.TryParse(GetTagValue(source, SemanticSearchConstant.MemoryTags.FileSize), out var fileSize))
                        response.FileSize = fileSize;

                    // Parse tags - get all tag values
                    response.Tags = GetAllTagValues(source, SemanticSearchConstant.MemoryTags.Tags);

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
        /// Filters documents based on user access control
        /// </summary>
        private List<SemanticSearchResponse> FilterDocumentsByAccess(
            List<SemanticSearchResponse> documents,
            string userId,
            string? userDepartmentId,
            string? userRole)
        {
            // Admin users can see all documents
            if (userRole?.ToUpper() == "ADMIN")
            {
                return documents;
            }

            var accessibleDocuments = new List<SemanticSearchResponse>();

            foreach (var doc in documents)
            {
                bool hasAccess = false;

                // Check if document is public (accessible to all users)
                if (doc.IsPublic)
                {
                    hasAccess = true;
                }
                // Check if document is from user's department
                else if (!string.IsNullOrEmpty(userDepartmentId) && 
                         doc.DepartmentId == userDepartmentId)
                {
                    hasAccess = true;
                }
                // Check if user owns the document
                else if (!string.IsNullOrEmpty(userId) && 
                         doc.CreatedBy == userId)
                {
                    hasAccess = true;
                }

                if (hasAccess)
                {
                    accessibleDocuments.Add(doc);
                }
            }

            return accessibleDocuments;
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
