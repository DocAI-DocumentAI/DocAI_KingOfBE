using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers;

[Route(ApiEndPointConstant.ApiEndpoint)]
[ApiController]
[CustomAuthorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentRecommendationService _recommendationService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, IDocumentRecommendationService recommendationService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a new document draft for review and approval
    /// </summary> 
    /// <param name="request">Document draft creation request with file and metadata</param>
    /// <returns>Created document draft information</returns>
    [HttpPost(ApiEndPointConstant.Document.UploadDraft)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocumentDraft([FromForm] CreateDraftRequest request)
    {
        var result = await _documentService.CreateDraftAsync(request);
        return Ok(ApiResponse<object>.Success(result, "Document draft uploaded successfully", 201));
    }

    /// <summary>
    /// Analyze a document file to extract metadata and content information
    /// </summary>
    /// <param name="file">Document file to analyze</param>
    /// <returns>Analysis results including extracted text and metadata</returns>
    [HttpPost(ApiEndPointConstant.Document.AnalyzeDocument)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<AnalyzeDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnayzeDocumentDraft(IFormFile file)
    {
        var result = await _documentService.AnalyzeDocumentAsync(file);
        return Ok(ApiResponse<object>.Success(result, "Analyze result", 200));
    }

    /// <summary>
    /// Generate enhanced structured summary for a document file during the creation process
    /// </summary>
    /// <param name="file">Document file to generate enhanced summary for</param>
    /// <returns>Enhanced structured summary with token usage information</returns>
    [HttpPost(ApiEndPointConstant.Document.RegenerateSummary)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<RegenerateSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RegenerateDocumentSummary(IFormFile file)
    {
        var result = await _documentService.RegenerateSummaryAsync(file);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<RegenerateSummaryResponse>.Error("SUMMARY_GENERATION_FAILED", result.ErrorMessage, 400));
        }

        return Ok(ApiResponse<RegenerateSummaryResponse>.Success(result, "Enhanced summary generated successfully", 200));
    }

    /// <summary>
    /// Update an existing document draft with new content or metadata
    /// </summary>
    /// <param name="documentId">The ID of the draft document to update</param>
    /// <param name="request">Updated document draft information</param>
    /// <returns>Updated document draft details</returns>
    [HttpPut(ApiEndPointConstant.Document.EditDraft)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EditDraft([FromRoute(Name = "id")] string documentId, [FromForm] UpdateDocumentDraftRequest request)
    {
        var result = await _documentService.UpdateDraftAsync(documentId, request);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Delete a document draft permanently
    /// </summary>
    /// <param name="documentId">The ID of the document to delete</param>
    /// <param name="versionId">The version ID of the draft to delete</param>
    /// <returns>Success confirmation</returns>
    [HttpDelete(ApiEndPointConstant.Document.DeleteDraft)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDocument([FromRoute(Name = "id")] string documentId, string versionId)
    {
        await _documentService.DeleteDraftAsync(documentId, versionId);
        return Ok(ApiResponse<object>.Success(null, "Document deleted successfully", 200));
    }

    /// <summary>
    /// Get details of an approved/official document by ID
    /// </summary>
    /// <param name="documentFileId">The ID of the official document to retrieve</param>
    /// <returns>Official document details and metadata</returns>
    [HttpGet(ApiEndPointConstant.Document.GetOfficialDocument)]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOfficialDocument([FromRoute(Name = "id")] string documentFileId)
    {
        var result = await _documentService.GetOfficialDocumentAsync(documentFileId);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get all approved/official documents with comprehensive filtering and pagination.
    /// Use DepartmentOnly=true to show only documents from user's department (both public and private).
    /// Use DepartmentOnly=false to show public documents from all departments.
    /// </summary>
    [HttpGet(ApiEndPointConstant.Document.GetAllOfficialDocuments)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllOfficialDocuments([FromQuery] OfficialDocumentsFilterRequest filterRequest, int pageNumber = 1, int pageSize = 10)
    {
        // Map request to filter
        var filter = new OfficialDocumentsFilter
        {
            Title = filterRequest.Title,
            Keyword = filterRequest.Keyword,
            VersionName = filterRequest.VersionName,
            FromDate = filterRequest.FromDate,
            ToDate = filterRequest.ToDate,
            EffectiveFrom = filterRequest.EffectiveFrom,
            EffectiveUntil = filterRequest.EffectiveUntil,
            LastSubmittedFrom = filterRequest.LastSubmittedFrom,
            LastSubmittedTo = filterRequest.LastSubmittedTo,
            DocumentTypeId = filterRequest.DocumentTypeId,
            Tags = filterRequest.Tags,
            SignedBy = filterRequest.SignedBy,
            FileType = filterRequest.FileType,
            SubmittedBy = filterRequest.SubmittedBy,
            IsPublic = filterRequest.IsPublic,
            MinFileSize = filterRequest.MinFileSize,
            MaxFileSize = filterRequest.MaxFileSize,
            MinDownloads = filterRequest.MinDownloads,
            MaxDownloads = filterRequest.MaxDownloads,
            DepartmentOnly = filterRequest.DepartmentOnly
        };

        // Use the new filtered method
        var filteredResult = await _documentService.GetAllOfficialDocumentsAsync(filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(filteredResult));
    }

    /// <summary>
    /// Get current user's document drafts with filtering and pagination
    /// </summary>
    /// <param name="filter">Filter criteria for documents</param>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 10)</param>
    /// <returns>Paginated list of user's document drafts</returns>
    [HttpGet(ApiEndPointConstant.Document.GetMyDocuments)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyDocuments([FromQuery] MyDocumentsFilter filter, int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetMyDocumentsAsync(filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get detailed information of a specific user's document draft
    /// </summary>
    /// <param name="versionId">The version ID of the document draft</param>
    /// <returns>Detailed document draft information</returns>
    [HttpGet(ApiEndPointConstant.Document.GetMyDocumentDetail)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyDocumentDetail([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetMyDocumentByIdAsync(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get all document drafts with pagination (for admin/manager view)
    /// </summary>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 10)</param>
    /// <returns>Paginated list of all document drafts</returns>
    [HttpGet(ApiEndPointConstant.Document.GetDrafts)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDrafts(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetDraftsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get a specific document draft by its version ID
    /// </summary>
    /// <param name="versionId">The version ID of the document draft</param>
    /// <returns>Document draft details</returns>
    [HttpGet(ApiEndPointConstant.Document.GetDraftById)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDraftById([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetDraftByIdAsync(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get all rejected documents with pagination
    /// </summary>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 10)</param>
    /// <returns>Paginated list of rejected documents</returns>
    [HttpGet(ApiEndPointConstant.Document.GetRejectedDocuments)]
    [CustomAuthorize(Roles = new[] { Roles.Editor, Roles.Manager })]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRejectedDocuments(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetRejectDocumentsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get a specific rejected document by its version ID
    /// </summary>
    /// <param name="versionId">The version ID of the rejected document</param>
    /// <returns>Rejected document details</returns>
    [HttpGet(ApiEndPointConstant.Document.GetRejectedById)]
    [CustomAuthorize(Roles = new[] { Roles.Editor, Roles.Manager })]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRejectedById([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetRejectedById(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Create a new version of an existing document
    /// </summary>
    /// <param name="documentId">The ID of the document to create a new version for</param>
    /// <param name="request">New version creation request with file and metadata</param>
    /// <returns>Created document version information</returns>
    [HttpPost(ApiEndPointConstant.Document.CreateNewVersion)]
    [CustomAuthorize(Roles = new[] { Roles.Editor })]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateNewVersion([FromRoute(Name = "id")] string documentId, [FromForm] CreateNewVersionDraftRequest request)
    {
        var result = await _documentService.CreateNewVersionAsync(documentId, request);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Perform enhanced semantic search on documents using AI-powered similarity matching with hybrid scoring
    /// </summary>
    /// <param name="request">Enhanced semantic search request with query text, scoring options, and filters</param>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 10)</param>
    /// <returns>Paginated list of semantically similar documents with detailed scoring information</returns>
    [HttpGet(ApiEndPointConstant.Document.SemanticSearch)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<SemanticSearchResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SemanticSearch([FromQuery] SemanticSearchRequest request, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            // Create filter from request parameters
            var filter = new SemanticSearchFilter
            {
                DocumentTypeId = request.DocumentTypeId,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveUntil = request.EffectiveUntil,
                DepartmentId = request.DepartmentId
            };

            var result = await _documentService.SemanticSearch(request, filter, pageNumber, pageSize);

            var message = request.EnableHybridScoring
                ? $"Found {result.Total} documents using hybrid semantic search"
                : $"Found {result.Total} documents using basic semantic search";

            return Ok(ApiResponse<IPaginate<SemanticSearchResponse>>.Success(result, message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search with query: {Query}", request.Query);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Error("SEARCH_ERROR", "An error occurred while performing semantic search"));
        }
    }

    /// <summary>
    /// Perform full-text search on document content and metadata
    /// </summary>
    /// <param name="filter">Search filter with keywords and criteria</param>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 10)</param>
    /// <returns>Paginated list of documents matching search criteria</returns>
    [HttpGet(ApiEndPointConstant.Document.FullTextSearch)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FullTextSearch([FromQuery] FullTextSearchFilter filter, int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.FullTextSearch(filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    /// <summary>
    /// Get AI-powered document recommendations based on a specific document
    /// </summary>
    /// <param name="documentId">The ID of the document to get recommendations for</param>
    /// <param name="request">Recommendation request parameters</param>
    /// <returns>List of recommended documents with similarity scores</returns>
    [HttpGet(ApiEndPointConstant.Document.GetRecommendations)]
    [ProducesResponseType(typeof(ApiResponse<DocumentRecommendationsResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentRecommendations(
        [FromRoute] string documentId,
        [FromQuery] DocumentRecommendationRequest request)
    {
        var result = await _recommendationService.GetRecommendationsAsync(documentId, request);
        return Ok(ApiResponse<DocumentRecommendationsResult>.Success(result, "Document recommendations retrieved successfully"));
    }

    /// <summary>
    /// Checks if the filter request has any applied filters
    /// </summary>
    /// <param name="filter">The filter request to check</param>
    /// <returns>True if no filters are applied, false otherwise</returns>
    private static bool IsEmptyFilter(OfficialDocumentsFilterRequest filter)
    {
        return filter == null ||
               (string.IsNullOrEmpty(filter.Title) &&
                string.IsNullOrEmpty(filter.Keyword) &&
                string.IsNullOrEmpty(filter.VersionName) &&
                !filter.FromDate.HasValue &&
                !filter.ToDate.HasValue &&
                !filter.EffectiveFrom.HasValue &&
                !filter.EffectiveUntil.HasValue &&
                !filter.LastSubmittedFrom.HasValue &&
                !filter.LastSubmittedTo.HasValue &&
                string.IsNullOrEmpty(filter.DocumentTypeId) &&
                (filter.Tags == null || !filter.Tags.Any()) &&
                string.IsNullOrEmpty(filter.SignedBy) &&
                string.IsNullOrEmpty(filter.FileType) &&
                string.IsNullOrEmpty(filter.SubmittedBy) &&
                !filter.IsPublic.HasValue &&
                !filter.MinFileSize.HasValue &&
                !filter.MaxFileSize.HasValue &&
                !filter.MinDownloads.HasValue &&
                !filter.MaxDownloads.HasValue);
    }
}