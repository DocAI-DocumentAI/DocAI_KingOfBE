using System.Text;
using System.Text.Json;
using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response.DocumentServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class DocumentServiceClient : IDocumentServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DocumentServiceClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _documentServiceBaseUrl;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IAuditService _auditService;

        public DocumentServiceClient(
            HttpClient httpClient,
            ILogger<DocumentServiceClient> logger,
            IConfiguration configuration,
            IAuditService auditService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _auditService = auditService;
            _documentServiceBaseUrl = configuration["Services:DocumentService:BaseUrl"] ?? "http://localhost:5003";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            ConfigureHttpClient();
        }

        public async Task<DocumentSearchResponse> SearchDocumentsAsync(DocumentSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Searching documents for user {UserId}, Query: {Query}",
                    request.UserId, request.Query);

                var response = await PostAsync<DocumentSearchResponse>("/api/documents/search", request);

                if (response != null)
                {
                    _logger.LogInformation("Document search completed. Found {DocumentCount} documents",
                        response.Documents?.Count ?? 0);

                    // Log search activity for audit
                    await _auditService.LogAsync(request.UserId, "DocumentSearch", "Search", request.Query,
                        null, new { Query = request.Query, ResultCount = response.Documents?.Count ?? 0 });

                    return response;
                }

                return CreateEmptySearchResponse(request.Query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents for user {UserId}", request.UserId);

                await _auditService.LogSecurityEventAsync(request.UserId, "DocumentSearchError",
                    $"Error searching documents: {ex.Message}", "medium");

                return CreateEmptySearchResponse(request.Query);
            }
        }

        public async Task<List<DocumentCitation>> SearchDocumentsByIdsAsync(List<string> documentIds, Guid userId)
        {
            try
            {
                _logger.LogInformation("Searching documents by IDs for user {UserId}, Count: {DocumentCount}",
                    userId, documentIds.Count);

                var request = new { DocumentIds = documentIds, UserId = userId };
                var response = await PostAsync<List<DocumentCitation>>("/api/documents/search/by-ids", request);

                if (response != null)
                {
                    _logger.LogInformation("Retrieved {DocumentCount} document citations", response.Count);

                    await _auditService.LogAsync(userId, "DocumentSearchByIds", "Search", string.Join(",", documentIds),
                        null, new { DocumentIds = documentIds, ResultCount = response.Count });

                    return response;
                }

                return new List<DocumentCitation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents by IDs for user {UserId}", userId);
                return new List<DocumentCitation>();
            }
        }

        public async Task<DocumentAccessResponse> CheckDocumentAccessAsync(DocumentAccessRequest request)
        {
            try
            {
                _logger.LogDebug("Checking document access for user {UserId}, Document: {DocumentId}",
                    request.UserId, request.DocumentId);

                var response = await PostAsync<DocumentAccessResponse>("/api/documents/access/check", request);

                if (response != null)
                {
                    await _auditService.LogAsync(request.UserId, "DocumentAccessCheck", "Access", request.DocumentId,
                        null, new { DocumentId = request.DocumentId, HasAccess = response.HasAccess, Reason = response.Reason });

                    return response;
                }

                return new DocumentAccessResponse
                {
                    HasAccess = false,
                    Reason = "Unable to verify access permissions",
                    RequiredPermissions = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking document access for user {UserId}, Document: {DocumentId}",
                    request.UserId, request.DocumentId);

                return new DocumentAccessResponse
                {
                    HasAccess = false,
                    Reason = $"Access check failed: {ex.Message}",
                    RequiredPermissions = new List<string>()
                };
            }
        }

        public async Task<BatchDocumentResponse> CheckBatchAccessAsync(BatchDocumentRequest request)
        {
            try
            {
                _logger.LogInformation("Checking batch document access for user {UserId}, Documents: {DocumentCount}",
                    request.UserId, request.DocumentIds.Count);

                var response = await PostAsync<BatchDocumentResponse>("/api/documents/access/batch-check", request);

                if (response != null)
                {
                    _logger.LogInformation("Batch access check completed. Accessible: {AccessibleCount}, Restricted: {RestrictedCount}",
                        response.AccessibleDocuments.Count, response.RestrictedDocuments.Count);

                    await _auditService.LogAsync(request.UserId, "BatchDocumentAccessCheck", "Access", string.Join(",", request.DocumentIds),
                        null, new
                        {
                            DocumentIds = request.DocumentIds,
                            AccessibleCount = response.AccessibleDocuments.Count,
                            RestrictedCount = response.RestrictedDocuments.Count
                        });

                    return response;
                }

                return new BatchDocumentResponse
                {
                    AccessibleDocuments = new List<string>(),
                    RestrictedDocuments = request.DocumentIds,
                    AccessReasons = request.DocumentIds.ToDictionary(id => id, id => "Access verification failed")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking batch document access for user {UserId}", request.UserId);

                return new BatchDocumentResponse
                {
                    AccessibleDocuments = new List<string>(),
                    RestrictedDocuments = request.DocumentIds,
                    AccessReasons = request.DocumentIds.ToDictionary(id => id, id => $"Access check failed: {ex.Message}")
                };
            }
        }

        public async Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId, Guid userId)
        {
            try
            {
                _logger.LogDebug("Getting document metadata for user {UserId}, Document: {DocumentId}",
                    userId, documentId);

                var request = new { DocumentId = documentId, UserId = userId };
                var response = await PostAsync<DocumentMetadata>("/api/documents/metadata", request);

                if (response != null)
                {
                    await _auditService.LogAsync(userId, "GetDocumentMetadata", "DocumentMetadata", documentId,
                        null, new { DocumentId = documentId, Title = response.Title });

                    return response;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document metadata for user {UserId}, Document: {DocumentId}",
                    userId, documentId);
                return null;
            }
        }

        public async Task<BatchDocumentResponse> GetBatchMetadataAsync(BatchDocumentRequest request)
        {
            try
            {
                _logger.LogInformation("Getting batch document metadata for user {UserId}, Documents: {DocumentCount}",
                    request.UserId, request.DocumentIds.Count);

                var response = await PostAsync<BatchDocumentResponse>("/api/documents/metadata/batch", request);

                if (response != null)
                {
                    await _auditService.LogAsync(request.UserId, "GetBatchDocumentMetadata", "DocumentMetadata", string.Join(",", request.DocumentIds),
                        null, new { DocumentIds = request.DocumentIds, ResultCount = response.AccessibleDocuments.Count });

                    return response;
                }

                return new BatchDocumentResponse
                {
                    AccessibleDocuments = new List<string>(),
                    RestrictedDocuments = request.DocumentIds,
                    AccessReasons = request.DocumentIds.ToDictionary(id => id, id => "Metadata retrieval failed")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting batch document metadata for user {UserId}", request.UserId);

                return new BatchDocumentResponse
                {
                    AccessibleDocuments = new List<string>(),
                    RestrictedDocuments = request.DocumentIds,
                    AccessReasons = request.DocumentIds.ToDictionary(id => id, id => $"Metadata retrieval failed: {ex.Message}")
                };
            }
        }

        public async Task<DocumentContent> GetDocumentContentAsync(string documentId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting document content for user {UserId}, Document: {DocumentId}",
                    userId, documentId);

                var request = new { DocumentId = documentId, UserId = userId };
                var response = await PostAsync<DocumentContent>("/api/documents/content", request);

                if (response != null)
                {
                    await _auditService.LogAsync(userId, "GetDocumentContent", "DocumentContent", documentId,
                        null, new { DocumentId = documentId, ContentLength = response.Content?.Length ?? 0 });

                    return response;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document content for user {UserId}, Document: {DocumentId}",
                    userId, documentId);
                return null;
            }
        }

        public async Task<DocumentStatusResponse> CheckDocumentStatusAsync(DocumentStatusRequest request)
        {
            try
            {
                _logger.LogDebug("Checking document status for Document: {DocumentId}", request.DocumentId);

                var response = await PostAsync<DocumentStatusResponse>("/api/documents/status", request);

                if (response != null)
                {
                    return response;
                }

                return new DocumentStatusResponse
                {
                    DocumentId = request.DocumentId,
                    Status = "unknown",
                    IsAccessible = false,
                    StatusReason = "Unable to determine document status",
                    LastStatusCheck = DateTime.UtcNow,
                    LifecycleInfo = new DocumentLifecycleInfo(),
                    AvailableVersions = new List<string>(),
                    StatusMetadata = new Dictionary<string, object>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking document status for Document: {DocumentId}", request.DocumentId);

                return new DocumentStatusResponse
                {
                    DocumentId = request.DocumentId,
                    Status = "error",
                    IsAccessible = false,
                    StatusReason = $"Status check failed: {ex.Message}",
                    LastStatusCheck = DateTime.UtcNow,
                    LifecycleInfo = new DocumentLifecycleInfo(),
                    AvailableVersions = new List<string>(),
                    StatusMetadata = new Dictionary<string, object> { { "Error", ex.Message } }
                };
            }
        }

        public async Task<List<string>> GetDocumentCategoriesAsync()
        {
            try
            {
                _logger.LogDebug("Getting document categories");

                var response = await GetAsync<List<string>>("/api/documents/categories");

                if (response != null)
                {
                    _logger.LogInformation("Retrieved {CategoryCount} document categories", response.Count);
                    return response;
                }

                return GetFallbackCategories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document categories");
                return GetFallbackCategories();
            }
        }

        // Private helper methods
        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_documentServiceBaseUrl);
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ChatBox-Service/1.0");

            // Add API key if configured
            var apiKey = _configuration["Services:DocumentService:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }

        private async Task<T> PostAsync<T>(string endpoint, object request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Document Service API call failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            return default(T);
        }

        private async Task<T> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Document Service API call failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            return default(T);
        }

        private DocumentSearchResponse CreateEmptySearchResponse(string query)
        {
            return new DocumentSearchResponse
            {
                Documents = new List<DocumentSearchItem>(),
                TotalCount = 0,
                Query = query,
                ProcessingTime = TimeSpan.Zero
            };
        }

        private List<string> GetFallbackCategories()
        {
            return new List<string>
            {
                "General",
                "Policies",
                "Procedures",
                "HR",
                "IT",
                "Finance",
                "Legal",
                "Training",
                "Marketing",
                "Operations"
            };
        }
    }
}
