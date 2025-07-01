using Document.API.Models;

namespace Document.API.Services.Interfaces
{
    public interface IAzureStorageService
    {
        Task<AzureUploadResponse> UploadFileAsync(IFormFile file);
        Task DeleteFileAsync(string filename);
        Task<string> DownloadFileAsync(string filename);
    }
}
