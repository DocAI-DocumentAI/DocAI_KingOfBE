using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for enriching document responses with user and department names
/// </summary>
public interface IDocumentEnrichmentService
{
    /// <summary>
    /// Enrich a single document response with names
    /// </summary>
    Task<DocumentResponse> EnrichDocumentResponseAsync(DocumentResponse document);
    
    /// <summary>
    /// Enrich a single document draft response with names
    /// </summary>
    Task<DocumentDraftResponse> EnrichDocumentDraftResponseAsync(DocumentDraftResponse document);
    
    /// <summary>
    /// Enrich multiple document responses with names (bulk operation for better performance)
    /// </summary>
    Task<List<DocumentResponse>> EnrichDocumentResponsesAsync(List<DocumentResponse> documents);
    
    /// <summary>
    /// Enrich multiple document draft responses with names (bulk operation for better performance)
    /// </summary>
    Task<List<DocumentDraftResponse>> EnrichDocumentDraftResponsesAsync(List<DocumentDraftResponse> documents);

    /// <summary>
    /// Enriches a single SemanticSearchResponse object with user and department names.
    /// </summary>
    /// <param name="response">SemanticSearchResponse object to enrich</param>
    /// <returns>Enriched SemanticSearchResponse object</returns>
    Task<SemanticSearchResponse> EnrichSemanticSearchResponseAsync(SemanticSearchResponse response);

    /// <summary>
    /// Enriches a list of SemanticSearchResponse objects with user and department names in bulk.
    /// This method is optimized for performance by making bulk requests to the Auth service.
    /// </summary>
    /// <param name="responses">List of SemanticSearchResponse objects to enrich</param>
    /// <returns>List of enriched SemanticSearchResponse objects</returns>
    Task<List<SemanticSearchResponse>> EnrichSemanticSearchResponsesAsync(List<SemanticSearchResponse> responses);

    /// <summary>
    /// Enrich a single bookmark response with names
    /// </summary>
    Task<BookmarkResponse> EnrichBookmarkResponseAsync(BookmarkResponse bookmark);

    /// <summary>
    /// Enrich multiple bookmark responses with names (bulk operation for better performance)
    /// </summary>
    Task<List<BookmarkResponse>> EnrichBookmarkResponsesAsync(List<BookmarkResponse> bookmarks);

    /// <summary>
    /// Enrich a single tag response with names
    /// </summary>
    Task<TagResponse> EnrichTagResponseAsync(TagResponse tag);

    /// <summary>
    /// Enrich multiple tag responses with names (bulk operation for better performance)
    /// </summary>
    Task<List<TagResponse>> EnrichTagResponsesAsync(List<TagResponse> tags);

    /// <summary>
    /// Enrich a single pending document response with names
    /// </summary>
    Task<PendingDocumentResponse> EnrichPendingDocumentResponseAsync(PendingDocumentResponse pendingDocument);

    /// <summary>
    /// Enrich multiple pending document responses with names (bulk operation for better performance)
    /// </summary>
    Task<List<PendingDocumentResponse>> EnrichPendingDocumentResponsesAsync(List<PendingDocumentResponse> pendingDocuments);

    /// <summary>
    /// Enrich a single approval queue detail response with names
    /// </summary>
    Task<ApprovalQueueDetailResponse> EnrichApprovalQueueDetailResponseAsync(ApprovalQueueDetailResponse approvalDetail);
}
