using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class MockDocumentClient : IDocumentClient
    {
        private readonly ILogger<MockDocumentClient> _logger;

        public MockDocumentClient(ILogger<MockDocumentClient> logger)
        {
            _logger = logger;
            _logger.LogWarning("--- Using MockDocumentClient for development purposes. ---");
        }

        public Task<List<DocumentDetailResponseExternal>> GetDocumentsForExpirationCheckAsync(DateTime warningDate)
        {
            _logger.LogInformation("MOCK: Getting documents for expiration check.");
            var mockDocuments = new List<DocumentDetailResponseExternal>
            {
                // Case 1: Document nearing expiration
                new DocumentDetailResponseExternal
                {
                    DocumentId = Guid.NewGuid(),
                    Title = "Financial Report Q2 2025",
                    Version = "1.0",
                    DepartmentId = Guid.Parse("d1e3f5c7-a9b8-4d6e-8c1a-2b3d4e5f6a7b"), // Sample Dept ID
                    EffectiveUntil = DateTime.UtcNow.AddDays(5), // Expires in 5 days
                    Status = "Approved",
                    DocumentLink = "http://example.com/doc/1"
                },
                // Case 2: Document has expired
                new DocumentDetailResponseExternal
                {
                    DocumentId = Guid.NewGuid(),
                    Title = "Marketing Campaign Plan",
                    Version = "2.1",
                    DepartmentId = Guid.Parse("e2f4a6b8-c9d7-5e8f-9b2a-3c4d5e6f7a8b"), // Sample Dept ID
                    EffectiveUntil = DateTime.UtcNow.AddDays(-2), // Expired 2 days ago
                    Status = "Approved",
                    DocumentLink = "http://example.com/doc/2"
                },
                // Case 3: Document not yet in warning period
                new DocumentDetailResponseExternal
                {
                    DocumentId = Guid.NewGuid(),
                    Title = "HR Policy Handbook",
                    Version = "3.0",
                    DepartmentId = Guid.Parse("d1e3f5c7-a9b8-4d6e-8c1a-2b3d4e5f6a7b"),
                    EffectiveUntil = DateTime.UtcNow.AddDays(30),
                    Status = "Approved",
                    DocumentLink = "http://example.com/doc/3"
                }
            };
            return Task.FromResult(mockDocuments);
        }

        public Task<bool> UpdateDocumentStatusAsync(Guid documentId, string version, string newStatus)
        {
            _logger.LogInformation("MOCK: Updated status of DocId {DocId} to '{NewStatus}'", documentId, newStatus);
            return Task.FromResult(true);
        }

        public Task<bool> DeactivateDocumentWarningsAsync(Guid documentId, string version)
        {
            _logger.LogInformation("MOCK: Deactivated warnings for DocId {DocId}", documentId);
            return Task.FromResult(true);
        }
    }
}