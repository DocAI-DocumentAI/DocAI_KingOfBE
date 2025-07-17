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
    }
}
