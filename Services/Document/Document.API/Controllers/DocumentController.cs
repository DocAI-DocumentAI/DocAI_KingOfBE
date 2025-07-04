using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers;

[Route(ApiEndPointConstant.ApiEndpoint)]
[ApiController]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost(ApiEndPointConstant.Document.UploadDraft)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocumentDraft([FromForm] CreateDraftRequest request, string userId)
    {
        var result = await _documentService.CreateDraftAsync(request, userId);
        return Ok(ApiResponse<object>.Success(result, "Document draft uploaded successfully", 201));
    }
    [HttpPost(ApiEndPointConstant.Document.AnalyzeDocument)]
    public async Task<IActionResult> AnayzeDocumentDraft(IFormFile file)
    {
        var result = await _documentService.AnalyzeDocumentAsync(file);
        return Ok(ApiResponse<object>.Success(result, "Analyze result", 200));
    }

    [HttpPut(ApiEndPointConstant.Document.EditDraft)]
    public async Task<IActionResult> EditDraft([FromRoute(Name = "id")] string documentId, UpdateDocumentDraftRequest request, string userId)
    {
        var result = await _documentService.UpdateDraftAsync(documentId, request, userId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpDelete(ApiEndPointConstant.Document.DeleteDraft)]
    public async Task<IActionResult> DeleteDocument([FromRoute(Name = "id")] string documentId, string versionId, string userId)
    {
        await _documentService.DeleteDraftAsync(documentId, versionId, userId);
        return Ok(ApiResponse<object>.Success(null, "Document deleted successfully", 200));
    }

    [HttpGet(ApiEndPointConstant.Document.GetOfficialDocument)]
    public async Task<IActionResult> GetOfficialDocument([FromRoute(Name = "id")] string documentFileId)
    {
        var result = await _documentService.GetOfficialDocumentAsync(documentFileId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet(ApiEndPointConstant.Document.GetAllOfficialDocuments)]
    public async Task<IActionResult> GetAllOfficialDocuments(int pageNumber = 1, int pageSize = 10)
    {
        var result = await _documentService.GetAllOfficialDocumentsAsync(pageNumber, pageSize);
        return Ok(ApiResponse<object>.Success(result));
    }
}