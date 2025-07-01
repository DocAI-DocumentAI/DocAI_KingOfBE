using Document.API.Models;

namespace Document.API.Services.Interfaces
{
    public interface IAzureStorageService
    {
        Task<AzureUploadResponse> UploadFileAsync(IFormFile file, string folder);
        Task DeleteFileAsync(string filename, string folder);
        Task MoveFileAsync(string sourceFilename, string sourceFolder, string destinationFolder);
        Task DownloadFileAsync(string filename);
    }
}
