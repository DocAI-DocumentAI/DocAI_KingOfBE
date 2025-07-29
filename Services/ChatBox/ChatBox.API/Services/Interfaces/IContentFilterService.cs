namespace ChatBox.API.Services.Interfaces
{
    public interface IContentFilterService
    {
        Task<bool> IsContentAllowedAsync(string content);
        Task<List<string>> GetProhibitedWordsInContentAsync(string content);
    }
}
