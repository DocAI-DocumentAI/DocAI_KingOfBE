// AZURE STORAGE INTERFACE - COMMENTED OUT FOR GOOGLE DRIVE MIGRATION
// This file is preserved for potential rollback but not actively used

/*
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    public interface IAzureStorageService
    {
        Task<AzureUploadResponse> UploadFileAsync(IFormFile file, string folder);
        Task DeleteFileAsync(string filename, string folder);
        Task MoveFileAsync(string sourceFilename, string sourceFolder, string destinationFolder);
        Task<Stream> DownloadFileAsync(string filename);
        Task<bool> FileExistsAsync(string filePath);
        Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string filePath);
        Task<string> GetFileContentTypeAsync(string filePath);
    }
}
*/

// END OF COMMENTED AZURE STORAGE INTERFACE
