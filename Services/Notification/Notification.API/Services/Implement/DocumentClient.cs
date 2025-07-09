using Notification.Api.Constants;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class DocumentClient : IDocumentClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DocumentClient> _logger;

        public DocumentClient(HttpClient httpClient, ILogger<DocumentClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> DeactivateDocumentWarningsAsync(Guid documentId, string version)
        {
            throw new NotImplementedException();
        }

        public Task<List<DocumentDetailResponseExternal>> GetDocumentsForExpirationCheckAsync(DateTime warningDate)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateDocumentStatusAsync(Guid documentId, string version, string newStatus)
        {
            throw new NotImplementedException();
        }

        //public async Task<DocumentDetailResponseExternal?> GetDocumentDetailsAsync(string documentId, string documentVersion)
        //{
        //    _logger.LogInformation("Calling Document Microservice to get details for DocId: {DocId}, Version: {Version}", documentId, documentVersion);
        //    try
        //    {
        //        // Giả định Document Service có endpoint /api/documents/{documentId}/versions/{versionId}
        //        var response = await _httpClient.GetAsync($"api/documents/{documentId}/versions/{documentVersion}");
        //        response.EnsureSuccessStatusCode();
        //        var docDetails = await response.Content.ReadFromJsonAsync<DocumentDetailResponseExternal>();
        //        return docDetails;
        //    }
        //    catch (HttpRequestException httpEx)
        //    {
        //        _logger.LogError(httpEx, "HTTP Error calling Document GetDetails API: {Message}. Status: {StatusCode}", httpEx.Message, httpEx.StatusCode);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An unexpected error occurred while getting document details.");
        //        return null;
        //    }
        //}

        //public async Task<List<DocumentDetailResponseExternal>> GetExpiringAndExpiredDocumentsAsync(DateTime expiresBefore, DateTime expiredAsOf, string statusFilter)
        //{
        //    _logger.LogInformation("Calling Document Microservice to get expiring/expired documents. ExpiresBefore: {ExpiresBefore}, ExpiredAsOf: {ExpiredAsOf}, Status: {Status}", expiresBefore, expiredAsOf, statusFilter);
        //    try
        //    {
        //        // Giả định Document Service có endpoint /api/documents/filter-by-date
        //        var response = await _httpClient.GetAsync($"api/documents/filter-by-date?status={statusFilter}&expiresBefore={expiresBefore:yyyy-MM-dd}&expiredAsOf={expiredAsOf:yyyy-MM-dd}");
        //        response.EnsureSuccessStatusCode();
        //        var documents = await response.Content.ReadFromJsonAsync<List<DocumentDetailResponseExternal>>();
        //        return documents ?? new List<DocumentDetailResponseExternal>();
        //    }
        //    catch (HttpRequestException httpEx)
        //    {
        //        _logger.LogError(httpEx, "HTTP Error calling Document GetExpiringAndExpired API: {Message}. Status: {StatusCode}", httpEx.Message, httpEx.StatusCode);
        //        return new List<DocumentDetailResponseExternal>();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An unexpected error occurred while getting expiring/expired documents.");
        //        return new List<DocumentDetailResponseExternal>();
        //    }
        //}

        //public async Task<bool> UpdateDocumentStatusAsync(string documentId, string documentVersion, string newStatus)
        //{
        //    _logger.LogInformation("Calling Document Microservice to update status for DocId: {DocId}, Version: {Version} to {NewStatus}", documentId, documentVersion, newStatus);
        //    try
        //    {
        //        // Giả định Document Service có API PATCH /api/documents/{documentId}/versions/{version}/status
        //        var requestBody = new { Status = newStatus }; // DTO đơn giản cho request Patch
        //        var response = await _httpClient.PatchAsJsonAsync($"api/documents/{documentId}/versions/{documentVersion}/status", requestBody);
        //        response.EnsureSuccessStatusCode();
        //        return true;
        //    }
        //    catch (HttpRequestException httpEx)
        //    {
        //        _logger.LogError(httpEx, "HTTP Error calling Document UpdateStatus API: {Message}. Status: {StatusCode}", httpEx.Message, httpEx.StatusCode);
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An unexpected error occurred while updating document status.");
        //        return false;
        //    }
        //}

        //public async Task<bool> DeactivateDocumentWarningsAsync(string documentId, string documentVersion)
        //{
        //    _logger.LogInformation("Calling Document Microservice to deactivate warnings for DocId: {DocId}, Version: {Version}", documentId, documentVersion);
        //    try
        //    {
        //        // Giả định Document Service có API POST /api/documents/{documentId}/versions/{version}/deactivate-warnings
        //        var response = await _httpClient.PostAsJsonAsync($"api/documents/{documentId}/versions/{documentVersion}/deactivate-warnings", new { }); // Gửi empty body
        //        response.EnsureSuccessStatusCode();
        //        return true;
        //    }
        //    catch (HttpRequestException httpEx)
        //    {
        //        _logger.LogError(httpEx, "HTTP Error calling Document DeactivateWarnings API: {Message}. Status: {StatusCode}", httpEx.Message, httpEx.StatusCode);
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An unexpected error occurred while deactivating document warnings.");
        //        return false;
        //    }
        //}
    }
}
