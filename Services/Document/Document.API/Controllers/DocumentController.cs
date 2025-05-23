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

    [HttpPost(ApiEndPointConstant.Document.Upload)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest uploadDocumentRequest)
    {
        await _documentService.UploadDocumentAsync(uploadDocumentRequest);
        return Ok(ApiResponse<object>.Success(null, "Document uploaded successfully", 201));
    }

    [HttpGet(ApiEndPointConstant.Document.GetDocument)]
    public async Task<IActionResult> GetDocument([FromRoute(Name = "id")] string documentId)
    {
        var result = await _documentService.GetDocumentByIdAsync(documentId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpPut(ApiEndPointConstant.Document.UpdateMetaData)]
    public async Task<IActionResult> UpdateMetaData([FromRoute(Name = "id")] string documentId, UpdateMetaDataReqest request)
    {
        var result = await _documentService.UpdateMetaDataDocumentAsync(documentId, request);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpDelete(ApiEndPointConstant.Document.Delete)]
    public async Task<IActionResult> DeleteDocument([FromRoute(Name = "id")] string documentId)
    {
        await _documentService.DeleteDocumentAsync(documentId);
        return Ok(ApiResponse<object>.Success(null, "Document deleted successfully", 200));
    }
}