using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for enriching document responses with user and department names
/// </summary>
public class DocumentEnrichmentService : IDocumentEnrichmentService
{
    private readonly INameLookupService _nameLookupService;
    private readonly ILogger<DocumentEnrichmentService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;

    public DocumentEnrichmentService(
        INameLookupService nameLookupService,
        ILogger<DocumentEnrichmentService> logger,
        IUnitOfWork unitOfWork,
        IMemoryCache cache)
    {
        _nameLookupService = nameLookupService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<DocumentResponse> EnrichDocumentResponseAsync(DocumentResponse document)
    {
        if (document == null) return document;

        var documents = await EnrichDocumentResponsesAsync(new List<DocumentResponse> { document });
        return documents.FirstOrDefault() ?? document;
    }

    public async Task<DocumentDraftResponse> EnrichDocumentDraftResponseAsync(DocumentDraftResponse document)
    {
        if (document == null) return document;

        var documents = await EnrichDocumentDraftResponsesAsync(new List<DocumentDraftResponse> { document });
        return documents.FirstOrDefault() ?? document;
    }

    public async Task<List<DocumentResponse>> EnrichDocumentResponsesAsync(List<DocumentResponse> documents)
    {
        if (!documents.Any()) return documents;

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.CreatedBy))
                    userIds.Add(doc.CreatedBy);
                if (!string.IsNullOrEmpty(doc.LastUpdatedby))
                    userIds.Add(doc.LastUpdatedby);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
                if (!string.IsNullOrEmpty(doc.DocumentTypeId))
                    documentTypeIds.Add(doc.DocumentTypeId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each document with names
                foreach (var doc in documents)
                {
                    if (!string.IsNullOrEmpty(doc.CreatedBy) &&
                        nameResponse.UserNames.TryGetValue(doc.CreatedBy, out string? createdByName))
                    {
                        doc.CreatedByName = createdByName;
                    }

                    if (!string.IsNullOrEmpty(doc.LastUpdatedby) &&
                        nameResponse.UserNames.TryGetValue(doc.LastUpdatedby, out string? updatedByName))
                    {
                        doc.LastUpdatedByName = updatedByName;
                    }

                    if (!string.IsNullOrEmpty(doc.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(doc.DepartmentId, out string? deptName))
                    {
                        doc.DepartmentName = deptName;
                    }

                    if (!string.IsNullOrEmpty(doc.DocumentTypeId) &&
                        documentTypeNames.TryGetValue(doc.DocumentTypeId, out string? docTypeName))
                    {
                        doc.DocumentTypeName = docTypeName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich document responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching document responses with names");
            return documents; // Return original documents if enrichment fails
        }
    }

    public async Task<List<DocumentDraftResponse>> EnrichDocumentDraftResponsesAsync(List<DocumentDraftResponse> documents)
    {
        if (!documents.Any()) return documents;

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.OwnerId))
                    userIds.Add(doc.OwnerId);
                if (!string.IsNullOrEmpty(doc.SubmittedBy))
                    userIds.Add(doc.SubmittedBy);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
                if (!string.IsNullOrEmpty(doc.DocumentTypeId))
                    documentTypeIds.Add(doc.DocumentTypeId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each document with names
                foreach (var doc in documents)
                {
                    if (!string.IsNullOrEmpty(doc.OwnerId) &&
                        nameResponse.UserNames.TryGetValue(doc.OwnerId, out string? ownerName))
                    {
                        doc.OwnerName = ownerName;
                    }

                    if (!string.IsNullOrEmpty(doc.SubmittedBy) &&
                        nameResponse.UserNames.TryGetValue(doc.SubmittedBy, out string? submittedByName))
                    {
                        doc.SubmittedByName = submittedByName;
                    }

                    if (!string.IsNullOrEmpty(doc.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(doc.DepartmentId, out string? deptName))
                    {
                        doc.DepartmentName = deptName;
                    }

                    if (!string.IsNullOrEmpty(doc.DocumentTypeId) &&
                        documentTypeNames.TryGetValue(doc.DocumentTypeId, out string? docTypeName))
                    {
                        doc.DocumentTypeName = docTypeName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich document draft responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching document draft responses with names");
            return documents; // Return original documents if enrichment fails
        }
    }

    public async Task<SemanticSearchResponse> EnrichSemanticSearchResponseAsync(SemanticSearchResponse response)
    {
        if (response == null) return response;

        var enrichedResponses = await EnrichSemanticSearchResponsesAsync(new List<SemanticSearchResponse> { response });
        return enrichedResponses.FirstOrDefault() ?? response;
    }

    public async Task<List<SemanticSearchResponse>> EnrichSemanticSearchResponsesAsync(List<SemanticSearchResponse> responses)
    {
        if (responses == null || !responses.Any())
        {
            return responses ?? new List<SemanticSearchResponse>();
        }

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var response in responses)
            {
                if (!string.IsNullOrEmpty(response.CreatedBy))
                    userIds.Add(response.CreatedBy);
                if (!string.IsNullOrEmpty(response.LastUpdatedby))
                    userIds.Add(response.LastUpdatedby);
                if (!string.IsNullOrEmpty(response.DepartmentId))
                    departmentIds.Add(response.DepartmentId);
                if (!string.IsNullOrEmpty(response.DocumentTypeId))
                    documentTypeIds.Add(response.DocumentTypeId);
            }

            // Get names in bulk
            var names = await _nameLookupService.GetNamesAsync(userIds.ToList(), departmentIds.ToList());
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            // Enrich each response
            var enrichedResponses = new List<SemanticSearchResponse>();
            foreach (var response in responses)
            {
                var enrichedResponse = response; // Reference copy for efficiency

                // Enrich user names
                if (!string.IsNullOrEmpty(response.CreatedBy))
                {
                    enrichedResponse.CreatedByName = names.UserNames.GetValueOrDefault(response.CreatedBy);
                }
                if (!string.IsNullOrEmpty(response.LastUpdatedby))
                {
                    enrichedResponse.LastUpdatedByName = names.UserNames.GetValueOrDefault(response.LastUpdatedby);
                }

                // Enrich department name
                if (!string.IsNullOrEmpty(response.DepartmentId))
                {
                    enrichedResponse.DepartmentName = names.DepartmentNames.GetValueOrDefault(response.DepartmentId);
                }

                // Enrich document type name
                if (!string.IsNullOrEmpty(response.DocumentTypeId))
                {
                    enrichedResponse.DocumentTypeName = documentTypeNames.GetValueOrDefault(response.DocumentTypeId);
                }

                enrichedResponses.Add(enrichedResponse);
            }

            _logger.LogInformation("Successfully enriched {Count} semantic search responses with names", enrichedResponses.Count);
            return enrichedResponses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich semantic search responses with names. Returning original responses.");
            return responses;
        }
    }

    public async Task<BookmarkResponse> EnrichBookmarkResponseAsync(BookmarkResponse bookmark)
    {
        if (bookmark == null) return bookmark;

        var bookmarks = await EnrichBookmarkResponsesAsync(new List<BookmarkResponse> { bookmark });
        return bookmarks.FirstOrDefault() ?? bookmark;
    }

    public async Task<List<BookmarkResponse>> EnrichBookmarkResponsesAsync(List<BookmarkResponse> bookmarks)
    {
        if (bookmarks == null || !bookmarks.Any())
        {
            return bookmarks ?? new List<BookmarkResponse>();
        }

        try
        {
            // Collect all unique user IDs
            var userIds = new HashSet<string>();

            foreach (var bookmark in bookmarks)
            {
                if (!string.IsNullOrEmpty(bookmark.OwnerId))
                    userIds.Add(bookmark.OwnerId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                new List<string>()
            );

            if (nameResponse.Success)
            {
                // Enrich each bookmark with names
                foreach (var bookmark in bookmarks)
                {
                    if (!string.IsNullOrEmpty(bookmark.OwnerId) &&
                        nameResponse.UserNames.TryGetValue(bookmark.OwnerId, out string? ownerName))
                    {
                        bookmark.OwnerName = ownerName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich bookmark responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return bookmarks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching bookmark responses with names");
            return bookmarks; // Return original bookmarks if enrichment fails
        }
    }

    public async Task<TagResponse> EnrichTagResponseAsync(TagResponse tag)
    {
        if (tag == null) return tag;

        var tags = await EnrichTagResponsesAsync(new List<TagResponse> { tag });
        return tags.FirstOrDefault() ?? tag;
    }

    public async Task<List<TagResponse>> EnrichTagResponsesAsync(List<TagResponse> tags)
    {
        if (tags == null || !tags.Any())
        {
            return tags ?? new List<TagResponse>();
        }

        try
        {
            // Collect all unique user IDs
            var userIds = new HashSet<string>();

            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag.CreatedBy))
                    userIds.Add(tag.CreatedBy);
                if (!string.IsNullOrEmpty(tag.LastUpdatedBy))
                    userIds.Add(tag.LastUpdatedBy);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                new List<string>()
            );

            if (nameResponse.Success)
            {
                // Enrich each tag with names
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrEmpty(tag.CreatedBy) &&
                        nameResponse.UserNames.TryGetValue(tag.CreatedBy, out string? createdByName))
                    {
                        tag.CreatedByName = createdByName;
                    }

                    if (!string.IsNullOrEmpty(tag.LastUpdatedBy) &&
                        nameResponse.UserNames.TryGetValue(tag.LastUpdatedBy, out string? updatedByName))
                    {
                        tag.LastUpdatedByName = updatedByName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich tag responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching tag responses with names");
            return tags; // Return original tags if enrichment fails
        }
    }

    public async Task<PendingDocumentResponse> EnrichPendingDocumentResponseAsync(PendingDocumentResponse pendingDocument)
    {
        if (pendingDocument == null) return pendingDocument;

        var pendingDocuments = await EnrichPendingDocumentResponsesAsync(new List<PendingDocumentResponse> { pendingDocument });
        return pendingDocuments.FirstOrDefault() ?? pendingDocument;
    }

    public async Task<List<PendingDocumentResponse>> EnrichPendingDocumentResponsesAsync(List<PendingDocumentResponse> pendingDocuments)
    {
        if (pendingDocuments == null || !pendingDocuments.Any())
        {
            return pendingDocuments ?? new List<PendingDocumentResponse>();
        }

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var doc in pendingDocuments)
            {
                if (!string.IsNullOrEmpty(doc.SubmittedBy))
                    userIds.Add(doc.SubmittedBy);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
                if (!string.IsNullOrEmpty(doc.DocumentTypeId))
                    documentTypeIds.Add(doc.DocumentTypeId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each pending document with names
                foreach (var doc in pendingDocuments)
                {
                    if (!string.IsNullOrEmpty(doc.SubmittedBy) &&
                        nameResponse.UserNames.TryGetValue(doc.SubmittedBy, out string? submittedByName))
                    {
                        doc.SubmittedByName = submittedByName;
                    }

                    if (!string.IsNullOrEmpty(doc.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(doc.DepartmentId, out string? deptName))
                    {
                        doc.DepartmentName = deptName;
                    }

                    if (!string.IsNullOrEmpty(doc.DocumentTypeId) &&
                        documentTypeNames.TryGetValue(doc.DocumentTypeId, out string? docTypeName))
                    {
                        doc.DocumentTypeName = docTypeName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich pending document responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return pendingDocuments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching pending document responses with names");
            return pendingDocuments; // Return original documents if enrichment fails
        }
    }

    public async Task<ApprovalQueueDetailResponse> EnrichApprovalQueueDetailResponseAsync(ApprovalQueueDetailResponse approvalDetail)
    {
        if (approvalDetail == null) return approvalDetail;

        try
        {
            // Collect all unique user and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            if (!string.IsNullOrEmpty(approvalDetail.OwnerId))
                userIds.Add(approvalDetail.OwnerId);
            if (!string.IsNullOrEmpty(approvalDetail.SubmittedBy))
                userIds.Add(approvalDetail.SubmittedBy);
            if (!string.IsNullOrEmpty(approvalDetail.ClaimedBy))
                userIds.Add(approvalDetail.ClaimedBy);
            if (!string.IsNullOrEmpty(approvalDetail.DepartmentId))
                departmentIds.Add(approvalDetail.DepartmentId);

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );

            if (nameResponse.Success)
            {
                // Enrich approval detail with names
                if (!string.IsNullOrEmpty(approvalDetail.OwnerId) &&
                    nameResponse.UserNames.TryGetValue(approvalDetail.OwnerId, out string? ownerName))
                {
                    approvalDetail.OwnerName = ownerName;
                }

                if (!string.IsNullOrEmpty(approvalDetail.SubmittedBy) &&
                    nameResponse.UserNames.TryGetValue(approvalDetail.SubmittedBy, out string? submittedByName))
                {
                    approvalDetail.SubmittedByName = submittedByName;
                }

                if (!string.IsNullOrEmpty(approvalDetail.ClaimedBy) &&
                    nameResponse.UserNames.TryGetValue(approvalDetail.ClaimedBy, out string? claimedByName))
                {
                    approvalDetail.ClaimedByName = claimedByName;
                }

                if (!string.IsNullOrEmpty(approvalDetail.DepartmentId) &&
                    nameResponse.DepartmentNames.TryGetValue(approvalDetail.DepartmentId, out string? deptName))
                {
                    approvalDetail.DepartmentName = deptName;
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich approval queue detail response with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return approvalDetail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching approval queue detail response with names");
            return approvalDetail; // Return original response if enrichment fails
        }
    }
    
    public async Task<List<DocumentRecommendationResponse>> EnrichDocumentRecommendationsAsync(List<DocumentRecommendationResponse> recommendations)
    {
        if (!recommendations.Any()) return recommendations;

        try
        {
            // Collect all unique department and document type IDs
            var departmentIds = recommendations
                .Where(r => !string.IsNullOrEmpty(r.DepartmentId))
                .Select(r => r.DepartmentId)
                .Distinct()
                .ToList();

            var documentTypeIds = recommendations
                .Where(r => !string.IsNullOrEmpty(r.DocumentTypeId))
                .Select(r => r.DocumentTypeId)
                .Distinct()
                .ToList();

            if (!departmentIds.Any() && !documentTypeIds.Any())
            {
                _logger.LogInformation("No department or document type IDs found in recommendations to enrich");
                return recommendations;
            }

            // Get names from the lookup service and document type service
            var names = await _nameLookupService.GetNamesAsync(new List<string>(), departmentIds);
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds);

            // Enrich each recommendation
            var enrichedRecommendations = new List<DocumentRecommendationResponse>();
            foreach (var recommendation in recommendations)
            {
                var enrichedRecommendation = new DocumentRecommendationResponse
                {
                    DocumentId = recommendation.DocumentId,
                    Title = recommendation.Title,
                    Description = recommendation.Description,
                    DocumentTypeId = recommendation.DocumentTypeId,
                    DocumentTypeName = recommendation.DocumentTypeName,
                    DepartmentId = recommendation.DepartmentId,
                    IsPublic = recommendation.IsPublic,
                    CreatedTime = recommendation.CreatedTime,
                    Tags = recommendation.Tags,
                    RelevanceScore = recommendation.RelevanceScore,
                    RecommendationReason = recommendation.RecommendationReason,
                    SharedTagCount = recommendation.SharedTagCount,
                    LatestVersionId = recommendation.LatestVersionId
                };

                // Enrich with department name
                if (!string.IsNullOrEmpty(recommendation.DepartmentId))
                {
                    enrichedRecommendation.DepartmentName = names.DepartmentNames.GetValueOrDefault(recommendation.DepartmentId);
                }

                // Enrich with document type name
                if (!string.IsNullOrEmpty(recommendation.DocumentTypeId))
                {
                    enrichedRecommendation.DocumentTypeName = documentTypeNames.GetValueOrDefault(recommendation.DocumentTypeId);
                }

                enrichedRecommendations.Add(enrichedRecommendation);
            }

            _logger.LogInformation("Successfully enriched {Count} document recommendations with names", enrichedRecommendations.Count);
            return enrichedRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich document recommendations with names. Returning original recommendations.");
            return recommendations;
        }
    }

    public async Task<DocumentVersionResponse> EnrichDocumentVersionResponseAsync(DocumentVersionResponse documentVersion)
    {
        if (documentVersion == null) return documentVersion;

        var documentVersions = await EnrichDocumentVersionResponsesAsync(new List<DocumentVersionResponse> { documentVersion });
        return documentVersions.FirstOrDefault() ?? documentVersion;
    }

    public async Task<List<DocumentVersionResponse>> EnrichDocumentVersionResponsesAsync(List<DocumentVersionResponse> documentVersions)
    {
        if (!documentVersions.Any()) return documentVersions;

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var doc in documentVersions)
            {
                if (!string.IsNullOrEmpty(doc.OwnerId))
                    userIds.Add(doc.OwnerId);
                if (!string.IsNullOrEmpty(doc.SubmittedBy))
                    userIds.Add(doc.SubmittedBy);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
                if (!string.IsNullOrEmpty(doc.DocumentTypeId))
                    documentTypeIds.Add(doc.DocumentTypeId);
            }

            if (!userIds.Any() && !departmentIds.Any() && !documentTypeIds.Any())
            {
                _logger.LogInformation("No user, department, or document type IDs found in document versions to enrich");
                return documentVersions;
            }

            // Get names from the lookup service and document type service
            var nameResponse = await _nameLookupService.GetNamesAsync(userIds.ToList(), departmentIds.ToList());
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each document version with names
                foreach (var doc in documentVersions)
                {
                    if (!string.IsNullOrEmpty(doc.OwnerId) &&
                        nameResponse.UserNames.TryGetValue(doc.OwnerId, out string? ownerName))
                    {
                        doc.OwnerName = ownerName;
                    }

                    if (!string.IsNullOrEmpty(doc.SubmittedBy) &&
                        nameResponse.UserNames.TryGetValue(doc.SubmittedBy, out string? submittedByName))
                    {
                        doc.SubmittedByName = submittedByName;
                    }

                    if (!string.IsNullOrEmpty(doc.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(doc.DepartmentId, out string? deptName))
                    {
                        doc.DepartmentName = deptName;
                    }

                    if (!string.IsNullOrEmpty(doc.DocumentTypeId) &&
                        documentTypeNames.TryGetValue(doc.DocumentTypeId, out string? docTypeName))
                    {
                        doc.DocumentTypeName = docTypeName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich document version responses with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return documentVersions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching document version responses with names");
            return documentVersions; // Return original documents if enrichment fails
        }
    }

    public async Task<DocumentReplacementCandidate> EnrichDocumentReplacementCandidateAsync(DocumentReplacementCandidate candidate)
    {
        if (candidate == null) return candidate;

        var candidates = await EnrichDocumentReplacementCandidatesAsync(new List<DocumentReplacementCandidate> { candidate });
        return candidates.FirstOrDefault() ?? candidate;
    }

    public async Task<List<DocumentReplacementCandidate>> EnrichDocumentReplacementCandidatesAsync(List<DocumentReplacementCandidate> candidates)
    {
        if (!candidates.Any()) return candidates;

        try
        {
            // Collect all unique user, department, and document type IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();
            var documentTypeIds = new HashSet<string>();

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.CreatedBy))
                    userIds.Add(candidate.CreatedBy);
                if (!string.IsNullOrEmpty(candidate.LastUpdatedBy))
                    userIds.Add(candidate.LastUpdatedBy);
                if (!string.IsNullOrEmpty(candidate.DepartmentId))
                    departmentIds.Add(candidate.DepartmentId);
                if (!string.IsNullOrEmpty(candidate.DocumentTypeId))
                    documentTypeIds.Add(candidate.DocumentTypeId);
            }

            if (!userIds.Any() && !departmentIds.Any() && !documentTypeIds.Any())
            {
                _logger.LogInformation("No user, department, or document type IDs found in replacement candidates to enrich");
                return candidates;
            }

            // Get names from the lookup service and document type service
            var nameResponse = await _nameLookupService.GetNamesAsync(userIds.ToList(), departmentIds.ToList());
            var documentTypeNames = await GetDocumentTypeNamesAsync(documentTypeIds.ToList());

            if (nameResponse.Success)
            {
                // Enrich each candidate with names
                foreach (var candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate.CreatedBy) &&
                        nameResponse.UserNames.TryGetValue(candidate.CreatedBy, out string? createdByName))
                    {
                        candidate.CreatedByName = createdByName;
                    }

                    if (!string.IsNullOrEmpty(candidate.LastUpdatedBy) &&
                        nameResponse.UserNames.TryGetValue(candidate.LastUpdatedBy, out string? lastUpdatedByName))
                    {
                        candidate.LastUpdatedByName = lastUpdatedByName;
                    }

                    if (!string.IsNullOrEmpty(candidate.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(candidate.DepartmentId, out string? deptName))
                    {
                        candidate.DepartmentName = deptName;
                    }

                    if (!string.IsNullOrEmpty(candidate.DocumentTypeId) &&
                        documentTypeNames.TryGetValue(candidate.DocumentTypeId, out string? docTypeName))
                    {
                        candidate.DocumentTypeName = docTypeName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich replacement candidates with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return candidates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching replacement candidates with names");
            return candidates; // Return original candidates if enrichment fails
        }
    }

    public async Task<DocumentSourceResponse> EnrichDocumentSourceResponseAsync(DocumentSourceResponse source)
    {
        if (source == null) return source;

        var sources = await EnrichDocumentSourceResponsesAsync(new List<DocumentSourceResponse> { source });
        return sources.FirstOrDefault() ?? source;
    }

    public async Task<List<DocumentSourceResponse>> EnrichDocumentSourceResponsesAsync(List<DocumentSourceResponse> sources)
    {
        if (!sources.Any()) return sources;

        try
        {
            // Collect all unique department IDs
            var departmentIds = sources
                .Where(s => !string.IsNullOrEmpty(s.DepartmentId))
                .Select(s => s.DepartmentId)
                .Distinct()
                .ToList();

            if (!departmentIds.Any())
            {
                _logger.LogInformation("No department IDs found in document sources to enrich");
                return sources;
            }

            // Get names from the lookup service
            var nameResponse = await _nameLookupService.GetNamesAsync(new List<string>(), departmentIds);

            if (nameResponse.Success)
            {
                // Enrich each source with names
                foreach (var source in sources)
                {
                    if (!string.IsNullOrEmpty(source.DepartmentId) &&
                        nameResponse.DepartmentNames.TryGetValue(source.DepartmentId, out string? deptName))
                    {
                        source.DepartmentName = deptName;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to enrich document sources with names: {ErrorMessage}",
                    nameResponse.ErrorMessage);
            }

            return sources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching document sources with names");
            return sources; // Return original sources if enrichment fails
        }
    }

    /// <summary>
    /// Get document type names for the provided document type IDs
    /// Uses caching for optimal performance
    /// </summary>
    /// <param name="documentTypeIds">List of document type IDs to lookup</param>
    /// <returns>Dictionary mapping document type IDs to names</returns>
    private async Task<Dictionary<string, string>> GetDocumentTypeNamesAsync(List<string> documentTypeIds)
    {
        var result = new Dictionary<string, string>();
        var uncachedIds = new List<string>();

        // Check cache first
        foreach (var id in documentTypeIds.Where(id => !string.IsNullOrEmpty(id)))
        {
            var cacheKey = $"doctype_name_{id}";
            if (_cache.TryGetValue(cacheKey, out string? cachedName) && cachedName != null)
            {
                result[id] = cachedName;
            }
            else
            {
                uncachedIds.Add(id);
            }
        }

        // Get uncached names from database
        if (uncachedIds.Any())
        {
            try
            {
                var documentTypes = await _unitOfWork.GetRepository<DocumentType>()
                    .GetListAsync(
                        predicate: dt => uncachedIds.Contains(dt.Id) && dt.DeletedTime == null,
                        selector: dt => new { dt.Id, dt.Name }
                    );

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    SlidingExpiration = TimeSpan.FromMinutes(15)
                };

                foreach (var docType in documentTypes)
                {
                    var cacheKey = $"doctype_name_{docType.Id}";
                    _cache.Set(cacheKey, docType.Name, cacheOptions);
                    result[docType.Id] = docType.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document type names for IDs: {DocumentTypeIds}",
                    string.Join(", ", uncachedIds));
            }
        }

        return result;
    }
}
