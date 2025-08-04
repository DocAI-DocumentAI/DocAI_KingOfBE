// AZURE STORAGE SERVICE - COMMENTED OUT FOR GOOGLE DRIVE MIGRATION
// This file is preserved for potential rollback but not actively used
// All Azure dependencies have been commented out in the project

/*
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Document.API.Constants;
using Document.API.Payload.Response;
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
                throw new InvalidOperationException(MessageConstant.AzureStorageNotConfigured);
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
        public async Task<AzureUploadResponse> UploadFileAsync(IFormFile file, string folder)
        {
            var blobName = $"{folder}/{file.FileName}";
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
        public async Task DeleteFileAsync(string filename, string folder)
        {
            if (string.IsNullOrEmpty(filename))
            {
                _logger.LogWarning("DeleteFileAsync called with an empty blob name.");
                return;
            }

            var blobName = $"{folder}/{filename}";
            var blobClient = _blobContainerClient.GetBlobClient(blobName);
            _logger.LogInformation("Deleting blob '{BlobName}' from Azure Storage.", blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task MoveFileAsync(string sourceFilename, string sourceFolder, string destinationFolder)
        {
            var sourceBlobName = $"{sourceFolder}/{sourceFilename}";
            var destinationBlobName = $"{destinationFolder}/{sourceFilename}";

            var sourceBlobClient = _blobContainerClient.GetBlobClient(sourceBlobName);
            var destinationBlobClient = _blobContainerClient.GetBlobClient(destinationBlobName);

            _logger.LogInformation("Moving blob from '{SourceBlob}' to '{DestinationBlob}'.", sourceBlobName, destinationBlobName);

            // Copy the blob
            await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

            // Delete the source blob
            await sourceBlobClient.DeleteIfExistsAsync();
        }

        public async Task<Stream> DownloadFileAsync(string filename)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(filename);
                var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from {filename}", filename);
                throw;
            }
        }

        public async Task<bool> FileExistsAsync(string filePath)
        {
            var blobClient = _blobContainerClient.GetBlobClient(filePath);
            return await blobClient.ExistsAsync();
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string filePath)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(filePath);

                // Check if file exists
                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                // Get blob properties to retrieve content type and metadata
                var properties = await blobClient.GetPropertiesAsync();

                // Download the file to a memory stream
                var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                memoryStream.Position = 0;

                // Extract filename from the blob path
                var fileName = Path.GetFileName(filePath);

                // Get content type from blob properties or determine from file extension
                var contentType = properties.Value.ContentType;
                if (string.IsNullOrEmpty(contentType))
                {
                    contentType = GetContentTypeFromExtension(fileName);
                }

                return (memoryStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file for viewing from {FilePath}", filePath);
                throw;
            }
        }

        public async Task<string> GetFileContentTypeAsync(string filePath)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(filePath);
                var properties = await blobClient.GetPropertiesAsync();

                var contentType = properties.Value.ContentType;
                if (string.IsNullOrEmpty(contentType))
                {
                    var fileName = Path.GetFileName(filePath);
                    contentType = GetContentTypeFromExtension(fileName);
                }

                return contentType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting content type for file {FilePath}", filePath);
                throw;
            }
        }

        private static string GetContentTypeFromExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}
*/

// END OF COMMENTED AZURE STORAGE SERVICE


