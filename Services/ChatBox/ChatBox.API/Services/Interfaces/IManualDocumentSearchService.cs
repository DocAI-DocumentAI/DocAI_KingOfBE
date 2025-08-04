namespace ChatBox.API.Services.Interfaces
{
    public interface IManualDocumentSearchService
    {
        Task<string> SearchAndAnswerAsync(string query, string userId);
        bool ShouldSearchDocuments(string message);
    }
}
