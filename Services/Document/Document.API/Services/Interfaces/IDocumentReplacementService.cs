using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Models;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentReplacementService
    {
        Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsAsync(
            DocumentReplacementSuggestionRequest request);

        Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsForEditAsync(
            string documentId,
            DocumentReplacementSuggestionRequest request);

        Task ClearReplacementCacheAsync(string documentTypeId, string departmentId);

        Task ClearAllReplacementCachesAsync();

        Task<ReplacementSuggestionScoring> GetScoringBreakdownAsync(
            DocumentReplacementSuggestionRequest request,
            string candidateDocumentId);

        Task<bool> CanReplaceDocumentAsync(string documentId);

        Task PreWarmCacheAsync(List<string> documentTypeIds);
    }
}
