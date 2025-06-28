namespace Document.API.Services.Interfaces
{
    public interface IAzureStorageService
    {
        Task<string> UploadFileAsync(IFormFile file);
        Task DeleteFileAsync(string filename);
        Task<string> DownloadFileAsync(string filename);
    }
}
