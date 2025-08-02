using Document.API.Payload.Response;
using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for enriching document responses with user and department names
/// </summary>
public class DocumentEnrichmentService : IDocumentEnrichmentService
{
    private readonly INameLookupService _nameLookupService;
    private readonly ILogger<DocumentEnrichmentService> _logger;

    public DocumentEnrichmentService(
        INameLookupService nameLookupService,
        ILogger<DocumentEnrichmentService> logger)
    {
        _nameLookupService = nameLookupService;
        _logger = logger;
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
            // Collect all unique user and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.CreatedBy))
                    userIds.Add(doc.CreatedBy);
                if (!string.IsNullOrEmpty(doc.LastUpdatedby))
                    userIds.Add(doc.LastUpdatedby);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );

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
            // Collect all unique user and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.OwnerId))
                    userIds.Add(doc.OwnerId);
                if (!string.IsNullOrEmpty(doc.SubmittedBy))
                    userIds.Add(doc.SubmittedBy);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );

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
            // Collect all unique user and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            foreach (var response in responses)
            {
                if (!string.IsNullOrEmpty(response.CreatedBy))
                    userIds.Add(response.CreatedBy);
                if (!string.IsNullOrEmpty(response.LastUpdatedby))
                    userIds.Add(response.LastUpdatedby);
                if (!string.IsNullOrEmpty(response.DepartmentId))
                    departmentIds.Add(response.DepartmentId);
            }

            // Get names in bulk
            var names = await _nameLookupService.GetNamesAsync(userIds.ToList(), departmentIds.ToList());

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
            // Collect all unique user and department IDs
            var userIds = new HashSet<string>();
            var departmentIds = new HashSet<string>();

            foreach (var doc in pendingDocuments)
            {
                if (!string.IsNullOrEmpty(doc.SubmittedBy))
                    userIds.Add(doc.SubmittedBy);
                if (!string.IsNullOrEmpty(doc.DepartmentId))
                    departmentIds.Add(doc.DepartmentId);
            }

            // Bulk lookup names
            var nameResponse = await _nameLookupService.GetNamesAsync(
                userIds.ToList(),
                departmentIds.ToList()
            );

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
            // Collect all unique department IDs
            var departmentIds = recommendations
                .Where(r => !string.IsNullOrEmpty(r.DepartmentId))
                .Select(r => r.DepartmentId)
                .Distinct()
                .ToList();

            if (!departmentIds.Any())
            {
                _logger.LogInformation("No department IDs found in recommendations to enrich");
                return recommendations;
            }

            // Get names from the lookup service
            var names = await _nameLookupService.GetNamesAsync(new List<string>(), departmentIds);

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
}
