using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Models;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentReplacementService
    {
        Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsAsync(
            DocumentReplacementSuggestionRequest request,
            string userId);

        Task<DocumentReplacementSuggestionResponse> GetReplacementSuggestionsForEditAsync(
            string documentId,
            DocumentReplacementSuggestionRequest request,
            string userId);

        Task ClearReplacementCacheAsync(string documentTypeId, string departmentId);

        Task ClearAllReplacementCachesAsync();

        Task<ReplacementSuggestionScoring> GetScoringBreakdownAsync(
            DocumentReplacementSuggestionRequest request,
            string candidateDocumentId,
            string userId);

        Task<bool> CanReplaceDocumentAsync(string documentId, string userId);

        Task PreWarmCacheAsync(List<string> documentTypeIds);
    }
}
