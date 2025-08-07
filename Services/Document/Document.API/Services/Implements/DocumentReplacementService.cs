using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Shared.Exceptions;
using Document.API.Constants;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for generating document replacement suggestions using hybrid semantic + metadata approach
    /// </summary>
    public class DocumentReplacementService : IDocumentReplacementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentReplacementService> _logger;
        private readonly IRedisService _redisService;
        private readonly IDocumentEnrichmentService _enrichmentService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IKernelMemory _memory;

        // Cache configuration
        private const string CACHE_PREFIX = "doc_replacement_suggestions:";
        private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromMinutes(15); // Shorter than recommendations

        // Scoring weights for replacement suggestion algorithm
        private const double SEMANTIC_SIMILARITY_WEIGHT = 0.60;
        private const double METADATA_SIMILARITY_WEIGHT = 0.25;
        private const double CONTEXTUAL_WEIGHT = 0.15;

        // Sub-weights for semantic similarity
        private const double EMBEDDING_SIMILARITY_WEIGHT = 0.70;
        private const double TITLE_SIMILARITY_WEIGHT = 0.20;
        private const double DESCRIPTION_SIMILARITY_WEIGHT = 0.10;

        // Sub-weights for metadata similarity
        private const double DOCUMENT_TYPE_WEIGHT = 0.50;
        private const double TAG_SIMILARITY_WEIGHT = 0.35;
        private const double DEPARTMENT_COMPATIBILITY_WEIGHT = 0.15;

        // Sub-weights for contextual factors
        private const double RECENCY_WEIGHT = 0.60;
        private const double DEPARTMENT_BONUS_WEIGHT = 0.25;
        private const double STATUS_RELEVANCE_WEIGHT = 0.15;

        // Performance limits
        private const int MAX_CANDIDATES_TO_EVALUATE = 500;
        private const int MAX_SUGGESTIONS_RETURNED = 20;
        private const int PROCESSING_TIMEOUT_MS = 3000;

        public DocumentReplacementService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentReplacementService> logger,
            IRedisService redisService,
            IDocumentEnrichmentService enrichmentService,
            IHttpContextAccessor httpContextAccessor,
            IKernelMemory memory)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _redisService = redisService;
            _enrichmentService = enrichmentService;
            _httpContextAccessor = httpContextAccessor;
            _memory = memory;
        }

        public async Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsAsync(
            DocumentReplacementSuggestionRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var userDepartmentId = GetCurrentUserDepartmentId();

            _logger.LogInformation("Getting replacement suggestions for new document. Title: {Title}, Type: {DocumentTypeId}, User: {UserId}",
                request.Title, request.DocumentTypeId, userId);

            try
            {
                // Check cache first
                var cacheKey = GenerateCacheKey(request, userId, userDepartmentId);
                var cachedResult = await GetFromCacheAsync(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Returning cached replacement suggestions for user {UserId}", userId);
                    cachedResult.FromCache = true;
                    cachedResult.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return cachedResult;
                }

                // Generate suggestions
                var suggestions = await GenerateReplacementSuggestionsAsync(request, userDepartmentId, userId);

                // Create result
                var result = new DocumentReplacementSuggestionResponse
                {
                    Suggestions = suggestions,
                    TotalFound = suggestions.Count,
                    RequestedCount = request.MaxSuggestions,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    GeneratedAt = DateTime.UtcNow,
                    FromCache = false,
                    MinSimilarityThreshold = request.MinSimilarityThreshold
                };

                // Cache the result
                await SetCacheAsync(cacheKey, result);

                _logger.LogInformation("Generated {Count} replacement suggestions in {ElapsedMs}ms", 
                    suggestions.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating replacement suggestions for user {UserId}", userId);
                
                // Return empty result on error to prevent blocking document creation
                return new DocumentReplacementSuggestionResponse
                {
                    Suggestions = new List<DocumentReplacementCandidate>(),
                    TotalFound = 0,
                    RequestedCount = request.MaxSuggestions,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    GeneratedAt = DateTime.UtcNow,
                    FromCache = false,
                    MinSimilarityThreshold = request.MinSimilarityThreshold
                };
            }
        }

        public async Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsForEditAsync(
            string documentId,
            DocumentReplacementSuggestionRequest request)
        {
            _logger.LogInformation("Getting replacement suggestions for editing document {DocumentId}", documentId);

            // For editing, we exclude the current document from suggestions
            var result = await GetReplacementSuggestionsAsync(request);

            // Filter out the current document being edited
            result.Suggestions = result.Suggestions.Where(s => s.DocumentId != documentId).ToList();
            result.TotalFound = result.Suggestions.Count;

            return result;
        }

        public Task ClearReplacementCacheAsync(string documentTypeId, string departmentId)
        {
            try
            {
                // For now, we'll implement a simple approach
                // In production, you might want to use Redis pattern deletion
                _logger.LogInformation("Cache clear requested for document type {DocumentTypeId}, department {DepartmentId}",
                    documentTypeId, departmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing replacement cache for type {DocumentTypeId}, department {DepartmentId}",
                    documentTypeId, departmentId);
            }
            return Task.CompletedTask;
        }

        public Task ClearAllReplacementCachesAsync()
        {
            try
            {
                _logger.LogInformation("Clearing all replacement suggestion caches");
                // Implementation would depend on Redis capabilities
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all replacement caches");
            }
            return Task.CompletedTask;
        }

        public async Task<ReplacementSuggestionScoring> GetScoringBreakdownAsync(
            DocumentReplacementSuggestionRequest request,
            string candidateDocumentId)
        {
            _logger.LogInformation("Getting scoring breakdown for candidate {CandidateId}", candidateDocumentId);

            // Get the candidate document
            var candidate = await GetDocumentVersionAsync(candidateDocumentId);
            if (candidate == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Candidate document not found");
            }

            var userDepartmentId = GetCurrentUserDepartmentId();
            var scoring = await CalculateDetailedScoringAsync(request, candidate, userDepartmentId);

            return scoring;
        }

        public async Task<bool> CanReplaceDocumentAsync(string documentId)
        {
            try
            {
                var document = await GetDocumentVersionAsync(documentId);
                if (document == null)
                {
                    return false;
                }

                var userDepartmentId = GetCurrentUserDepartmentId();

                // Check replacement eligibility rules
                return CanUserReplaceDocument(document, userDepartmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking replacement eligibility for document {DocumentId}",
                    documentId);
                return false;
            }
        }

        public async Task PreWarmCacheAsync(List<string> documentTypeIds)
        {
            _logger.LogInformation("Pre-warming cache for {Count} document types", documentTypeIds.Count);

            try
            {
                foreach (var documentTypeId in documentTypeIds)
                {
                    // Get common departments for this document type
                    var allVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                        predicate: v => v.DocumentFile.DocumentTypeId == documentTypeId,
                        include: i => i.Include(v => v.DocumentFile));

                    var departments = allVersions
                        .Select(v => v.DocumentFile.DepartmentId)
                        .Distinct()
                        .Take(5) // Limit to top 5 departments
                        .ToList();

                    foreach (var departmentId in departments)
                    {
                        // Create a sample request for pre-warming
                        var sampleRequest = new DocumentReplacementSuggestionRequest
                        {
                            Title = "Sample Document",
                            Description = "Sample description for cache warming",
                            DocumentTypeId = documentTypeId,
                            Tags = new List<string> { "sample", "cache", "warming" },
                            IsPublic = false,
                            MaxSuggestions = 5,
                            MinSimilarityThreshold = 0.45
                        };

                        // Generate suggestions to warm the cache
                        await GetReplacementSuggestionsAsync(sampleRequest);

                        // Add a small delay to avoid overwhelming the system
                        await Task.Delay(100);
                    }
                }

                _logger.LogInformation("Cache pre-warming completed for {Count} document types", documentTypeIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache pre-warming");
            }
        }

        #region Helper Methods

        private async Task<List<DocumentReplacementCandidate>> GenerateReplacementSuggestionsAsync(
            DocumentReplacementSuggestionRequest request,
            string? userDepartmentId,
            string userId)
        {
            // Step 1: Get candidate documents with pre-filtering
            var candidates = await GetReplacementCandidatesAsync(request, userDepartmentId);

            if (!candidates.Any())
            {
                _logger.LogInformation("No replacement candidates found for document type {DocumentTypeId}", request.DocumentTypeId);
                return new List<DocumentReplacementCandidate>();
            }

            _logger.LogInformation("Found {Count} potential replacement candidates", candidates.Count);

            // Step 2: Calculate similarity scores for each candidate
            var scoredCandidates = await CalculateReplacementScoresAsync(request, candidates, userDepartmentId);

            // Step 3: Filter by minimum threshold and sort by score
            var filteredCandidates = scoredCandidates
                .Where(c => c.SimilarityScore >= request.MinSimilarityThreshold)
                .OrderByDescending(c => c.SimilarityScore)
                .Take(Math.Min(request.MaxSuggestions, MAX_SUGGESTIONS_RETURNED))
                .ToList();

            // Step 4: Enrich with additional information
            var enrichedCandidates = await EnrichReplacementCandidatesAsync(filteredCandidates);

            return enrichedCandidates;
        }

        private async Task<List<DocumentVersion>> GetReplacementCandidatesAsync(
            DocumentReplacementSuggestionRequest request,
            string? userDepartmentId)
        {
            // Build predicate for filtering
            var predicate = new Func<DocumentVersion, bool>(v =>
                // Must be same document type (required for replacement)
                v.DocumentFile.DocumentTypeId == request.DocumentTypeId &&
                // Must be approved documents only
                v.Status == StatusEnum.Approved &&
                // Must not be already replaced
                !v.DocumentFile.IsReplaced &&
                // Department access control
                (v.IsPublic ||
                 (!request.SameDepartmentOnly && (string.IsNullOrEmpty(userDepartmentId) || v.DocumentFile.DepartmentId == userDepartmentId)) ||
                 (request.SameDepartmentOnly && !string.IsNullOrEmpty(userDepartmentId) && v.DocumentFile.DepartmentId == userDepartmentId))
            );

            // Get candidates using GetListAsync
            var allCandidates = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                predicate: v => v.DocumentFile.DocumentTypeId == request.DocumentTypeId &&
                               v.Status == StatusEnum.Approved &&
                               !v.DocumentFile.IsReplaced,
                include: i => i.Include(v => v.DocumentFile)
                              .Include(v => v.DocumentTags)
                              .ThenInclude(dt => dt.Tag)
            );

            // Apply additional filtering and limit
            var filteredCandidates = allCandidates
                .Where(v =>
                    // Department access control
                    (v.IsPublic ||
                     (!request.SameDepartmentOnly && (string.IsNullOrEmpty(userDepartmentId) || v.DocumentFile.DepartmentId == userDepartmentId)) ||
                     (request.SameDepartmentOnly && !string.IsNullOrEmpty(userDepartmentId) && v.DocumentFile.DepartmentId == userDepartmentId))
                )
                .OrderByDescending(v => v.CreatedTime)
                .Take(MAX_CANDIDATES_TO_EVALUATE)
                .ToList();

            return filteredCandidates;
        }

        private async Task<List<DocumentReplacementCandidate>> CalculateReplacementScoresAsync(
            DocumentReplacementSuggestionRequest request,
            List<DocumentVersion> candidates,
            string? userDepartmentId)
        {
            var scoredCandidates = new List<DocumentReplacementCandidate>();
            var requestTags = request.Tags?.Select(t => t.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();

            // Generate embedding for the input document metadata
            var inputText = GenerateInputTextForEmbedding(request);
            var inputEmbedding = await GenerateEmbeddingAsync(inputText);

            // Process candidates in batches for better performance
            const int batchSize = 50;
            var batches = candidates.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(async candidate =>
                {
                    try
                    {
                        var candidateTags = candidate.DocumentTags.Select(dt => dt.Tag.Name.ToLowerInvariant()).ToHashSet();

                        // Calculate semantic similarity
                        var semanticScore = await CalculateSemanticSimilarityAsync(request, candidate, inputEmbedding);

                        // Calculate metadata similarity
                        var metadataScore = CalculateMetadataSimilarity(request, candidate, requestTags, candidateTags, userDepartmentId);

                        // Calculate contextual score
                        var contextScore = CalculateContextualScore(candidate, userDepartmentId);

                        // Combine weighted scores
                        var finalScore = (semanticScore * SEMANTIC_SIMILARITY_WEIGHT) +
                                       (metadataScore * METADATA_SIMILARITY_WEIGHT) +
                                       (contextScore * CONTEXTUAL_WEIGHT);

                        // Create reasons for the suggestion
                        var reasons = CreateReplacementReasons(request, candidate, requestTags, candidateTags, semanticScore, metadataScore, userDepartmentId);

                        // Check if user can replace this document
                        var canReplace = CanUserReplaceDocument(candidate, userDepartmentId);

                        var replacementCandidate = new DocumentReplacementCandidate
                        {
                            DocumentId = candidate.DocumentFile.Id,
                            Title = candidate.Title,
                            Description = candidate.DocumentFile.Description,
                            SimilarityScore = Math.Round(finalScore, 3),
                            SemanticScore = Math.Round(semanticScore, 3),
                            MetadataScore = Math.Round(metadataScore, 3),
                            ContextScore = Math.Round(contextScore, 3),
                            Reasons = reasons,
                            CanReplace = canReplace,
                            DepartmentId = candidate.DocumentFile.DepartmentId,
                            DocumentTypeId = candidate.DocumentFile.DocumentTypeId,
                            CreatedBy = candidate.DocumentFile.CreatedBy,
                            CreatedTime = candidate.CreatedTime,
                            LastUpdatedBy = candidate.LastUpdatedBy,
                            LastUpdatedTime = candidate.LastUpdatedTime,
                            Tags = candidateTags.ToList(),
                            SharedTagCount = requestTags.Intersect(candidateTags).Count(),
                            Status = candidate.Status.ToString(),
                            IsPublic = candidate.IsPublic,
                            FileSize = candidate.FileSize,
                            FileType = candidate.FileType,
                            EffectiveFrom = candidate.EffectiveFrom,
                            EffectiveUntil = candidate.EffectiveUntil,
                            SignedBy = candidate.SignedBy,
                            Summary = candidate.Summary
                        };

                        return replacementCandidate;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error calculating score for candidate document {DocumentId}", candidate.Id);
                        return null; // Return null for failed candidates
                    }
                });

                // Wait for batch to complete with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(PROCESSING_TIMEOUT_MS));
                try
                {
                    var batchResults = await Task.WhenAll(batchTasks);

                    // Add successful results to the collection
                    foreach (var result in batchResults.Where(r => r != null))
                    {
                        scoredCandidates.Add(result);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Batch processing timed out after {TimeoutMs}ms", PROCESSING_TIMEOUT_MS);
                    break; // Stop processing remaining batches
                }
            }

            return scoredCandidates;
        }

        private string GenerateInputTextForEmbedding(DocumentReplacementSuggestionRequest request)
        {
            var textBuilder = new StringBuilder();
            textBuilder.AppendLine(request.Title);

            if (!string.IsNullOrEmpty(request.Description))
            {
                textBuilder.AppendLine(request.Description);
            }

            if (request.Tags != null && request.Tags.Any())
            {
                textBuilder.AppendLine(string.Join(" ", request.Tags));
            }

            return textBuilder.ToString().Trim();
        }

        private async Task<double> GenerateEmbeddingAsync(string text)
        {
            try
            {
                // Use Kernel Memory to search for similar content and get relevance scores
                // This is a simplified approach - in production you might want to use direct embedding comparison
                var searchResult = await _memory.SearchAsync(
                    text,
                    limit: 1,
                    minRelevance: 0.0);

                if (searchResult?.Results?.Any() == true)
                {
                    // Return the highest relevance score as a proxy for embedding similarity
                    var firstResult = searchResult.Results.First();
                    return firstResult.Partitions.FirstOrDefault()?.Relevance ?? 0.0;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating embedding for text: {Text}", text.Substring(0, Math.Min(100, text.Length)));
                return 0.0;
            }
        }

        private async Task<double> CalculateSemanticSimilarityAsync(
            DocumentReplacementSuggestionRequest request,
            DocumentVersion candidate,
            double inputEmbedding)
        {
            try
            {
                // Use Kernel Memory to search for the candidate document using the input text
                var inputText = GenerateInputTextForEmbedding(request);

                // Create a filter to search only for this specific document
                var filter = new MemoryFilter().ByTag("documentId", candidate.DocumentFile.Id);

                var searchResult = await _memory.SearchAsync(
                    inputText,
                    limit: 1,
                    filter: filter,
                    minRelevance: 0.0);

                double embeddingSimilarity = 0.0;
                if (searchResult?.Results?.Any() == true)
                {
                    var firstResult = searchResult.Results.First();
                    embeddingSimilarity = firstResult.Partitions.FirstOrDefault()?.Relevance ?? 0.0;
                }

                // Calculate title similarity
                var titleSimilarity = CalculateStringSimilarity(request.Title, candidate.Title);

                // Calculate description similarity
                var descriptionSimilarity = CalculateStringSimilarity(
                    request.Description ?? "",
                    candidate.DocumentFile.Description ?? "");

                // Combine semantic scores with weights
                var semanticScore = (embeddingSimilarity * EMBEDDING_SIMILARITY_WEIGHT) +
                                  (titleSimilarity * TITLE_SIMILARITY_WEIGHT) +
                                  (descriptionSimilarity * DESCRIPTION_SIMILARITY_WEIGHT);

                return Math.Min(semanticScore, 1.0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating semantic similarity for document {DocumentId}", candidate.Id);

                // Fallback to text-based similarity if Kernel Memory fails
                var titleSimilarity = CalculateStringSimilarity(request.Title, candidate.Title);
                var descriptionSimilarity = CalculateStringSimilarity(
                    request.Description ?? "",
                    candidate.DocumentFile.Description ?? "");

                return (titleSimilarity * 0.7) + (descriptionSimilarity * 0.3);
            }
        }

        private string GenerateCandidateTextForEmbedding(DocumentVersion candidate)
        {
            var textBuilder = new StringBuilder();
            textBuilder.AppendLine(candidate.Title);

            if (!string.IsNullOrEmpty(candidate.DocumentFile.Description))
            {
                textBuilder.AppendLine(candidate.DocumentFile.Description);
            }

            if (candidate.DocumentTags != null && candidate.DocumentTags.Any())
            {
                var tags = candidate.DocumentTags.Select(dt => dt.Tag.Name);
                textBuilder.AppendLine(string.Join(" ", tags));
            }

            return textBuilder.ToString().Trim();
        }

        private double CalculateStringSimilarity(string str1, string str2)
        {
            if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
                return 1.0;

            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
                return 0.0;

            // Simple Jaccard similarity on words
            var words1 = str1.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var words2 = str2.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

            if (!words1.Any() && !words2.Any())
                return 1.0;

            if (!words1.Any() || !words2.Any())
                return 0.0;

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }

        private double CalculateMetadataSimilarity(
            DocumentReplacementSuggestionRequest request,
            DocumentVersion candidate,
            HashSet<string> requestTags,
            HashSet<string> candidateTags,
            string? userDepartmentId)
        {
            // Document type match (required for replacement, so always 1.0)
            var documentTypeMatch = 1.0;

            // Tag similarity using Jaccard coefficient
            var tagSimilarity = CalculateTagSimilarity(requestTags, candidateTags);

            // Department compatibility
            var departmentCompatibility = CalculateDepartmentCompatibility(userDepartmentId, candidate);

            // Combine metadata scores with weights
            var metadataScore = (documentTypeMatch * DOCUMENT_TYPE_WEIGHT) +
                              (tagSimilarity * TAG_SIMILARITY_WEIGHT) +
                              (departmentCompatibility * DEPARTMENT_COMPATIBILITY_WEIGHT);

            return Math.Min(metadataScore, 1.0);
        }

        private double CalculateTagSimilarity(HashSet<string> requestTags, HashSet<string> candidateTags)
        {
            if (!requestTags.Any() && !candidateTags.Any())
                return 1.0;

            if (!requestTags.Any() || !candidateTags.Any())
                return 0.0;

            var intersection = requestTags.Intersect(candidateTags).Count();
            var union = requestTags.Union(candidateTags).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }

        private double CalculateDepartmentCompatibility(string? userDepartmentId, DocumentVersion candidate)
        {
            // Public documents are always compatible
            if (candidate.IsPublic)
                return 1.0;

            // Same department gets full score
            if (!string.IsNullOrEmpty(userDepartmentId) && userDepartmentId == candidate.DocumentFile.DepartmentId)
                return 1.0;

            // Different department gets partial score
            return 0.3;
        }

        private double CalculateContextualScore(DocumentVersion candidate, string? userDepartmentId)
        {
            // Calculate recency score
            var recencyScore = CalculateRecencyScore(candidate.CreatedTime);

            // Calculate department bonus
            var departmentBonus = CalculateDepartmentBonus(candidate, userDepartmentId);

            // Calculate status relevance (approved documents get full score)
            var statusRelevance = candidate.Status == StatusEnum.Approved ? 1.0 : 0.5;

            // Combine contextual scores with weights
            var contextScore = (recencyScore * RECENCY_WEIGHT) +
                             (departmentBonus * DEPARTMENT_BONUS_WEIGHT) +
                             (statusRelevance * STATUS_RELEVANCE_WEIGHT);

            return Math.Min(contextScore, 1.0);
        }

        private double CalculateRecencyScore(DateTime createdTime)
        {
            var daysSinceCreation = (DateTime.UtcNow - createdTime).TotalDays;

            // Documents created within last 30 days get full score
            if (daysSinceCreation <= 30)
                return 1.0;

            // Score decreases linearly over 365 days
            if (daysSinceCreation >= 365)
                return 0.1; // Minimum score for very old documents

            return 1.0 - ((daysSinceCreation - 30) / 335 * 0.9);
        }

        private double CalculateDepartmentBonus(DocumentVersion candidate, string? userDepartmentId)
        {
            if (string.IsNullOrEmpty(userDepartmentId))
                return 0.0;

            return candidate.DocumentFile.DepartmentId == userDepartmentId ? 1.0 : 0.0;
        }

        private List<string> CreateReplacementReasons(
            DocumentReplacementSuggestionRequest request,
            DocumentVersion candidate,
            HashSet<string> requestTags,
            HashSet<string> candidateTags,
            double semanticScore,
            double metadataScore,
            string? userDepartmentId)
        {
            var reasons = new List<string>();

            // Semantic similarity reasons
            if (semanticScore >= 0.8)
                reasons.Add("High content similarity (80%+)");
            else if (semanticScore >= 0.6)
                reasons.Add("Good content similarity (60%+)");
            else if (semanticScore >= 0.4)
                reasons.Add("Moderate content similarity (40%+)");

            // Document type (always same for replacement candidates)
            reasons.Add("Same document type");

            // Tag similarity
            var sharedTags = requestTags.Intersect(candidateTags).Count();
            if (sharedTags > 0)
            {
                reasons.Add($"{sharedTags} shared tag{(sharedTags > 1 ? "s" : "")}");
            }

            // Department compatibility
            if (!string.IsNullOrEmpty(userDepartmentId) && userDepartmentId == candidate.DocumentFile.DepartmentId)
            {
                reasons.Add("Same department");
            }
            else if (candidate.IsPublic)
            {
                reasons.Add("Public document");
            }

            // Recency
            var daysSinceCreation = (DateTime.UtcNow - candidate.CreatedTime).TotalDays;
            if (daysSinceCreation <= 30)
            {
                reasons.Add("Recently created");
            }
            else if (daysSinceCreation <= 90)
            {
                reasons.Add("Created within 3 months");
            }

            return reasons.Any() ? reasons : new List<string> { "Similar document characteristics" };
        }

        private bool CanUserReplaceDocument(DocumentVersion candidate, string? userDepartmentId)
        {
            // User can replace if:
            // 1. Document is public, OR
            // 2. Document is in the same department as the user
            return candidate.IsPublic ||
                   (!string.IsNullOrEmpty(userDepartmentId) && candidate.DocumentFile.DepartmentId == userDepartmentId);
        }

        private async Task<List<DocumentReplacementCandidate>> EnrichReplacementCandidatesAsync(
            List<DocumentReplacementCandidate> candidates)
        {
            try
            {
                // Use the enrichment service to add department names, user names, etc.
                return await _enrichmentService.EnrichDocumentReplacementCandidatesAsync(candidates);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enriching replacement candidates");
                return candidates;
            }
        }

        private async Task<ReplacementSuggestionScoring> CalculateDetailedScoringAsync(
            DocumentReplacementSuggestionRequest request,
            DocumentVersion candidate,
            string? userDepartmentId)
        {
            var requestTags = request.Tags?.Select(t => t.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            var candidateTags = candidate.DocumentTags.Select(dt => dt.Tag.Name.ToLowerInvariant()).ToHashSet();

            // Generate input embedding
            var inputText = GenerateInputTextForEmbedding(request);
            var inputEmbedding = await GenerateEmbeddingAsync(inputText);

            // Calculate detailed scores
            var embeddingSimilarity = 0.5; // Placeholder
            var titleSimilarity = CalculateStringSimilarity(request.Title, candidate.Title);
            var descriptionSimilarity = CalculateStringSimilarity(request.Description ?? "", candidate.DocumentFile.Description ?? "");

            var documentTypeMatch = 1.0; // Always 1.0 for replacement candidates
            var tagSimilarity = CalculateTagSimilarity(requestTags, candidateTags);
            var departmentCompatibility = CalculateDepartmentCompatibility(userDepartmentId, candidate);

            var recencyScore = CalculateRecencyScore(candidate.CreatedTime);
            var departmentBonus = CalculateDepartmentBonus(candidate, userDepartmentId);
            var statusRelevance = candidate.Status == StatusEnum.Approved ? 1.0 : 0.5;

            // Calculate weighted scores
            var semanticScore = (embeddingSimilarity * EMBEDDING_SIMILARITY_WEIGHT) +
                              (titleSimilarity * TITLE_SIMILARITY_WEIGHT) +
                              (descriptionSimilarity * DESCRIPTION_SIMILARITY_WEIGHT);

            var metadataScore = (documentTypeMatch * DOCUMENT_TYPE_WEIGHT) +
                              (tagSimilarity * TAG_SIMILARITY_WEIGHT) +
                              (departmentCompatibility * DEPARTMENT_COMPATIBILITY_WEIGHT);

            var contextScore = (recencyScore * RECENCY_WEIGHT) +
                             (departmentBonus * DEPARTMENT_BONUS_WEIGHT) +
                             (statusRelevance * STATUS_RELEVANCE_WEIGHT);

            var weightedSemantic = semanticScore * SEMANTIC_SIMILARITY_WEIGHT;
            var weightedMetadata = metadataScore * METADATA_SIMILARITY_WEIGHT;
            var weightedContext = contextScore * CONTEXTUAL_WEIGHT;
            var finalScore = weightedSemantic + weightedMetadata + weightedContext;

            return new ReplacementSuggestionScoring
            {
                Semantic = new SemanticSimilarityScores
                {
                    EmbeddingSimilarity = Math.Round(embeddingSimilarity, 3),
                    TitleSimilarity = Math.Round(titleSimilarity, 3),
                    DescriptionSimilarity = Math.Round(descriptionSimilarity, 3)
                },
                Metadata = new MetadataSimilarityScores
                {
                    DocumentTypeMatch = Math.Round(documentTypeMatch, 3),
                    TagSimilarity = Math.Round(tagSimilarity, 3),
                    DepartmentCompatibility = Math.Round(departmentCompatibility, 3)
                },
                Context = new ContextualScores
                {
                    RecencyScore = Math.Round(recencyScore, 3),
                    DepartmentBonus = Math.Round(departmentBonus, 3),
                    StatusRelevance = Math.Round(statusRelevance, 3)
                },
                Final = new WeightedScores
                {
                    WeightedSemantic = Math.Round(weightedSemantic, 3),
                    WeightedMetadata = Math.Round(weightedMetadata, 3),
                    WeightedContext = Math.Round(weightedContext, 3),
                    FinalScore = Math.Round(finalScore, 3)
                }
            };
        }

        #endregion

        #region Utility Methods

        private string GenerateCacheKey(DocumentReplacementSuggestionRequest request, string userId, string? departmentId)
        {
            // Create a hash of the request parameters for cache key
            var keyData = $"{request.Title}:{request.Description}:{request.DocumentTypeId}:{departmentId}:" +
                         $"{string.Join(",", request.Tags ?? new List<string>())}:{request.IsPublic}:" +
                         $"{request.MaxSuggestions}:{request.MinSimilarityThreshold}:{request.SameDepartmentOnly}:{userId}";

            var hash = keyData.GetHashCode();
            return $"{CACHE_PREFIX}{Math.Abs(hash)}";
        }

        private async Task<DocumentReplacementSuggestionResponse?> GetFromCacheAsync(string cacheKey)
        {
            try
            {
                var cachedJson = await _redisService.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    return JsonSerializer.Deserialize<DocumentReplacementSuggestionResponse>(cachedJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from cache for key {CacheKey}", cacheKey);
            }
            return null;
        }

        private async Task SetCacheAsync(string cacheKey, DocumentReplacementSuggestionResponse result)
        {
            try
            {
                var json = JsonSerializer.Serialize(result);
                await _redisService.SetStringAsync(cacheKey, json, CACHE_EXPIRY);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error setting cache for key {CacheKey}", cacheKey);
            }
        }

        private async Task<DocumentVersion?> GetDocumentVersionAsync(string documentId)
        {
            try
            {
                var versions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                    predicate: v => v.DocumentFile.Id == documentId,
                    include: i => i.Include(v => v.DocumentFile)
                                  .Include(v => v.DocumentTags)
                                  .ThenInclude(dt => dt.Tag));

                return versions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document version for document {DocumentId}", documentId);
                return null;
            }
        }

        private string? GetCurrentUserDepartmentId()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    return httpContext.User.FindFirst("departmentId")?.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting current user department ID");
            }
            return null;
        }

        /// <summary>
        /// Gets the current user's ID from JWT token
        /// </summary>
        /// <returns>User ID</returns>
        private string GetCurrentUserId()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            var userIdClaim = user?.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID not found in token");
            return userIdClaim;
        }

        #endregion
    }

    /// <summary>
    /// Internal class for scoring documents during replacement suggestion calculation
    /// </summary>
    internal class ScoredReplacementCandidate
    {
        public DocumentVersion Document { get; set; } = null!;
        public double SimilarityScore { get; set; }
        public double SemanticScore { get; set; }
        public double MetadataScore { get; set; }
        public double ContextScore { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public bool CanReplace { get; set; }
        public int SharedTagCount { get; set; }
    }
}
