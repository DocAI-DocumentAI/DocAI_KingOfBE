namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentSearchService
    {
        Task<List<string>> SearchDocumentsAsync(string query, int limit = 5);
        Task<string> GetDocumentContentAsync(string documentId);
    }
}
