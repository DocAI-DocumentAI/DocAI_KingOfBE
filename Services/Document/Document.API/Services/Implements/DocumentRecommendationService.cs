using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using System.Text.Json;
using Document.API.Constants;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for generating document recommendations using Document Type + Tag Hybrid approach
    /// </summary>
    public class DocumentRecommendationService : IDocumentRecommendationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentRecommendationService> _logger;
        private readonly IRedisService _redisService;
        private readonly IDocumentEnrichmentService _enrichmentService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Cache configuration
        private const string CACHE_PREFIX = "doc_recommendations:";
        private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromMinutes(30);

        // Scoring weights for recommendation algorithm
        private const double TAG_SIMILARITY_WEIGHT = 0.60;
        private const double RECENCY_WEIGHT = 0.25;
        private const double DEPARTMENT_BONUS_WEIGHT = 0.15;

        public DocumentRecommendationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentRecommendationService> logger,
            IRedisService redisService,
            IDocumentEnrichmentService enrichmentService,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _redisService = redisService;
            _enrichmentService = enrichmentService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<DocumentRecommendationsResult> GetRecommendationsAsync(
            string documentId, 
            DocumentRecommendationRequest request, 
            string userId)
        {
            _logger.LogInformation("Getting recommendations for document {DocumentId}, user {UserId}, count {Count}", 
                documentId, userId, request.Count);

            // Check cache first
            var cacheKey = $"{CACHE_PREFIX}{documentId}:{userId}:{request.Count}:{request.IncludeSameDepartmentOnly}:{request.DocumentTypeFilter}";
            var cachedResult = await GetFromCacheAsync(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogInformation("Returning cached recommendations for document {DocumentId}", documentId);
                cachedResult.FromCache = true;
                return cachedResult;
            }

            // Get source document
            var sourceDocument = await GetSourceDocumentAsync(documentId);
            if (sourceDocument == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Source document not found");
            }

            // Get user department for access control
            var userDepartmentId = GetCurrentUserDepartmentId();

            // Generate recommendations
            var recommendations = await GenerateRecommendationsAsync(sourceDocument, userDepartmentId, request);

            // Create result
            var result = new DocumentRecommendationsResult
            {
                SourceDocumentId = documentId,
                SourceDocumentTitle = sourceDocument.Title,
                Recommendations = recommendations,
                TotalFound = recommendations.Count,
                RequestedCount = request.Count,
                GeneratedAt = DateTime.UtcNow,
                FromCache = false
            };

            // Cache the result
            await SetCacheAsync(cacheKey, result);

            _logger.LogInformation("Generated {Count} recommendations for document {DocumentId}", 
                recommendations.Count, documentId);

            return result;
        }

        public async Task ClearRecommendationCacheAsync(string documentId)
        {
            try
            {
                // Clear all cache entries for this document
                var pattern = $"{CACHE_PREFIX}{documentId}:*";
                // Note: Redis pattern deletion would require server access
                // For now, we'll implement a simple approach
                _logger.LogInformation("Cache clear requested for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing recommendation cache for document {DocumentId}", documentId);
            }
        }

        private async Task<DocumentVersion?> GetSourceDocumentAsync(string documentId)
        {
            return await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                predicate: v => v.DocumentFile.Id == documentId && v.IsOfficial,
                include: i => i.Include(v => v.DocumentFile)
                    .ThenInclude(df => df.DocumentType)
                    .Include(v => v.DocumentTags)
                    .ThenInclude(dt => dt.Tag)
            );
        }

        private string? GetCurrentUserDepartmentId()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            return user?.FindFirst("departmentId")?.Value;
        }

        private async Task<List<DocumentRecommendationResponse>> GenerateRecommendationsAsync(
            DocumentVersion sourceDocument,
            string? userDepartmentId,
            DocumentRecommendationRequest request)
        {
            // Step 1: Get candidate documents (same document type + access control)
            var candidates = await GetCandidateDocumentsAsync(sourceDocument, userDepartmentId, request);

            if (!candidates.Any())
            {
                _logger.LogInformation("No candidate documents found for recommendations");
                return new List<DocumentRecommendationResponse>();
            }

            // Step 2: Calculate scores for each candidate
            var scoredCandidates = CalculateRecommendationScores(sourceDocument, candidates, userDepartmentId);

            // Step 3: Sort by score and take top N
            var topRecommendations = scoredCandidates
                .OrderByDescending(c => c.Score)
                .Take(request.Count)
                .ToList();

            // Step 4: Convert to response models
            var recommendations = topRecommendations.Select(c => CreateRecommendationResponse(c)).ToList();

            // Step 5: Enrich with department names
            var enrichedRecommendations = await _enrichmentService.EnrichDocumentRecommendationsAsync(recommendations);

            return enrichedRecommendations;
        }

        private async Task<List<DocumentVersion>> GetCandidateDocumentsAsync(
            DocumentVersion sourceDocument,
            string? userDepartmentId,
            DocumentRecommendationRequest request)
        {
            // Build predicate for filtering
            var predicate = BuildCandidatePredicate(sourceDocument, userDepartmentId, request);

            // Get candidates using repository method
            var allCandidates = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                predicate: predicate,
                include: i => i.Include(v => v.DocumentFile)
                    .ThenInclude(df => df.DocumentType)
                    .Include(v => v.DocumentTags)
                    .ThenInclude(dt => dt.Tag),
                orderBy: q => q.OrderByDescending(v => v.CreatedTime)
            );

            // Limit to prevent performance issues
            var candidates = allCandidates.Take(100).ToList();

            _logger.LogInformation("Found {Count} candidate documents for recommendations", candidates.Count);
            return candidates;
        }

        private System.Linq.Expressions.Expression<Func<DocumentVersion, bool>> BuildCandidatePredicate(
            DocumentVersion sourceDocument,
            string? userDepartmentId,
            DocumentRecommendationRequest request)
        {
            return v =>
                // Primary filters: same document type, official status, not the source document
                v.DocumentFile.DocumentTypeId == sourceDocument.DocumentFile.DocumentTypeId &&
                v.IsOfficial &&
                v.DocumentFile.Id != sourceDocument.DocumentFile.Id &&
                // Access control: respect isPublic and department restrictions
                (string.IsNullOrEmpty(userDepartmentId) ? v.IsPublic : (v.IsPublic || v.DocumentFile.DepartmentId == userDepartmentId)) &&
                // Optional: same department only filter
                (!request.IncludeSameDepartmentOnly || string.IsNullOrEmpty(userDepartmentId) || v.DocumentFile.DepartmentId == userDepartmentId) &&
                // Optional: specific document type filter
                (string.IsNullOrEmpty(request.DocumentTypeFilter) || v.DocumentFile.DocumentTypeId == request.DocumentTypeFilter);
        }

        private List<ScoredDocument> CalculateRecommendationScores(
            DocumentVersion sourceDocument,
            List<DocumentVersion> candidates,
            string? userDepartmentId)
        {
            var sourceTags = sourceDocument.DocumentTags.Select(dt => dt.Tag.Name.ToLowerInvariant()).ToHashSet();
            var scoredDocuments = new List<ScoredDocument>();

            foreach (var candidate in candidates)
            {
                var candidateTags = candidate.DocumentTags.Select(dt => dt.Tag.Name.ToLowerInvariant()).ToHashSet();

                // Calculate tag similarity score
                var tagSimilarityScore = CalculateTagSimilarity(sourceTags, candidateTags);

                // Calculate recency score (newer documents get higher scores)
                var recencyScore = CalculateRecencyScore(candidate.CreatedTime);

                // Calculate department bonus (same department gets bonus)
                var departmentBonus = CalculateDepartmentBonus(candidate, userDepartmentId);

                // Combine weighted scores
                var finalScore = (tagSimilarityScore * TAG_SIMILARITY_WEIGHT) +
                               (recencyScore * RECENCY_WEIGHT) +
                               (departmentBonus * DEPARTMENT_BONUS_WEIGHT);

                // Create recommendation reason
                var reason = CreateRecommendationReason(sourceTags, candidateTags, candidate, userDepartmentId);

                scoredDocuments.Add(new ScoredDocument
                {
                    Document = candidate,
                    Score = finalScore,
                    TagSimilarityScore = tagSimilarityScore,
                    RecencyScore = recencyScore,
                    DepartmentBonus = departmentBonus,
                    SharedTagCount = sourceTags.Intersect(candidateTags).Count(),
                    RecommendationReason = reason
                });
            }

            return scoredDocuments;
        }

        private double CalculateTagSimilarity(HashSet<string> sourceTags, HashSet<string> candidateTags)
        {
            if (!sourceTags.Any() || !candidateTags.Any())
            {
                return 0.0;
            }

            var intersection = sourceTags.Intersect(candidateTags).Count();
            var union = sourceTags.Union(candidateTags).Count();

            // Jaccard similarity coefficient
            return union > 0 ? (double)intersection / union : 0.0;
        }

        private double CalculateRecencyScore(DateTime createdTime)
        {
            var daysSinceCreation = (DateTime.UtcNow - createdTime).TotalDays;

            // Documents created within last 30 days get full score
            // Score decreases linearly over 365 days
            if (daysSinceCreation <= 30)
                return 1.0;

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

        private string CreateRecommendationReason(
            HashSet<string> sourceTags,
            HashSet<string> candidateTags,
            DocumentVersion candidate,
            string? userDepartmentId)
        {
            var sharedTags = sourceTags.Intersect(candidateTags).Count();
            var reasons = new List<string>();

            if (sharedTags > 0)
            {
                reasons.Add($"{sharedTags} shared tag{(sharedTags > 1 ? "s" : "")}");
            }

            if (!string.IsNullOrEmpty(userDepartmentId) && candidate.DocumentFile.DepartmentId == userDepartmentId)
            {
                reasons.Add("same department");
            }

            reasons.Add("same document type");

            return reasons.Any() ? string.Join(", ", reasons) : "similar document";
        }

        private DocumentRecommendationResponse CreateRecommendationResponse(ScoredDocument scoredDocument)
        {
            var document = scoredDocument.Document;

            return new DocumentRecommendationResponse
            {
                DocumentId = document.DocumentFile.Id,
                Title = document.Title,
                Description = document.DocumentFile.Description,
                DocumentTypeId = document.DocumentFile.DocumentTypeId,
                DocumentTypeName = document.DocumentFile.DocumentType?.Name,
                DepartmentId = document.DocumentFile.DepartmentId,
                IsPublic = document.IsPublic,
                CreatedTime = document.CreatedTime,
                Tags = document.DocumentTags.Select(dt => dt.Tag.Name).ToList(),
                RelevanceScore = Math.Round(scoredDocument.Score, 3),
                RecommendationReason = scoredDocument.RecommendationReason,
                SharedTagCount = scoredDocument.SharedTagCount,
                LatestVersionId = document.Id.ToString()
            };
        }

        private async Task<DocumentRecommendationsResult?> GetFromCacheAsync(string cacheKey)
        {
            try
            {
                var cachedJson = await _redisService.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    return JsonSerializer.Deserialize<DocumentRecommendationsResult>(cachedJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from cache for key {CacheKey}", cacheKey);
            }
            return null;
        }

        private async Task SetCacheAsync(string cacheKey, DocumentRecommendationsResult result)
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
    }

    /// <summary>
    /// Internal class for scoring documents during recommendation calculation
    /// </summary>
    internal class ScoredDocument
    {
        public DocumentVersion Document { get; set; } = null!;
        public double Score { get; set; }
        public double TagSimilarityScore { get; set; }
        public double RecencyScore { get; set; }
        public double DepartmentBonus { get; set; }
        public int SharedTagCount { get; set; }
        public string RecommendationReason { get; set; } = string.Empty;
    }
}
