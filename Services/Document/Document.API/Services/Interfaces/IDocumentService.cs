using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;

namespace Document.API.Services.Interfaces;

public interface IDocumentService
{
    Task<DocumentDraftResponse> CreateDraftAsync(CreateDraftRequest request, string userId);
    Task<DocumentDraftResponse> UpdateDraftAsync(string versionId, UpdateDocumentDraftRequest request, string userId);
    Task DeleteDraftAsync(string documentId, string versionId, string userId);
    Task<IPaginate<DocumentDraftResponse>> GetDraftsAsync(string userId, int pageNumber, int pageSize);
    Task<DocumentDraftResponse> GetDraftByIdAsync(string versionId, string userId);
    Task<IPaginate<DocumentDraftResponse>> GetRejectDocumentsAsync(string userId, int pageNumber, int pageSize);
    Task<DocumentDraftResponse> GetRejectedById(string versionId, string userId);
    Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(IFormFile file);
}
