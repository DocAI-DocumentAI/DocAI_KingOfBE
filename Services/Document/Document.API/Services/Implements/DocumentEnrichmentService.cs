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
}
