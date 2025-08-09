using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IManualDocumentSearchService
    {
        Task<string> SearchAndAnswerAsync(string query, string userId);

        Task<(string RawContent, List<DocumentInfo> Sources)> SearchWithSourcesAsync(string query, string userId);

        bool ShouldSearchDocuments(string message);

    }
}
