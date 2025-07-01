using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Document.API.Models;
using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _blobContainerClient;
        private readonly ILogger<AzureStorageService> _logger;
        public AzureStorageService(IConfiguration configuration, ILogger<AzureStorageService> logger)
        {
            _logger = logger;
            _configuration = configuration;
            var connectionString = configuration["AzureStorage:BlobStorage:ConnectionString"];
            var containerName = configuration["AzureStorage:BlobStorage:ContainerName"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(containerName))
            {
                _logger.LogCritical("Azure Storage configuration is missing");
                throw new InvalidOperationException("Azure Storage is not configured");
            }

            try
            {
                _blobContainerClient = new BlobContainerClient(connectionString, containerName);
                // Ensure the container exists when the service is initialized.
                _blobContainerClient.CreateIfNotExists(PublicAccessType.BlobContainer);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to connect to Azure Blob Storage container '{ContainerName}'", containerName);
                throw; // Re-throw the exception to stop the application startup
            }

        }
        public async Task<AzureUploadResponse> UploadFileAsync(IFormFile file)
        {
            var blobName = file.FileName;
            var blobClient = _blobContainerClient.GetBlobClient(blobName);

            _logger.LogInformation("Uploading file '{FileName}' to Azure Blob Storage as '{BlobName}'.", file.FileName, blobName);

            await using (var stream = file.OpenReadStream())
            {
                var response = await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
                var md5Hash = Convert.ToBase64String(response.Value.ContentHash);

                return new AzureUploadResponse
                {
                    BlobName = blobName,
                    Md5Hash = md5Hash
                };
            }
        }
        public async Task DeleteFileAsync(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                _logger.LogWarning("DeleteFileAsync called with an empty blob name.");
                return;
            }

            var blobClient = _blobContainerClient.GetBlobClient(filename);
            _logger.LogInformation("Deleting blob '{BlobName}' from Azure Storage.", filename);
            await blobClient.DeleteIfExistsAsync();
        }

        public Task<string> DownloadFileAsync(string fileUrl)
        {
            throw new NotImplementedException();
        }

    }
}

