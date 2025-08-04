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

    public DocumentController(IDocumentService documentService, IDocumentRecommendationService recommendationService)
    {
        _documentService = documentService;
        _recommendationService = recommendationService;
    }

    [HttpPost(ApiEndPointConstant.Document.UploadDraft)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocumentDraft([FromForm] CreateDraftRequest request)
    {
        var result = await _documentService.CreateDraftAsync(request);
        return Ok(ApiResponse<object>.Success(result, "Document draft uploaded successfully", 201));
    }
    
    [HttpPost(ApiEndPointConstant.Document.AnalyzeDocument)]
    [ProducesResponseType(typeof(ApiResponse<AnalyzeDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnayzeDocumentDraft(IFormFile file)
    {
        var result = await _documentService.AnalyzeDocumentAsync(file);
        return Ok(ApiResponse<object>.Success(result, "Analyze result", 200));
    }

    [HttpPut(ApiEndPointConstant.Document.EditDraft)]
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

    [HttpDelete(ApiEndPointConstant.Document.DeleteDraft)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDocument([FromRoute(Name = "id")] string documentId, string versionId)
    {
        await _documentService.DeleteDraftAsync(documentId, versionId);
        return Ok(ApiResponse<object>.Success(null, "Document deleted successfully", 200));
    }

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

    [HttpGet(ApiEndPointConstant.Document.GetAllOfficialDocuments)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllOfficialDocuments(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetAllOfficialDocumentsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetMyDocuments)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyDocuments([FromQuery] MyDocumentsFilter filter, int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetMyDocumentsAsync(filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetMyDocumentDetail)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyDocumentDetail([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetMyDocumentByIdAsync(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetDrafts)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDrafts(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetDraftsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetDraftById)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDraftById([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetDraftByIdAsync(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetRejectedDocuments)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentDraftResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRejectedDocuments(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetRejectDocumentsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetRejectedById)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRejectedById([FromRoute(Name = "id")] string versionId)
    {
        var result = await _documentService.GetRejectedById(versionId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpPost(ApiEndPointConstant.Document.CreateNewVersion)]
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

    [HttpGet(ApiEndPointConstant.Document.SemanticSearch)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SemanticSearch([FromQuery] SemanticSearchRequest request, [FromQuery] SemanticSearchFilter filter, int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.SemanticSearch(request,filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.FullTextSearch)]
    [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FullTextSearch([FromQuery] FullTextSearchFilter filter, int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.FullTextSearch(filter, pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }

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
}