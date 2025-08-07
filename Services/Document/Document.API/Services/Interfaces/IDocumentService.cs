using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Domain.Models;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;

namespace Document.API.Services.Interfaces;

public interface IDocumentService
{
    Task<DocumentDraftResponse> CreateDraftAsync(CreateDraftRequest request);
    Task<DocumentDraftResponse> UpdateDraftAsync(string versionId, UpdateDocumentDraftRequest request);
    Task DeleteDraftAsync(string documentId, string versionId);
    Task<IPaginate<DocumentDraftResponse>> GetDraftsAsync(int pageNumber, int pageSize);
    Task<DocumentDraftResponse> GetDraftByIdAsync(string versionId);
    Task<IPaginate<DocumentDraftResponse>> GetRejectDocumentsAsync(int pageNumber, int pageSize);
    Task<DocumentDraftResponse> GetRejectedById(string versionId);
    Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(IFormFile file);
    Task<RegenerateSummaryResponse> RegenerateSummaryAsync(IFormFile file);
    Task<DocumentDraftResponse> GetOfficialDocumentAsync(string documentFileId);
    Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(int pageNumber, int pageSize);
    Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(OfficialDocumentsFilterRequest filter, int pageNumber, int pageSize);
    Task<IPaginate<DocumentDraftResponse>> GetMyDocumentsAsync(MyDocumentsFilter filter, int pageNumber, int pageSize);
    Task<DocumentDraftResponse> GetMyDocumentByIdAsync(string versionId);
    Task<DocumentDraftResponse> CreateNewVersionAsync(string documentId, CreateNewVersionDraftRequest request);
    Task<List<DocumentVersionResponse>> GetDocumentVersionsAsync(string documentId);
    Task<DocumentVersionResponse> GetDocumentVersionByVersionIdAsync(string documentId, string versionId);
    Task<IPaginate<SemanticSearchResponse>> SemanticSearch(SemanticSearchRequest request, SemanticSearchFilter filter, int pageNumber, int pageSize);
    Task<IPaginate<DocumentDraftResponse>> FullTextSearch(FullTextSearchFilter filter, int pageNumber, int pageSize);

    // File serving methods
    Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string versionId);
    Task<(Stream stream, string contentType, string fileName)> GetFileForDownloadAsync(string versionId);
    Task<DocumentVersion> GetFileInfoAsync(string versionId);
}
